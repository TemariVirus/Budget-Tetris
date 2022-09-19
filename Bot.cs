namespace TetrisAI;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Tetris;

public class Bot
{
    const double MINTRESH = -1, MAXTRESH = -0.01, MOVEMUL = 1.05, MOVETARGET = 5,
                 DISCOUNT_FACTOR = 0.95;

    private Game _Game;
    public Game Game
    {
        get => _Game;
        set
        {
            _Game = value;
            _Game.SoftG = 40;
        }
    }
    private GameBase Sim;
    bool Done;

    private readonly NN Network;
    public bool UsePCFinder = true;
    long ThinkTicks = Stopwatch.Frequency;
    double MoveTresh = -0.1;

    int CurrentDepth;
    double MaxDepth;

    readonly Dictionary<ulong, double> CachedValues = new Dictionary<ulong, double>();
    readonly Dictionary<ulong, double> CachedStateValues = new Dictionary<ulong, double>();
    readonly ulong[][] MatrixHashTable = RandomArray(10, 40), NextHashTable;
    readonly ulong[] PieceHashTable = RandomArray(8), HoldHashTable = RandomArray(8);
    readonly double[] Discounts;

    readonly Stopwatch Sw = new Stopwatch();
    bool TimesUp;

    const int RunAvgCount = 20;
    long[] NodeCounts = new long[RunAvgCount];

    private Bot(Game game)
    {
        Game = game;

        NextHashTable = RandomArray(game.Next.Length, 8);

        Discounts = new double[game.Next.Length];
        for (int i = 0; i < game.Next.Length; i++) Discounts[i] = Math.Pow(DISCOUNT_FACTOR, i);

        CachedValues.EnsureCapacity(200000);
        CachedStateValues.EnsureCapacity(5000000);
    }

    public Bot(NN network, Game game) : this(game)
    {
        Network = network;
        game.Name = "Bot";
    }

    public Bot(string filePath, Game game) : this(NN.LoadNN(filePath), game)
    {
        string name = filePath.Substring(Math.Max(0, filePath.LastIndexOf('\\')));
        name = name.Substring(1, name.LastIndexOf('.') - 1);
        game.Name = name;
    }

    static ulong[] RandomArray(int size)
    {
        ulong[] arr = new ulong[size];
        byte[] bytes = new byte[size * sizeof(ulong)];
        Random rand = new Random(Guid.NewGuid().GetHashCode());
        rand.NextBytes(bytes);
        Buffer.BlockCopy(bytes, 0, arr, 0, bytes.Length);

        return arr;
    }

    static ulong[][] RandomArray(int size1, int size2)
    {
        ulong[][] arr = new ulong[size1][];
        for (int i = 0; i < size1; i++)
            arr[i] = RandomArray(size2);
        return arr;
    }

    public void Start(int think_time, int move_delay)
    {
        ThinkTicks = think_time * Stopwatch.Frequency / 1000;
        Game.TickingCallback = () =>
        {
            Done = true;
        };
        Thread main = new Thread(() =>
        {
            while (!Game.IsDead)
            {
                List<Moves> moves = FindMoves();
                while (Sw.ElapsedTicks < ThinkTicks) Thread.Sleep(0);
                foreach (Moves move in moves)
                {
                    Game.Play(move);
                    // Move delay
                    Thread.Sleep(move_delay);
                }
                Done = false;
                while (!Done) Thread.Sleep(0);
                Game.WriteAt(0, 23, ConsoleColor.White, $"Depth: {MaxDepth}".PadRight(Game.GAMEWIDTH));
                long count = NodeCounts.Aggregate(0, (aggregate, next) => (next == 0) ? aggregate : aggregate + 1);
                if (count == 0) count++;
                Game.WriteAt(0, 24, ConsoleColor.White, $"Nodes: {NodeCounts.Sum() / count}".PadRight(Game.GAMEWIDTH));
                Game.WriteAt(0, 25, ConsoleColor.White, $"Tresh: {Math.Round(MoveTresh, 6)}".PadRight(Game.GAMEWIDTH));
                for (int i = NodeCounts.Length - 1; i > 0; i--)
                    NodeCounts[i] = NodeCounts[i - 1];
                NodeCounts[0] = 0;
            }
        });
        main.Priority = ThreadPriority.Highest;
        main.Start();
    }

