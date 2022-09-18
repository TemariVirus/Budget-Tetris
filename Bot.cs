using Tetris;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Documents;

class Bot
{
    const double THINKTIMEINSECONDS = 0.2,
                 MINTRESH = -1, MAXTRESH = -0.01, MOVEMUL = 1.05, MOVETARGET = 5,
                 DISCOUNT_FACTOR = 0.95;

    public Game Game;
    private GameBase Sandbox;
    bool Done;

    private readonly NN Network;
    bool UsePCFinder = true;
    long ThinkTicks = (long)(THINKTIMEINSECONDS * Stopwatch.Frequency);
    double MoveTresh = -0.1;

    int CurrentDepth;
    double MaxDepth;
    
    readonly Dictionary<ulong, double> CachedValues = new Dictionary<ulong, double>();
    readonly Dictionary<ulong, double> CachedStateValues = new Dictionary<ulong, double>();
    readonly ulong[][] MatrixHashTable = RandomArray(10, 40), NextHashTable;
    readonly ulong[] PieceHashTable = RandomArray(8), HoldHashTable = RandomArray(8);
    readonly double[] Discounts;

    readonly Stopwatch Timer = new Stopwatch();
    bool TimesUp;


    const int RunAvgCount = 20;
    long[] NCount = new long[RunAvgCount];

