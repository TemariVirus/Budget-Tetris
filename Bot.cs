namespace TetrisAI;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
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
    double MoveTresh = -0.05;

    int CurrentDepth;
    double MaxDepth;

    readonly Dictionary<ulong, double> CachedValues = new Dictionary<ulong, double>();
    readonly Dictionary<ulong, double> CachedStateValues = new Dictionary<ulong, double>();
    readonly ulong[][] MatrixHashTable = RandomArray(10, 40), NextHashTable;
    readonly ulong[] PieceHashTable = RandomArray(8), HoldHashTable = RandomArray(8);
    readonly double[] Discounts;

    readonly Stopwatch Sw = new Stopwatch();
    bool TimesUp;

    private bool ToStop;
    public Thread BotThread { get; private set; }
    const int RunAvgCount = 20;
    long[] NodeCounts;

    private Bot(Game game)
    {
        Game = game;

        NextHashTable = RandomArray(game.Next.Length, 8);

        Discounts = new double[game.Next.Length];
        for (int i = 0; i < game.Next.Length; i++) Discounts[i] = Math.Pow(DISCOUNT_FACTOR, i);

        //CachedValues.EnsureCapacity(200000);
        //CachedStateValues.EnsureCapacity(5000000);
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
        if (BotThread != null)
            if (BotThread.IsAlive)
                return;

        ThinkTicks = think_time * Stopwatch.Frequency / 1000;
        NodeCounts = new long[RunAvgCount];
        ToStop = false;
        Game.TickingCallback = () =>
        {
            Done = true;
        };
        BotThread = new Thread(() =>
        {
            while (!Game.IsDead)
            {
                // Find moves
                List<Moves> moves = FindMoves();
                // Wait out excess think time
                while (Sw.ElapsedTicks < ThinkTicks) Thread.Sleep(0);
                // Play moves
                foreach (Moves move in moves)
                {
                    Game.Play(move);
                    Thread.Sleep(move_delay);
                }
                Done = false;
                while (!Done && !ToStop) Thread.Sleep(0);
                // Stats
                Game.WriteAt(0, 23, ConsoleColor.White, $"Depth: {MaxDepth}".PadRight(Game.GAMEWIDTH));
                long count = NodeCounts.Aggregate(0, (aggregate, next) => (next == 0) ? aggregate : aggregate + 1);
                if (count == 0) count++;
                Game.WriteAt(0, 24, ConsoleColor.White, $"Nodes: {NodeCounts.Sum() / count}".PadRight(Game.GAMEWIDTH));
                Game.WriteAt(0, 25, ConsoleColor.White, $"Tresh: {Math.Round(MoveTresh, 6)}".PadRight(Game.GAMEWIDTH));
                for (int i = NodeCounts.Length - 1; i > 0; i--)
                    NodeCounts[i] = NodeCounts[i - 1];
                NodeCounts[0] = 0;
                // Check if should stop
                if (ToStop) break;
            }

            ToStop = false;
            return;
        });
        BotThread.Priority = ThreadPriority.Highest;
        BotThread.Start();

        // Write stats to prevent flashing
        Game.WriteAt(0, 23, ConsoleColor.White, $"Depth: 0".PadRight(Game.GAMEWIDTH));
        Game.WriteAt(0, 24, ConsoleColor.White, $"Nodes: 0".PadRight(Game.GAMEWIDTH));
        Game.WriteAt(0, 25, ConsoleColor.White, $"Tresh: 0.000000".PadRight(Game.GAMEWIDTH));
    }

    public void Stop()
    {
        ToStop = true;
        BotThread.Join();
    }

    int[] SearchScore(bool last_rot, ref int combo, ref int b2b, out double trash, out int cleared)
    {
        // { scoreadd, B2B, T - spin, combo, clears } //first bit of B2B = B2B chain status
        // Check for t - spins
        int tspin = Sim.TSpinType(last_rot);
        // Clear lines
        int[] clears = Sim.Place(out cleared);
        // Combo
        combo = cleared == 0 ? -1 : combo + 1;
        // Perfect clear
        bool pc = true;
        for (int x = 0; x < 10; x++) if (Sim.Matrix[39][x] != 0) pc = false;
        // Compute score
        if (tspin == 0 && cleared != 4 && cleared != 0) b2b = -1;
        else if (tspin + cleared > 3) b2b++;

        ////_trash = new double[] { 0, 0, 1, 2, 4 }[cleared];
        //if (pc) _trash = 5;
        //else _trash = new double[] { 0, 0, 1, 1.5, 5 }[cleared];
        ////if (info[2] == 3) _trash += new double[] { 0, 2, 3, 4 }[cleared];
        //if (tspin == 3) _trash += new double[] { 0, 2, 5, 8.5 }[cleared];
        //if ((tspin + cleared > 3) && b2b > -1) _trash++;
        ////if (pc) _trash += 4;
        ////if (comb > 0) _trash += new double[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 9)];
        //if (comb0 > 0) _trash += new double[] { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb0 - 1, 11)]; //jstris combo table
        //// Modify _trash(use trash sent * APL and offset it slightly)
        ////if (cleared != 0 && comb > 1) _trash = Math.FusedMultiplyAdd(_trash, _trash / cleared, -1.5);

        trash = pc ? Game.PCTrash[cleared] :
                tspin == 3 ? Game.TSpinTrash[cleared] :
                             Game.LinesTrash[cleared];
        if (combo > 0) trash += Game.ComboTrash[Math.Min(combo, Game.ComboTrash.Length) - 1];
        if ((tspin + cleared > 3) && b2b > 0) trash++;

        return clears;
    }

    List<Moves> FindMoves()
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
        public readonly int Id;
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
        public readonly int Id;
        public readonly int Input, Output;
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

        public Connection(ConnectionData data) : this(data.Input, data.Output, data.Weight, data.Enabled)
        {
            
        }

        public Connection Clone()
        {
            Connection clone = new Connection(Input, Output, Weight);
            if (!Enabled) clone.Enabled = false;
            return clone;
        }
    }

    public struct TrainingData
    {
        public int Gen;
        public double CompatTresh;
        public NNData[] NNData;
    }
    
    public struct NNData
    {
        public string Name;
        public bool Played;
        public int Inputs;
        public int Outputs;
        public double Fitness;
        public double Mu, Delta;
        public List<ConnectionData> Connections;

        public NNData(NN network)
        {
            Name = network.Name;
            Played = network.Played;
            Inputs = network.InputCount;
            Outputs = network.OutputCount;
            Fitness = network.Fitness;
            Mu = network.Mu;
            Delta = network.Delta;
            Connections = new List<ConnectionData>();
            foreach (Connection c in network.Connections.Values)
                Connections.Add(new ConnectionData
                {
                    Enabled = c.Enabled,
                    Input = c.Input,
                    Output = c.Output,
                    Weight = c.Weight
                });
        }
    }

    public struct ConnectionData
    {
        public bool Enabled;
        public int Input, Output;
        public double Weight;
    }

    #region // Hyperparameters
    // Activation
    public static readonly Func<double, double> DEFAULT_ACTIVATION = ReLU;
    // Crossover & speciation
    public const int SPECIES_TARGET = 5, SPECIES_TRY_TIMES = 10;
    public const double COMPAT_MOD = 1.1;
    public const double WEIGHT_DIFF_COE = 1, EXCESS_COE = 2;
    public const double ELITE_PERCENT = 0.4; //0.3
    // Population
    public const double FITNESS_TARGET = 2500;
    public const int MAX_GENERATIONS = -1; // Leave maxgen as -1 for unlimited
    // Mutation
    public const bool BOUND_WEIGHTS = false, ALLOW_RECURSIVE_CON = false;
    public const double MUTATE_POW = 2; //MUTATE_POW = 3
    public const double CON_PURTURB_CHANCE = 0.8, CON_ENABLE_CHANCE = 0.1, CON_ADD_CHANCE = 0.1, NODE_ADD_CHANCE = 0.02;
    public const int TRY_ADD_CON_TIMES = 5;
    #endregion

    static readonly Random Rand = new();
    private static readonly List<int> InNodes = new List<int>(), OutNodes = new List<int>();

    public string Name = GenerateName();
    public bool Played = false;
    public int InputCount { get; private set; }
    public int OutputCount { get; private set; }
    public double Fitness = 0;
    public double Mu, Delta;
    private readonly List<Node> Nodes = new List<Node>();
    public bool[] Visited { get; private set; }
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

    private NN(NNData data) : this(data.Inputs, data.Outputs, data.Connections.Select(x => new Connection(x)).ToList())
    {
        Name = data.Name;
        Played = data.Played;
        Fitness = data.Fitness;
        Mu = data.Mu;
        Delta = data.Delta;
    }

    private static string GenerateName()
    {
        string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x" };
        string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };

        string name = "";
        int len = Rand.Next(8) + 4;
        while (name.Length < len)
        {
            name += consonants[Rand.Next(consonants.Length)];
            name += vowels[Rand.Next(vowels.Length)];
        }

        //name = (name[0] + 32) + name.Substring(1);
        Span<char> name_span = name.ToCharArray();
        name_span[0] = (char)(name_span[0] - 32);
        
        return name_span.ToString();
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

    public NN MateWith(NN other)
    {
        // Other NN should be fitter
        if (this.Fitness > other.Fitness)
            return other.MateWith(this);

        // Clone
        NN child = other.Clone();
        // Corss
        foreach (int c in child.Connections.Keys)
        {
            if (Connections.ContainsKey(c))
            {
                // 50% of the time change weights
                if (Rand.NextDouble() < 0.5) child.Connections[c].Weight = Connections[c].Weight;
                // 10% of the time make enabled same as less fit one
                if (Rand.NextDouble() < 0.1) child.Connections[c].Enabled = Connections[c].Enabled;
            }
        }
        // Mutate
        child.Mutate();
        return child;
    }

    public NN Mutate()
    {
        // Mutate connections
        foreach (Connection c in Connections.Values)
        {
            // Mutate the weight
            if (Rand.NextDouble() < CON_PURTURB_CHANCE)
            {
                if (Rand.NextDouble() < 0.1) c.Weight = GaussRand();                           // 10% chance to completely change weight
                else if (Rand.NextDouble() < 0.5) c.Weight += UniformRand() / 25 * MUTATE_POW; // 45% chance to slightly change weight
                else c.Weight *= 1 + (UniformRand() / 40 * MUTATE_POW);                        // 45% chance to slightly scale weight
            }
            // Enable/disable connection
            if (Rand.NextDouble() < CON_ENABLE_CHANCE) c.Enabled = !c.Enabled;
            // Keep weight within bounds
            if (BOUND_WEIGHTS) c.Weight = c.Weight < 0 ? Math.Max(c.Weight, -MUTATE_POW * 2) : Math.Min(c.Weight, MUTATE_POW * 2);
        }

        // Add connection between existing nodes
        if (Rand.NextDouble() < CON_ADD_CHANCE)
        {
            for (int i = 0; i < TRY_ADD_CON_TIMES; i++)
            {
                int inid = Rand.Next(Nodes.Count), outid = Rand.Next(Nodes.Count);
                if (outid == inid) outid = (outid + 1) % Nodes.Count;
                Connection newcon = new Connection(inid, outid, GaussRand()), reversecon = new Connection(outid, inid, 0);
                if (!Connections.ContainsKey(newcon.Id) && (ALLOW_RECURSIVE_CON || !Connections.ContainsKey(reversecon.Id)))
                {
                    Nodes[inid].Outputs.Add(newcon);
                    Nodes[outid].Inputs.Add(newcon);
                    Connections.Add(newcon.Id, newcon);
                    ConnectionIds.Add(newcon.Id);
                    break;
                }
            }
        }

        // Add node between onto existing connection
        if (Rand.NextDouble() < NODE_ADD_CHANCE)
        {
            // Original connection to split
            Connection breakcon = Connections[ConnectionIds[Rand.Next(Connections.Count)]];
            for (int i = 0; i < TRY_ADD_CON_TIMES - 1 && !breakcon.Enabled; i++) breakcon = Connections[ConnectionIds[Rand.Next(Connections.Count)]];
            // Disable original connection
            breakcon.Enabled = false;
            // Insert node inbetween
            Connection incon = new Connection(breakcon.Input, Nodes.Count, breakcon.Weight), outcon = new Connection(Nodes.Count, breakcon.Output, 1);
            Connections.Add(incon.Id, incon);
            Connections.Add(outcon.Id, outcon);
            ConnectionIds.Add(incon.Id);
            ConnectionIds.Add(outcon.Id);
            Nodes[breakcon.Input].Outputs.Add(incon);
            Nodes[breakcon.Output].Inputs.Add(outcon);
            Nodes.Add(new Node(Nodes.Count, Nodes, DEFAULT_ACTIVATION, new List<Connection>() { incon }, new List<Connection>() { outcon }));
        }

        // Find all connected nodes
        FindConnectedNodes();

        return this;
    }

    public bool SameSpecies(NN other, double compat_tresh)
    {
        int matching = 0, large_genome_norm = Math.Max(1, Math.Max(Connections.Count, other.Connections.Count) - 20);
        double weight_diff = 0;
        // Go through each connection and see if it is excess or matching
        foreach (int conid in Connections.Keys)
        {
            if (other.Connections.ContainsKey(conid))
            {
                double weight = Connections[conid].Enabled ? Connections[conid].Weight : 0, other_weight = other.Connections[conid].Enabled ? other.Connections[conid].Weight : 0;
                weight_diff += Math.Abs(weight - other_weight);
                matching++;
            }
        }
        // Return whether or not they're the same species
        if (matching == 0) return EXCESS_COE * (Connections.Count + other.Connections.Count - 2 * matching) / large_genome_norm < compat_tresh;
        else return (WEIGHT_DIFF_COE * weight_diff / matching) + (EXCESS_COE * (Connections.Count + other.Connections.Count - 2 * matching) / large_genome_norm) < compat_tresh;
    }

    public int GetSize() => Visited.Count(x => x == true);

    public NN Clone() => new NN(InputCount, OutputCount, new List<Connection>(Connections.Values));

    public static void SaveNN(string path, NN network)
    {
        NNData data = new NNData(network);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json, Encoding.UTF8);
        return;
    }

    public static void SaveNNs(string path, NN[] networks, int gen, double compat_tresh)
    {
        NNData[] networks_data = networks.Select(x => new NNData(x)).ToArray();
        TrainingData training_data = new TrainingData
        {
            Gen = gen,
            CompatTresh = compat_tresh,
            NNData = networks_data
        };
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        string json = JsonSerializer.Serialize(training_data, options);
        File.WriteAllText(path, json, Encoding.UTF8); 
        return;
    }

    public static NN LoadNN(string path)
    {
        string jsonString = File.ReadAllText(path);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        NNData data = JsonSerializer.Deserialize<NNData>(jsonString, options);

        return new NN(data);
    }

    public static NN[] LoadNNs(string path, out int gen, out double compat_tresh)
    {
        string jsonString = File.ReadAllText(path);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        TrainingData training_data = JsonSerializer.Deserialize<TrainingData>(jsonString, options)!;
        gen = training_data.Gen;
        compat_tresh = training_data.CompatTresh;
        NN[] networks = training_data.NNData.Select(x => new NN(x)).ToArray();

        return networks;
    }

    public static NN[] Train(string path, Action<NN[], int, double> fitness_func, int inputs, int outputs, int pop_size)
    {
        string save_path = path.Insert(path.LastIndexOf('.'), " save");

        // Initialise/load NNs
        int gen = 0;
        double compat_tresh = 0.5;
        NN[] NNs = new NN[pop_size];
        if (File.Exists(path))
        {
            NNs = LoadNNs(path, out gen, out compat_tresh);
            // Add extra NNs if too few
            if (NNs.Length < pop_size)
            {
                var extra = new NN[pop_size - NNs.Length].Select(x => new NN(inputs, outputs));
                NNs = NNs.Concat(extra).ToArray();
            }
            else pop_size = NNs.Length;
        }
        else
        {
            for (int i = 0; i < pop_size; i++)
                NNs[i] = new NN(inputs, outputs);
            SaveNNs(path, NNs, gen, compat_tresh);
        }

        // Train
        for (; gen < MAX_GENERATIONS || MAX_GENERATIONS == -1; gen++)
        {
            // Update fitness
            fitness_func(NNs, gen, compat_tresh);
            NNs = NNs.OrderByDescending(x => x.Fitness).ToArray();
            if (NNs[^1].Fitness < 0) throw new Exception("Fitness must be not less than 0!");

            // Speciate
            List<List<NN>> species = Speciate(NNs, compat_tresh);
            for (int i = 0; i < SPECIES_TRY_TIMES; i++)
            {
                // Adjust compattresh
                if (species.Count < SPECIES_TARGET) compat_tresh /= Math.Pow(COMPAT_MOD, 1 - (double)i / SPECIES_TRY_TIMES);
                else if (species.Count > SPECIES_TARGET) compat_tresh *= Math.Pow(COMPAT_MOD, 1 - (double)i / SPECIES_TRY_TIMES);
                else break;
                species = Speciate(NNs, compat_tresh);
            }
            species = species.OrderByDescending(s => s.Average(x => x.Fitness)).ToList();

            // Explicit fitness sharing
            // Find average fitnesses
            double avg_fit = NNs.Average(x => x.Fitness);
            List<double> sp_avg_fits = species.ConvertAll(s => s.Average(x => x.Fitness));
            // Calculate amount of babies
            int[] species_babies = new int[species.Count];
            for (int i = 0; i < species_babies.Length; i++)
            {
                if (avg_fit == 0)
                    species_babies[i] = species[i].Count;
                else
                    species_babies[i] = (int)(sp_avg_fits[i] / avg_fit * species[i].Count);
            }
            // If there is space for extra babies, distribute babies to the fittest species first
            for (int i = 0; species_babies.Sum() < pop_size; i++)
                species_babies[i]++;

            // Return if target reached
            if (NNs.Max(x => x.Fitness) >= FITNESS_TARGET)
            {
                SaveNNs(path, NNs, gen, compat_tresh);
                return NNs;
            }

            // Make save file before mating
            SaveNNs(save_path, NNs, gen, compat_tresh);

            // Mating season
            List<NN> new_NNs = new List<NN>();
            for (int i = 0; i < species.Count; i++)
            {
                // Only top n% can reproduce
                species[i] = species[i].OrderByDescending(x => x.Fitness).ToList();
                List<NN> elites = species[i].GetRange(0, (int)Math.Ceiling(species[i].Count * ELITE_PERCENT));
                
                // Add best from species without mutating
                if (species_babies[i] > 0) new_NNs.Add(elites[0]);
                
                // Prepare roulette wheel (fitter parents have higher chance of mating)
                double[] wheel = new double[elites.Count];
                wheel[0] = elites[0].Fitness;
                for (int j = 1; j < wheel.Length; j++)
                    wheel[j] = wheel[j - 1] + elites[j].Fitness;

                // Make babeeeeeee
                for (int j = 0; j < species_babies[i] - 1; j++)
                {
                    // Spin the wheel
                    int index = 0;
                    double speen = Rand.NextDouble() * wheel[^1];
                    while (wheel[index] <= speen)
                    {
                        if (++index == wheel.Length)
                        {
                            index = 0;
                            break;
                        }
                    }
                    NN parent1 = elites[index];

                    index = 0;
                    speen = Rand.NextDouble() * wheel[^1];
                    while (wheel[index] <= speen)
                    {
                        if (++index == wheel.Length)
                        {
                            index = 0;
                            break;
                        }
                    }
                    NN parent2 = elites[index];

                    // Uwoooogh seeeeegs
                    new_NNs.Add(parent1.MateWith(parent2));
                }
            }

            // Replace old NNs with new NNs and set all NNs to unplayed
            new_NNs.CopyTo(NNs);
            foreach (NN nn in NNs)
                nn.Played = false;

            // Save new generation
            SaveNNs(path, NNs, gen, compat_tresh);
        }

        return NNs;
    }

    public static List<List<NN>> Speciate(NN[] networks, double compat_tresh)
    {
        List<List<NN>> species = new List<List<NN>>();
        species.Add(new List<NN> { networks[0] });
        for (int i = 1; i < networks.Length; i++)
        {
            bool existing_sp = false;
            NN nn = networks[i];
            foreach (List<NN> s in species)
            {
                if (nn.SameSpecies(s[0], compat_tresh))
                {
                    s.Add(nn);
                    existing_sp = true;
                    break;
                }
            }
            if (!existing_sp) species.Add(new List<NN> { nn });
        }

        return species;
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