    int[] SearchScore(bool lastrot, ref int comb, ref int _b2b, out double _trash, out int cleared)
    {
        // { scoreadd, B2B, T - spin, combo, clears } //first bit of B2B = B2B chain status
        // Check for t - spins
        int tspin = Sim.TSpin(lastrot);
        // Clear lines
        int[] clears = Sim.Place(out cleared);
        // Combo
        comb = cleared == 0 ? -1 : comb + 1;
        // Perfect clear
        bool pc = true;
        for (int x = 0; x < 10; x++) if (Sim.Matrix[39][x] != 0) pc = false;
        // Compute score
        if (tspin == 0 && cleared != 4 && cleared != 0) _b2b = -1;
        else if (tspin + cleared > 3) _b2b++;

        //_trash = new double[] { 0, 0, 1, 2, 4 }[cleared];
        if (pc) _trash = 5;
        else _trash = new double[] { 0, 0, 1, 1.5, 5 }[cleared];
        //if (info[2] == 3) _trash += new double[] { 0, 2, 3, 4 }[cleared];
        if (tspin == 3) _trash += new double[] { 0, 2, 5, 8.5 }[cleared];
        if ((tspin + cleared > 3) && _b2b > -1) _trash++;
        //if (pc) _trash += 4;
        //if (comb > 0) _trash += new double[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 9)];
        if (comb > 0) _trash += new double[] { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 11)]; //jstris combo table
        // Modify _trash(use trash sent * APL and offset it slightly)
        //if (cleared != 0 && comb > 1) _trash = Math.FusedMultiplyAdd(_trash, _trash / cleared, -1.5);