    public Bot(string filePath, Game game)
    {
        Network = NN.LoadNN(filePath);
        Game = game;

        NextHashTable = RandomArray(game.Next.Length, 8);

        Discounts = new double[game.Next.Length];
        for (int i = 0; i < game.Next.Length; i++) Discounts[i] = Math.Pow(DISCOUNT_FACTOR, i);

        CachedValues.EnsureCapacity(200000);
        CachedStateValues.EnsureCapacity(5000000);
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

    public void Start(int ThinkDelay, int MoveDelay)
    {
        Game.TickingCallback = () =>
        {
            Done = true;
        };
        Thread main = new Thread(() =>
        {
            while (!Game.IsDead)
            {
                List<Moves> moves = FindMoves();
                
                // Think Delay
                Thread.Sleep(ThinkDelay);
                while (moves.Count != 0)
                {
                    Game.Play(moves[0]);
                    moves.RemoveAt(0);
                    // Move delay
                    Thread.Sleep(MoveDelay);
                }
                Done = false;
                while (!Done) Thread.Sleep(0);
                Game.WriteAt(1, 0, ConsoleColor.White, Math.Round(NCount.Average()).ToString().PadRight(9));
                Game.WriteAt(1, 22, ConsoleColor.White, MaxDepth.ToString().PadRight(6));
                Game.WriteAt(1, 23, ConsoleColor.White, MoveTresh.ToString().PadRight(12));
                for (int i = NCount.Length - 1; i > 0; i--)
                    NCount[i] = NCount[i - 1];
                NCount[0] = 0;
            }
        });
        main.Priority = ThreadPriority.Highest;
        main.Start();
    }

    int[] SearchScore(bool lastrot, ref int comb, ref int _b2b, out double _trash, out int cleared)
    {
        // { scoreadd, B2B, T - spin, combo, clears } //first bit of B2B = B2B chain status
        // Check for t - spins
        int tspin = Sandbox.TSpin(lastrot);
        // Clear lines
        int[] clears = Sandbox.Place(out cleared);
        // Combo
        comb = cleared == 0 ? -1 : comb + 1;
        // Perfect clear
        bool pc = true;
        for (int x = 0; x < 10; x++) if (Sandbox.Matrix[39][x] != 0) pc = false;
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
        // Remove excess cache
        if (CachedValues.Count < 200000) CachedValues.Clear();
        if (CachedStateValues.Count < 5000000) CachedStateValues.Clear();

        // Get copy of attached game
        Sandbox = Game.Clone();
        GameBase pathfind = Game.Clone();
        
        // Call the dedicated PC Finder
        //

        Timer.Restart();
        TimesUp = false;

        List<Moves> bestMoves = new List<Moves>();
        double bestScore = double.MinValue;

        // Keep searching until time's up or no next pieces left
        MaxDepth = 0;
        int combo = Game.Combo, b2b = Game.B2B;
        double out1 = Network.FeedFoward(ExtrFeat(0))[0];
        for (int depth = 0; !TimesUp && depth < Sandbox.Next.Length; depth++)
        {
            CurrentDepth = depth;
            bool swap;
            for (int s = 0; s < 2; s++)
            {
                swap = s == 1;
                // Get piece based on swap
                Piece current = swap ? Sandbox.Hold : Sandbox.Current, hold = swap ? Sandbox.Current : Sandbox.Hold;
                int nexti = 0;
                if (swap && Sandbox.Hold == Piece.EMPTY)
                {
                    current = Sandbox.Next[0];
                    nexti++;
                }

                for (int rot = 0; rot < 4 * Piece.ROTATION_CW; rot += Piece.ROTATION_CW)
                {
                    if (TimesUp) break;

                    Piece piece = current.PieceType | rot;

                    for (int orix = piece.MinX; orix <= piece.MaxX; orix++)
                    {
                        if (TimesUp) break;

                        Sandbox.Current = piece;
                        Sandbox.X = orix;
                        Sandbox.Y = 19;
                        // Hard drop
                        Sandbox.TryDrop(40);
                        int oriy = Sandbox.Y;
                        // Update screen
                        int new_combo = combo, new_b2b = b2b;
                        int[] clears = SearchScore(false, ref new_combo, ref new_b2b, out double garbage, out int cleared);
                        // Check if better
                        double newvalue = Search(depth, nexti + 1, Sandbox.Next[nexti], hold, false, new_combo, new_b2b, out1, garbage);
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
                        Sandbox.Current = piece;
                        Sandbox.X = orix;
                        Sandbox.Y = oriy;
                        Sandbox.Unplace(clears, cleared);
                        // Only vertical pieces can be spun
                        if ((rot & Piece.ROTATION_CW) == Piece.ROTATION_CW && piece.PieceType != Piece.O)
                        {
                            for (int rotate_cw = 0; rotate_cw < 2; rotate_cw++)
                            {
                                Sandbox.Current = piece;
                                Sandbox.X = orix;
                                Sandbox.Y = oriy;
                                bool clockwise = rotate_cw == 0;

                                for (int i = 0; i < 2; i++)
                                {
                                    if (!Sandbox.TryRotate(clockwise ? 1 : -1)) break;
                                    if (!Sandbox.OnGround()) break;
                                    // Update screen
                                    Piece rotated = Sandbox.Current;
                                    int x = Sandbox.X, y = Sandbox.Y;
                                    new_combo = combo;
                                    new_b2b = b2b;
                                    clears = SearchScore(true, ref new_combo, ref new_b2b, out garbage, out cleared);
                                    // Check if better
                                    newvalue = Search(depth, nexti + 1, Sandbox.Next[nexti], hold, false, new_combo, new_b2b, out1, garbage);
                                    if (newvalue > bestScore)
                                    {
                                        if (pathfind.PathFind(rotated, x, y, out List<Moves> new_bestMoves))
                                        {
                                            bestScore = newvalue;
                                            bestMoves = new_bestMoves;
                                        }
                                    }
                                    // Revert screen
                                    Sandbox.Current = rotated;
                                    Sandbox.X = x;
                                    Sandbox.Y = y;
                                    Sandbox.Unplace(clears, cleared);

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

                Sandbox.Current = swap ? hold : current;
            }
        }
        CachedValues.Clear();
        Timer.Stop();

        // Check if pc found
        //
        // Adjust movetresh
        double time_remaining = (double)(ThinkTicks - Timer.ElapsedTicks) / ThinkTicks;
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
        if (Timer.ElapsedTicks > ThinkTicks)
        {
            TimesUp = true;
            return double.MinValue;
        }

        if (depth == 0 || nexti == NextHashTable.Length)
        {
            NCount[0]++;

            ulong hash = BitConverter.DoubleToUInt64Bits(_trash);
            for (int x = 0; x < 10; x++)
                for (int y = 20; y < 40; y++)
                    if (Sandbox.Matrix[y][x] != Piece.EMPTY)
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
                        if (Sandbox.Matrix[y][x] != Piece.EMPTY)
                            statehash ^= MatrixHashTable[x][y];
                if (!CachedStateValues.ContainsKey(statehash))
                    CachedStateValues.Add(statehash, outs[0]);
                return outs[0];
            }
            // Check value for swap
            if (!swapped)
            {
                if (_hold.PieceType == Piece.EMPTY)
                    _value = Math.Max(_value, Search(depth, nexti + 1, Sandbox.Next[nexti], current, true, comb, b2b, prevstate, _trash));
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

                    Sandbox.X = orix;
                    Sandbox.Y = 19;
                    Sandbox.Current = piece;
                    Sandbox.TryDrop(40);
                    int oriy = Sandbox.Y;
                    int[] clears;
                    if ((piece.PieceType != Piece.S && piece.PieceType != Piece.Z) || rot < Piece.ROTATION_180)
                    {
                        // Update matrix
                        int new_comb = comb, new_b2b = b2b;
                        clears = SearchScore(false, ref new_comb, ref new_b2b, out double garbage, out int cleared);
                        // Check if better
                        _value = Math.Max(_value, Search(depth - 1, nexti + 1, Sandbox.Next[nexti], _hold, false, new_comb, new_b2b, outs[0], discount * garbage + _trash));
                        // Revert matrix
                        Sandbox.X = orix;
                        Sandbox.Y = oriy;
                        Sandbox.Current = piece;
                        Sandbox.Unplace(clears, cleared);
                    }
                    // Only vertical pieces can be spun
                    if ((rot & Piece.ROTATION_CW) == Piece.ROTATION_CW && piece.PieceType != Piece.O)
                    {
                        for (int rotate_cw = 0; rotate_cw < 2; rotate_cw++)
                        {
                            bool clockwise = rotate_cw == 0;
                            Sandbox.X = orix;
                            Sandbox.Y = oriy;
                            Sandbox.Current = piece;
                            // Try spin
                            for (int i = 0; i < 2; i++)
                            {
                                if (!Sandbox.TryRotate(clockwise ? 1 : -1)) break;
                                if (!Sandbox.OnGround()) break;
                                int x = Sandbox.X, y = Sandbox.Y;
                                Piece rotated = Sandbox.Current;
                                // Update matrix
                                int new_comb = comb, new_b2b = b2b;
                                clears = SearchScore(true, ref new_comb, ref new_b2b, out double garbage, out int cleared);
                                // Check if better
                                _value = Math.Max(_value, Search(depth - 1, nexti + 1, Sandbox.Next[nexti], _hold, false, new_comb, new_b2b, outs[0], discount * garbage + _trash));
                                // Revert matrix
                                Sandbox.X = x;
                                Sandbox.Y = y;
                                Sandbox.Current = rotated;
                                Sandbox.Unplace(clears, cleared);

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

    int DistanceFromWall(Piece piece, List<Moves> _moves)
    {
        int _x = 4;
        foreach (Moves move in _moves)
        {
            if (move == Moves.Left) _x--;
            else if (move == Moves.Right) _x++;
            else if (move == Moves.RotateCW) piece += Piece.ROTATION_CW;
            else if (move == Moves.RotateCCW) piece += Piece.ROTATION_CCW;
            else if (move == Moves.SoftDrop) break;
        }
        return Math.Min(_x - piece.MinX, piece.MaxX - _x);
    }

    double[] ExtrFeat(double _trash)
    {
        // Find heightest block in each column
        double[] heights = new double[10];
        for (int x = 0; x < 10; x++)
        {
            double height = 20;
            for (int y = 20; Sandbox.Matrix[y][x] == 0; y++)
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
                if (Sandbox.Matrix[y][0] == 0 && Sandbox.Matrix[y - 1][0] != 0)
                    if (y < 39 - heights[1])
                        caves += heights[0] + y - 39;
            for (int x = 1; x < 9; x++)
                for (int y = 39 - (int)heights[x]; y < 40; y++)
                    if (Sandbox.Matrix[y][x] == 0 && Sandbox.Matrix[y - 1][x] != 0)
                        if (y <= Math.Min(39 - heights[x - 1], 39 - heights[x + 1]))
                            caves += heights[x] + y - 39;
            for (int y = 39 - (int)heights[9]; y < 40; y++)
                if (Sandbox.Matrix[y][9] == 0 && Sandbox.Matrix[y - 1][9] != 0) if (y <= 39 - heights[8])
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
                bool empty = Sandbox.Matrix[y][0] == 0;
                for (int x = 1; x < 10; x++)
                {
                    bool isempty = Sandbox.Matrix[y][x] == 0;
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
                bool empty = Sandbox.Matrix[19][x] == 0;
                for (int y = 20; y < 40; y++)
                {
                    bool isempty = Sandbox.Matrix[y][x] == 0;
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
        for (int i = 0; nexti + i < NextHashTable.Length && i < depth; i++) hash ^= NextHashTable[i][Sandbox.Next[nexti + i]];
        for (int x = 0; x < 10; x++) for (int y = 20; y < 40; y++) if (Sandbox.Matrix[y][x] != Piece.EMPTY) hash ^= MatrixHashTable[x][y];
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

class NN
{
    internal class Node
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

    internal class Connection
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

    public int InputCount { get; private set; }
    public int OutputCount { get; private set; }
    private static readonly List<int> InNodes = new List<int>(), OutNodes = new List<int>();
    readonly List<Node> Nodes = new List<Node>();
    public bool[] Visited = Array.Empty<bool>();
    readonly List<int> ConnectionIds = new List<int>();
    readonly Dictionary<int, Connection> Connections = new Dictionary<int, Connection>();

    NN(int inputs, int outputs, List<Connection> connections)
    {
        InputCount = inputs + 1; // Add 1 for bias
        OutputCount = outputs;
        foreach (Connection c in connections)
        {
            // Add connection to connection tracking lists
            Connection newc = c.Clone();
            ConnectionIds.Add(newc.Id);
            Connections.Add(newc.Id, newc);
            // Add nodes as nescessary
            while (Nodes.Count <= newc.Input || Nodes.Count <= newc.Output)
                Nodes.Add(new Node(Nodes.Count, Nodes, ReLU));
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
        for (int i = 0; i < InputCount - 1; i++)
            if (Visited[i])
                Nodes[i].Value = input[i];
        Nodes[InputCount - 1].Value = 1; // Bias node
        // Update hidden all nodes
        for (int i = InputCount + OutputCount; i < Nodes.Count; i++)
            if (Visited[i])
                Nodes[i].UpdateValue();
        // Update ouput nodes and get output
        double[] output = new double[OutputCount];
        for (int i = 0; i < OutputCount; i++) output[i] = Nodes[i + InputCount].UpdateValue();

        return output;
    }

    void FindConnectedNodes()
    {
        Visited = new bool[Nodes.Count];
        for (int i = InputCount; i < InputCount + OutputCount; i++)
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