        return clears;
    }

    public List<Moves> FindMoves()
    {
        // Get copy of attached game
        Sim = Game.Clone();
        GameBase pathfind = Game.Clone();

        // Call the dedicated PC Finder
        //

        Sw.Restart();
        TimesUp = false;

        List<Moves> bestMoves = new List<Moves>();
        double bestScore = double.MinValue;

        // Keep searching until time's up or no next pieces left
        MaxDepth = 0;
        int combo = Game.Combo, b2b = Game.B2B;
        double out1 = Network.FeedFoward(ExtrFeat(0))[0];
        for (int depth = 0; !TimesUp && depth < Sim.Next.Length; depth++)
        {
            CurrentDepth = depth;
            bool swap;
            for (int s = 0; s < 2; s++)
            {
                swap = s == 1;
                // Get piece based on swap
                Piece current = swap ? Sim.Hold : Sim.Current, hold = swap ? Sim.Current : Sim.Hold;
                int nexti = 0;
                if (swap && Sim.Hold == Piece.EMPTY)
                {
                    current = Sim.Next[0];
                    nexti++;
                }

                for (int rot = 0; rot < 4 * Piece.ROTATION_CW; rot += Piece.ROTATION_CW)
                {
                    if (TimesUp) break;

                    Piece piece = current.PieceType | rot;

                    for (int orix = piece.MinX; orix <= piece.MaxX; orix++)
                    {
                        if (TimesUp) break;

                        Sim.Current = piece;
                        Sim.X = orix;
                        Sim.Y = 19;
                        // Hard drop
                        Sim.TryDrop(40);
                        int oriy = Sim.Y;
                        // Update screen
                        int new_combo = combo, new_b2b = b2b;
                        int[] clears = SearchScore(false, ref new_combo, ref new_b2b, out double garbage, out int cleared);
                        // Check if better
                        double newvalue = Search(depth, nexti + 1, Sim.Next[nexti], hold, false, new_combo, new_b2b, out1, garbage);
                        if (newvalue >= bestScore)
                        {
                            if (pathfind.PathFind(piece, orix, oriy, out List<Moves> new_bestMoves))
                            {
                                if (newvalue > bestScore || bestMoves.Contains(Moves.SoftDrop) || new_bestMoves.Count < bestMoves.Count)
                                {
                                    bestScore = newvalue;
                                    bestMoves = new_bestMoves;
                                }
                            }
                        }
                        Sim.Current = piece;
                        Sim.X = orix;
                        Sim.Y = oriy;
                        Sim.Unplace(clears, cleared);
                        // Only vertical pieces can be spun
                        if ((rot & Piece.ROTATION_CW) == Piece.ROTATION_CW && piece.PieceType != Piece.O)
                        {
                            for (int rotate_cw = 0; rotate_cw < 2; rotate_cw++)
                            {
                                Sim.Current = piece;
                                Sim.X = orix;
                                Sim.Y = oriy;
                                bool clockwise = rotate_cw == 0;

                                for (int i = 0; i < 2; i++)
                                {
                                    if (!Sim.TryRotate(clockwise ? 1 : -1)) break;
                                    if (!Sim.OnGround()) break;
                                    // Update screen
                                    Piece rotated = Sim.Current;
                                    int x = Sim.X, y = Sim.Y;
                                    new_combo = combo;
                                    new_b2b = b2b;
                                    clears = SearchScore(true, ref new_combo, ref new_b2b, out garbage, out cleared);
                                    // Check if better
                                    newvalue = Search(depth, nexti + 1, Sim.Next[nexti], hold, false, new_combo, new_b2b, out1, garbage);
                                    if (newvalue > bestScore)
                                    {
                                        if (pathfind.PathFind(rotated, x, y, out List<Moves> new_bestMoves))
                                        {
                                            bestScore = newvalue;
                                            bestMoves = new_bestMoves;
                                        }
                                    }
                                    // Revert screen
                                    Sim.Current = rotated;
                                    Sim.X = x;
                                    Sim.Y = y;
                                    Sim.Unplace(clears, cleared);

                                    // Only try to spin T pieces twice (for TSTs)
                                    if (piece.PieceType != Piece.T) break;
                                }
                            }
                        }
                    }
                    // O pieces can't be rotated
                    if (piece.PieceType == Piece.O)
                    {
                        MaxDepth += 1d / 2;
                        break;
                    }
                    else MaxDepth += (1d / 2) / 4;
                }

                Sim.Current = swap ? hold : current;
            }
        }
        //CachedValues.Clear();
        // Remove excess cache
        if (CachedValues.Count < 200000) CachedValues.Clear();
        if (CachedStateValues.Count < 5000000) CachedStateValues.Clear();

        // Check if PC found
        //
        // Adjust movetresh
        double time_remaining = (double)(ThinkTicks - Sw.ElapsedTicks) / ThinkTicks;
        if (time_remaining > 0)
            MoveTresh *= Math.Pow(MOVEMUL, time_remaining / ((1 + Math.E) / Math.E - time_remaining));
        else
            MoveTresh *= Math.Pow(MOVEMUL, MaxDepth - MOVETARGET);
        MoveTresh = Math.Min(Math.Max(MoveTresh, MINTRESH), MAXTRESH);

        return bestMoves;
    }

    double Search(int depth, int nexti, Piece current, Piece _hold, bool swapped, int comb, int b2b, double prevstate, double _trash)
    {
        //if (TimesUp || pc_found) return double.MinValue;
        if (TimesUp) return double.MinValue;
        if (Sw.ElapsedTicks > ThinkTicks)
        {
            TimesUp = true;
            return double.MinValue;
        }

        if (depth == 0 || nexti == NextHashTable.Length)
        {
            NodeCounts[0]++;

            ulong hash = BitConverter.DoubleToUInt64Bits(_trash);
            for (int x = 0; x < 10; x++)
                for (int y = 20; y < 40; y++)
                    if (Sim.Matrix[y][x] != Piece.EMPTY)
                        hash ^= MatrixHashTable[x][y];
            if (CachedStateValues.ContainsKey(hash))
                return CachedStateValues[hash];

            double[] feat = ExtrFeat(_trash);
            double val = Network.FeedFoward(feat)[0];
            CachedStateValues.Add(hash, val);
            return val;
        }
        else
        {
            double _value = double.MinValue;
            // Have we seen this situation before?
            ulong hash = HashBoard(current, _hold, nexti, depth);
            if (CachedValues.ContainsKey(hash)) return CachedValues[hash];
            // Check if this move should be explored
            double discount = Discounts[CurrentDepth - depth];
            double[] outs = Network.FeedFoward(ExtrFeat(_trash));
            if (outs[0] - prevstate < MoveTresh)
            {
                ulong statehash = BitConverter.DoubleToUInt64Bits(_trash);
                for (int x = 0; x < 10; x++)
                    for (int y = 20; y < 40; y++)
                        if (Sim.Matrix[y][x] != Piece.EMPTY)
                            statehash ^= MatrixHashTable[x][y];
                if (!CachedStateValues.ContainsKey(statehash))
                    CachedStateValues.Add(statehash, outs[0]);
                return outs[0];
            }
            // Check value for swap
            if (!swapped)
            {
                if (_hold.PieceType == Piece.EMPTY)
                    _value = Math.Max(_value, Search(depth, nexti + 1, Sim.Next[nexti], current, true, comb, b2b, prevstate, _trash));
                else
                    _value = Math.Max(_value, Search(depth, nexti, _hold, current, true, comb, b2b, prevstate, _trash));
                if (CachedValues.ContainsKey(hash))
                    return CachedValues[hash];
            }
            // Check all landing spots
            for (int rot = 0; rot < 4 * Piece.ROTATION_CW; rot += Piece.ROTATION_CW)
            {
                Piece piece = current | rot;
                for (int orix = piece.MinX; orix <= piece.MaxX; orix++)
                {
                    if (TimesUp) break;

                    Sim.X = orix;
                    Sim.Y = 19;
                    Sim.Current = piece;
                    Sim.TryDrop(40);
                    int oriy = Sim.Y;
                    int[] clears;
                    if ((piece.PieceType != Piece.S && piece.PieceType != Piece.Z) || rot < Piece.ROTATION_180)
                    {
                        // Update matrix
                        int new_comb = comb, new_b2b = b2b;
                        clears = SearchScore(false, ref new_comb, ref new_b2b, out double garbage, out int cleared);
                        // Check if better
                        _value = Math.Max(_value, Search(depth - 1, nexti + 1, Sim.Next[nexti], _hold, false, new_comb, new_b2b, outs[0], discount * garbage + _trash));
                        // Revert matrix
                        Sim.X = orix;
                        Sim.Y = oriy;
                        Sim.Current = piece;
                        Sim.Unplace(clears, cleared);
                    }
                    // Only vertical pieces can be spun
                    if ((rot & Piece.ROTATION_CW) == Piece.ROTATION_CW && piece.PieceType != Piece.O)
                    {
                        for (int rotate_cw = 0; rotate_cw < 2; rotate_cw++)
                        {
                            bool clockwise = rotate_cw == 0;
                            Sim.X = orix;
                            Sim.Y = oriy;
                            Sim.Current = piece;
                            // Try spin
                            for (int i = 0; i < 2; i++)
                            {
                                if (!Sim.TryRotate(clockwise ? 1 : -1)) break;
                                if (!Sim.OnGround()) break;
                                int x = Sim.X, y = Sim.Y;
                                Piece rotated = Sim.Current;
                                // Update matrix
                                int new_comb = comb, new_b2b = b2b;
                                clears = SearchScore(true, ref new_comb, ref new_b2b, out double garbage, out int cleared);
                                // Check if better
                                _value = Math.Max(_value, Search(depth - 1, nexti + 1, Sim.Next[nexti], _hold, false, new_comb, new_b2b, outs[0], discount * garbage + _trash));
                                // Revert matrix
                                Sim.X = x;
                                Sim.Y = y;
                                Sim.Current = rotated;
                                Sim.Unplace(clears, cleared);

                                // Only try to spin T pieces twice(for TSTs)
                                if (current.PieceType != Piece.T) break;
                            }
                        }
                    }
                }
                //o pieces can't be rotated
                if (current == 7) break;
            }

            CachedValues.Add(hash, _value);
            return _value;
        }
    }

    double[] ExtrFeat(double _trash)
    {
        // Find heightest block in each column
        double[] heights = new double[10];
        for (int x = 0; x < 10; x++)
        {
            double height = 20;
            for (int y = 20; Sim.Matrix[y][x] == 0; y++)
            {
                height--;
                if (y == 39) break;
            }
            heights[x] = height;
        }
        // Standard height
        double h = 0;
        if (Network.Visited[0])
        {
            foreach (double height in heights) h += height * height;
            h = Math.Sqrt(h);
        }
        // "caves"
        double caves = 0;
        if (Network.Visited[1])
        {
            for (int y = 39 - (int)heights[0]; y < 40; y++)
                if (Sim.Matrix[y][0] == 0 && Sim.Matrix[y - 1][0] != 0)
                    if (y < 39 - heights[1])
                        caves += heights[0] + y - 39;
            for (int x = 1; x < 9; x++)
                for (int y = 39 - (int)heights[x]; y < 40; y++)
                    if (Sim.Matrix[y][x] == 0 && Sim.Matrix[y - 1][x] != 0)
                        if (y <= Math.Min(39 - heights[x - 1], 39 - heights[x + 1]))
                            caves += heights[x] + y - 39;
            for (int y = 39 - (int)heights[9]; y < 40; y++)
                if (Sim.Matrix[y][9] == 0 && Sim.Matrix[y - 1][9] != 0) if (y <= 39 - heights[8])
                        caves += heights[9] + y - 39;
        }
        // Pillars
        double pillars = 0;
        if (Network.Visited[2])
        {
            for (int x = 0; x < 10; x++)
            {
                double diff;
                // Don't punish for tall towers at the side
                if (x != 0 && x != 9) diff = Math.Min(Math.Abs(heights[x - 1] - heights[x]), Math.Abs(heights[x + 1] - heights[x]));
                else diff = x == 0 ? Math.Max(0, heights[1] - heights[0]) : Math.Min(0, heights[8] - heights[9]);
                if (diff > 2) pillars += diff * diff;
                else pillars += diff;
            }
        }
        // Row trasitions
        double rowtrans = 0;
        if (Network.Visited[3])
        {
            for (int y = 19; y < 40; y++)
            {
                bool empty = Sim.Matrix[y][0] == 0;
                for (int x = 1; x < 10; x++)
                {
                    bool isempty = Sim.Matrix[y][x] == 0;
                    if (empty ^ isempty)
                    {
                        rowtrans++;
                        empty = isempty;
                    }
                }
            }
        }
        // Column trasitions
        double coltrans = 0;
        if (Network.Visited[4])
        {
            for (int x = 0; x < 10; x++)
            {
                bool empty = Sim.Matrix[19][x] == 0;
                for (int y = 20; y < 40; y++)
                {
                    bool isempty = Sim.Matrix[y][x] == 0;
                    if (empty ^ isempty)
                    {
                        coltrans++;
                        empty = isempty;
                    }
                }
            }
        }

        return new double[] { h, caves, pillars, rowtrans, coltrans, _trash };
    }

    ulong HashBoard(Piece piece, Piece _hold, int nexti, int depth)
    {
        ulong hash = PieceHashTable[piece.PieceType] ^ HoldHashTable[_hold.PieceType];
        for (int i = 0; nexti + i < NextHashTable.Length && i < depth; i++) hash ^= NextHashTable[i][Sim.Next[nexti + i]];
        for (int x = 0; x < 10; x++) for (int y = 20; y < 40; y++) if (Sim.Matrix[y][x] != Piece.EMPTY) hash ^= MatrixHashTable[x][y];
        return hash;
    }

    bool MatrixToUlong(int[][] matrix, out ulong ulong_matrix)
    {
        //pc_moves = null;
        ulong_matrix = 0;
        for (int y = 17; y < matrix.Length - 4; y++) for (int x = 0; x < matrix[0].Length; x++) if (matrix[y][x] != 0) return false;
        for (int i = 0; i < 40; i++)
        {
            if (matrix[(i / 10) + matrix.Length - 4][i % 10] != 0) ulong_matrix |= 1ul << (39 - i);
        }
        return true;
    }
}

public class NN
{
    private class Node
    {
        public List<Node> Network;
        public double Value = 0;
        public int Id;
        public List<Connection> Inputs, Outputs;
        readonly Func<double, double> Activation;

        public Node(int id, List<Node> network, Func<double, double> activationType, List<Connection> inputs = null, List<Connection> outputs = null)
        {
            Id = id;
            Network = network;
            Inputs = inputs ?? new List<Connection>();
            Outputs = outputs ?? new List<Connection>();
            Activation = activationType;
        }

        public double UpdateValue()
        {
            // sum activations * weights
            Value = 0;
            foreach (Connection c in Inputs)
                if (c.Enabled)
                    Value += Network[c.Input].Value * c.Weight;
            Value = Activation(Value);

            return Value;
        }
    }

    private class Connection
    {
        public bool Enabled;
        public int Input, Output, Id;
        public double Weight;

        public Connection(int input, int output, double weight, bool enabled = true)
        {
            Input = input;
            Output = output;
            Weight = weight;
            Enabled = enabled;
            // Assign the id according to the input and output nodes
            Id = -1;
            for (int i = 0; i < InNodes.Count && Id == -1; i++)
                if (InNodes[i] == Input && OutNodes[i] == Output)
                    Id = i;
            if (Id == -1)
            {
                Id = InNodes.Count;
                InNodes.Add(Input);
                OutNodes.Add(Output);
            }
        }

        public Connection Clone()
        {
            Connection clone = new Connection(Input, Output, Weight);
            if (!Enabled) clone.Enabled = false;
            return clone;
        }
    }

    public static readonly Func<double, double> DEFAULT_ACTIVATION = ReLU;

    static readonly Random Rand = new();
    private static readonly List<int> InNodes = new List<int>(), OutNodes = new List<int>();

    public int InputCount { get; private set; }
    public int OutputCount { get; private set; }
    private readonly List<Node> Nodes = new List<Node>();
    public bool[] Visited { get; private set; } = Array.Empty<bool>();
    private readonly List<int> ConnectionIds = new List<int>();
    private readonly Dictionary<int, Connection> Connections = new Dictionary<int, Connection>();

    private NN(int inputs, int outputs)
    {
        InputCount = inputs;
        OutputCount = outputs;
        // Make input nodes
        for (int i = 0; i < InputCount + 1; i++)
        {
            // Connect every input to every output
            List<Connection> outs = new();
            for (int j = 0; j < OutputCount; j++)
            {
                outs.Add(new(i, InputCount + j + 1, GaussRand()));
                Connections.Add(outs[j].Id, outs[j]);
                ConnectionIds.Add(outs[j].Id);
            }
            Nodes.Add(new(i, Nodes, DEFAULT_ACTIVATION, outputs: new(outs)));
        }
        // Make output nodes
        for (int i = 0; i < OutputCount; i++)
        {
            List<Connection> ins = new();
            for (int j = 0; j < InputCount + 1; j++)
                ins.Add(Connections[ConnectionIds[OutputCount * j + i]]);
            Nodes.Add(new(InputCount + i + 1, Nodes, DEFAULT_ACTIVATION, inputs: new List<Connection>(ins)));
        }
        // Find all connected nodes
        FindConnectedNodes();
    }

    private NN(int inputs, int outputs, List<Connection> connections)
    {
        InputCount = inputs; // Add 1 for bias
        OutputCount = outputs;
        foreach (Connection c in connections)
        {
            // Add connection to connection tracking lists
            Connection newc = c.Clone();
            ConnectionIds.Add(newc.Id);
            Connections.Add(newc.Id, newc);
            // Add nodes as nescessary
            while (Nodes.Count <= newc.Input || Nodes.Count <= newc.Output)
                Nodes.Add(new Node(Nodes.Count, Nodes, DEFAULT_ACTIVATION));
            // Add connection to coresponding nodes
            Nodes[c.Input].Outputs.Add(newc);
            Nodes[c.Output].Inputs.Add(newc);
        }
        // Find all connected nodes
        FindConnectedNodes();

    }

    public double[] FeedFoward(double[] input)
    {
        // Set input nodes
        for (int i = 0; i < InputCount; i++)
            if (Visited[i])
                Nodes[i].Value = input[i];
        Nodes[InputCount].Value = 1; // Bias node
        // Update hidden all nodes
        for (int i = InputCount + OutputCount + 1; i < Nodes.Count; i++)
            if (Visited[i]) Nodes[i].UpdateValue();
        // Update ouput nodes and get output
        double[] output = new double[OutputCount];
        for (int i = 0; i < OutputCount; i++) output[i] = Nodes[i + InputCount + 1].UpdateValue();

        return output;
    }

    private void FindConnectedNodes()
    {
        Visited = new bool[Nodes.Count];
        for (int i = InputCount + 1; i < InputCount + OutputCount + 1; i++)
            if (!Visited[i])
                VisitDownstream(i);

        void VisitDownstream(int i)
        {
            Visited[i] = true;
            foreach (Connection c in Nodes[i].Inputs)
                if (!Visited[c.Input] && c.Enabled)
                    VisitDownstream(c.Input);
        }
    }

    public int GetSize() => Visited.Count(x => x == true);

    public NN Clone() => new NN(InputCount, OutputCount, new List<Connection>(Connections.Values));

    public static NN LoadNN(string path)
    {
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string[] inout = lines[1].Split(' ');
        List<Connection> cons = new List<Connection>();
        for (int i = 2; i < lines.Length; i++)
        {
            string[] split = lines[i].Split(' ');
            if (split.Length != 4)
                cons.Add(new Connection(Convert.ToInt32(split[0]), Convert.ToInt32(split[1]), Convert.ToDouble(split[2])));
        }

        return new NN(Convert.ToInt32(inout[0]), Convert.ToInt32(inout[1]), cons);
    }

    static double GaussRand()
    {
        double output = 0;
        for (int i = 0; i < 6; i++) output += Rand.NextDouble();
        return (output - 3) / 3;
    }
    static double UniformRand() => Math.FusedMultiplyAdd(Rand.NextDouble(), 2, -1);
    #region // Activation functions
    static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
    static double TanH(double x) => Math.Tanh(x);
    static double ReLU(double x) => x >= 0 ? x : 0;
    static double LeakyReLU(double x) => x >= 0 ? x : 0.01 * x;
    static double ELU(double x) => x >= 0 ? x : Math.Exp(x) - 1;
    static double SELU(double x) => 1.050700987355480 * (x >= 0 ? x : 1.670086994173469 * (Math.Exp(x) - 1));
    static double SoftPlus(double x) => Math.Log(1 + Math.Exp(x));
    #endregion
}
