namespace Tetris;

using FastConsole;
using Masks;
using System.Diagnostics;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Documents;
using System.IO;
using System.Windows.Media;
using static System.Formats.Asn1.AsnWriter;
using System.Windows.Shapes;

class Bot
{

    
    const double THINKTIMEINSECONDS = 0.15,
                 MINTRESH = -1, MAXTRESH = -0.01, MOVEMUL = 1.05, MOVETARGET = 5,
                 DISCOUNT_FACTOR = 0.95;

    bool Done;
    public Game Game;
    private readonly NN Network;

    bool UsePCFinder = true;    
    long ThinkTicks = (long)(THINKTIMEINSECONDS * Stopwatch.Frequency);
    double MoveTresh = -0.1;

    readonly Dictionary<ulong, double> CachedValues = new Dictionary<ulong, double>();
    readonly Dictionary<ulong, double> CachedStateValues = new Dictionary<ulong, double>();
    readonly ulong[][] NextHashTable;
    readonly ulong[] PieceHashTable = RandomArray(8), HoldHashTable = RandomArray(8);
    readonly double[] Discounts;

    readonly Stopwatch Timer = new Stopwatch();
    bool TimesUp;

    int CurrentDepth;
    double MaxDepth;

    const int run_avg_count = 20;
    long[] nodec = new long[run_avg_count];

    public Bot(string filePath, Game game)
    {
        Network = NN.LoadNN(filePath);
        AttachGame(game);
        NextHashTable = RandomArray(game.NextLength, 8);
        Discounts = new double[game.NextLength];
        for (int i = 0; i < game.NextLength; i++) Discounts[i] = Math.Pow(DISCOUNT_FACTOR, i);

        #if NET6_0_OR_GREATER
        CachedValues.EnsureCapacity(200000);
        CachedStateValues.EnsureCapacity(5000000);
        #endif
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

    public void AttachGame(Game game)
    {
        Game = game;
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
                Game.TickAsync();
                FConsole.ForceRender();
                Game.WriteAt(1, 0, ConsoleColor.White, Math.Round(nodec.Average()).ToString().PadRight(9));
                Game.WriteAt(1, 22, ConsoleColor.White, MaxDepth.ToString().PadRight(6));
                Game.WriteAt(1, 23, ConsoleColor.White, MoveTresh.ToString().PadRight(12));
                for (int i = nodec.Length - 1; i > 0; i--)
                    nodec[i] = nodec[i - 1];
                nodec[0] = 0;

                while (!Done) Thread.Sleep(0);
            }
        });
        main.Priority = ThreadPriority.Highest;
        main.Start();
    }

    public List<Moves> FindMoves()
    {
        // Remove excess cache
        //if (CachedValues.Count >= 200000) CachedValues.Clear();
        //if (CachedStateValues.Count >= 5000000) CachedStateValues.Clear();
        while (CachedValues.Count > 180000) 
            CachedValues.Remove(CachedValues.First().Key);
        while (CachedStateValues.Count > 4970000)
            CachedStateValues.Remove(CachedStateValues.First().Key);

        // Call the dedicated PC Finder
        //

        Timer.Restart();
        TimesUp = false;

        Matrix10x24 Matrix = Game.Matrix.Clone();
        ///Matrix10x24* ptr = &Matrix;
        Piece Current = Game.Current, Hold = Game.Hold;
        Piece[] Next = (Piece[])Game.Next.Clone();
        int B2B = Game.B2B, Combo = Game.Combo;

        List<Moves> best_moves = new List<Moves>();
        double bestScore = double.MinValue;

        // Keep searching until time's up or no next pieces left
        MaxDepth = 0;
        
        double out1 = Network.FeedFoward(TetrisHelper.ExractFeat(Matrix, 0))[0];
        for (int depth = 0; !TimesUp && depth < Game.NextLength; depth++)
        {
            CurrentDepth = depth;
            for (int swap_int = 0; swap_int < 2; swap_int++)
            {
                bool holding_piece = swap_int == 1;
                // Get piece based on swap
                int nexti = 0;
                Piece piece = holding_piece ? Hold : Current;
                Piece hold = holding_piece ? Current : Hold;
                if (piece == Piece.EMPTY) piece = Next[nexti++];

                for ( ; !TimesUp && piece < Piece.ROTATION_CW * 4; piece += Piece.ROTATION_CW)
                {
                    List<Moves> temp_moves = new List<Moves>();
                    if ((piece & Piece.ROTATION_BITS) == Piece.ROTATION_CW) 
                        temp_moves.Add(Moves.RotateCW);
                    else if ((piece & Piece.ROTATION_BITS) == Piece.ROTATION_CCW)
                        temp_moves.Add(Moves.RotateCCW);
                    else if ((piece & Piece.ROTATION_BITS) == Piece.ROTATION_180)
                        temp_moves.Add(Moves.Rotate180);
                    for (int i = 0; i < piece.StartX; i++) temp_moves.Add(Moves.Right);

                    for (int x = 0; x < piece.MaxX; x++)
                    {
                        if (temp_moves.Count == 0)
                            temp_moves.Add(Moves.Left);
                        else if (temp_moves[temp_moves.Count - 1] == Moves.Right)
                            temp_moves.RemoveAt(temp_moves.Count - 1);
                        else
                            temp_moves.Add(Moves.Left);

                        int y = piece.StartY;
                        // Hard drop
                        Matrix.MoveToGround(piece, x, ref y);
                        // Update screen
                        Matrix10x24 matrix = Matrix.Clone();
                        double garbage;
                        ReadOnlySpan<int> info = SearchScore(matrix, B2B, x, y, piece, Combo, false, out garbage);
                        // Check if better
                        double newvalue = FindBestScore(matrix, depth, nexti + 1, Next[nexti], hold, false, info[2], info[0], out1, garbage);
                        if (newvalue >= bestScore)
                        {
                            if (newvalue > bestScore || DistanceFromWall(piece, temp_moves) < DistanceFromWall(holding_piece ^ best_moves.Contains(Moves.Hold) ? piece : hold, best_moves))
                            {
                                bestScore = newvalue;
                                best_moves = new List<Moves>(temp_moves);
                                if (holding_piece) best_moves.Insert(0, Moves.Hold);
                            }
                        }
                        //Revert(vscreen, piece, rot, orix, oriy, info);
                        // Only vertical pieces can be spun
                        if ((piece & Piece.ROTATION_CW) == Piece.ROTATION_CW && (piece & Piece.PIECE_BITS) != Piece.O)
                        {
                            for (int cw_int = 0; cw_int < 2; cw_int++)
                            {
                                bool cw = cw_int == 0;

                                temp_moves.Add(Moves.SoftDrop);
                                Piece _piece = piece;
                                for (int i = 0; i < 2; i++)
                                {
                                    int new_x = x, new_y = y;
                                    bool rotated;
                                    if (cw) rotated = Matrix.TryRotateCW(ref _piece, ref new_x, ref new_y);
                                    else rotated = Matrix.TryRotateCCW(ref _piece, ref new_x, ref new_y);
                                    if (!rotated) break;
                                    if (!Matrix.OnGround(piece, new_x, new_y)) break;
                                    temp_moves.Add(cw ? Moves.RotateCW : Moves.RotateCCW);

                                    // Update screen
                                    matrix = Matrix.Clone();
                                    info = SearchScore(matrix, B2B, new_x, new_y, _piece, Combo, true, out garbage);
                                    // Check if better
                                    newvalue = FindBestScore(matrix, depth, nexti + 1, Next[nexti], hold, false, info[2], info[0], out1, garbage);
                                    if (newvalue > bestScore)
                                    {
                                        bestScore = newvalue;
                                        best_moves = new List<Moves>(temp_moves);
                                        if (holding_piece) best_moves.Insert(0, Moves.Hold);
                                    }
                                    // Revert screen
                                    //Revert(vscreen, piece, r, x, y, info);

                                    // Only try to spin T pieces twice (for TSTs)
                                    if ((piece & Piece.PIECE_BITS) != Piece.T) break;
                                }

                                while (temp_moves[temp_moves.Count - 1] == (cw ? Moves.RotateCW : Moves.RotateCCW)) temp_moves.RemoveAt(temp_moves.Count - 1);
                                temp_moves.Remove(Moves.SoftDrop);
                            }
                        }
                    }
                    // o pieces can't be rotated
                    if (piece == 7)
                    {
                        MaxDepth += 1d / 2;
                        break;
                    }
                    else MaxDepth += (1d / 2) / 4;
                }
            }
        }
        CachedValues.Clear();
        Timer.Stop();

        // Check if pc found
        //
        
        // Otherwise use ai's moves
        best_moves.Add(Moves.HardDrop);
        // Adjust movetresh
        double time_remaining = (double)(ThinkTicks - Timer.ElapsedTicks) / ThinkTicks;
        if (time_remaining > 0)
            MoveTresh *= Math.Pow(MOVEMUL, time_remaining / ((1 + Math.E) / Math.E - time_remaining));
        else
            MoveTresh *= Math.Pow(MOVEMUL, MaxDepth - MOVETARGET);
        MoveTresh = Math.Min(Math.Max(MoveTresh, MINTRESH), MAXTRESH);

        return best_moves;

        
        double FindBestScore(Matrix10x24 _matrix, int depth, int nexti, Piece _piece, Piece _hold, bool swapped, int comb, int b2b, double prevstate, double _trash)
        {
            //if (TimesUp || pc_found) return double.MinValue;
            if (TimesUp) return double.MinValue;
            if (Timer.ElapsedTicks > ThinkTicks)
            {
                TimesUp = true;
                return double.MinValue;
            }

            if (depth == 0 || nexti == Next.Length)
            {
                nodec[0]++;

                ulong hash = BitConverter.DoubleToUInt64Bits(_trash);
                hash ^= _matrix.Hash();
                if (CachedStateValues.ContainsKey(hash))
                    return CachedStateValues[hash];

                double[] feat = TetrisHelper.ExractFeat(_matrix, _trash);
                double val = Network.FeedFoward(feat)[0];
                CachedStateValues.Add(hash, val);
                return val;
            }
            else
            {
                double _value = double.MinValue;
                // Have we seen this situation before?
                ulong hash = HashBoard(_matrix, _piece, _hold, nexti, depth);
                if (CachedValues.ContainsKey(hash)) return CachedValues[hash];
                // Check if this move should be explored
                double discount = Discounts[CurrentDepth - depth];
                double[] outs = Network.FeedFoward(TetrisHelper.ExractFeat(_matrix, _trash));
                if (outs[0] - prevstate < MoveTresh)
                {
                    ulong statehash = BitConverter.DoubleToUInt64Bits(_trash);
                    statehash ^= _matrix.Hash();
                    if (!CachedStateValues.ContainsKey(statehash))
                        CachedStateValues.Add(statehash, outs[0]);
                    return outs[0];
                }
                // Check value for swap
                if (!swapped)
                {
                    if (Hold == 0)
                        _value = Math.Max(_value, FindBestScore(_matrix, depth, nexti + 1, Next[nexti], _piece, true, comb, b2b, prevstate, _trash));
                    else
                        _value = Math.Max(_value, FindBestScore(_matrix, depth, nexti, _hold, _piece, true, comb, b2b, prevstate, _trash));
                    if (CachedValues.ContainsKey(hash))
                        return CachedValues[hash];
                }
                // Check all landing spots
                for ( ; _piece < Piece.ROTATION_CW * 4; _piece += Piece.ROTATION_CW)
                {
                    for (int x = 0; x < _piece.MaxX; x++)
                    {
                        int y = 18;
                        // Hard drop
                        _matrix.MoveToGround(_piece, x, ref y);
                        Matrix10x24 _matrix_clone = _matrix.Clone();
                        double garbage;
                        ReadOnlySpan<int> info;
                        if ((_piece != Piece.S && _piece != Piece.Z) || (_piece & Piece.ROTATION_180) == 0)
                        {
                            // Update matrix
                            info = SearchScore(_matrix_clone, b2b, x, y, _piece, comb, false, out garbage);
                            // Check if better
                            _value = Math.Max(_value, FindBestScore(_matrix_clone, depth - 1, nexti + 1, Next[nexti], _hold, false, info[2], info[0], outs[0], discount * garbage + _trash));
                            // Revert matrix
                            //Revert(_matrix, _piece, rot, orix, oriy, info);
                        }
                        // Only vertical pieces can be spun
                        if ((_piece & Piece.ROTATION_CW) == Piece.ROTATION_CW && (_piece & Piece.PIECE_BITS) != Piece.O)
                        {
                            for (int cw_counter = 0; cw_counter < 2; cw_counter++)
                            {
                                bool cw = cw_counter == 0;
                                // Try spin clockwise
                                int new_x = x, new_y = y;
                                bool _rotated = true;
                                for (int i = 0; i < 2 && _rotated; i++)
                                {
                                    if (cw) _rotated = _matrix.TryRotateCW(ref _piece, ref new_x, ref new_y);
                                    else _rotated = _matrix.TryRotateCCW(ref _piece, ref new_x, ref new_y);
                                    
                                    if (!_matrix.OnGround(_piece, new_x, new_y)) break;
                                    // Update matrix
                                    _matrix_clone = _matrix.Clone();
                                    info = SearchScore(_matrix_clone, b2b, new_x, new_y, _piece, comb, true, out garbage);
                                    // Check if better
                                    _value = Math.Max(_value, FindBestScore(_matrix_clone, depth - 1, nexti + 1, Next[nexti], _hold, false, info[2], info[0], outs[0], discount * garbage + _trash));
                                    // Revert matrix
                                    //Revert(_matrix, _piece, r, x, y, info);

                                    // Only try to spin T pieces twice(for TSTs)
                                    if (_piece.R == Piece.ROTATION_180 || _piece.PieceType != Piece.T) break;
                                }
                            }
                        }
                    }
                    //o pieces can't be rotated
                    if (_piece == 7) break;
                }

                CachedValues.Add(hash, _value);
                return _value;
            }
        }

        ReadOnlySpan<int> SearchScore(Matrix10x24 _matrix, int _b2b, int _x, int _y, Piece _piece, int comb, bool lastrot, out double _trash)
        {
            // { B2B, T-Spin, combo }
            // Check for t - spins
            int tspin = _matrix.GetTSpinKind(lastrot, _piece, _x, _y); //0 = no spin, 2 = mini, 3 = t-spin
            // Find cleared lines
            //_matrix.MoveToGround(_piece, _x, ref _y);
            int cleared = _matrix.PlaceAndClear(_piece, _x, _y);
            // B2B
            bool B2Bbonus = (tspin + cleared > 3) && _b2b > -1;
            if (tspin == 0 && cleared != 4 && cleared != 0) _b2b = -1; // Reset B2B
            else if (tspin + cleared > 3) _b2b++;
            // Combo
            comb = cleared == 0 ? -1 : comb + 1;
            // Perfect clear
            bool pc = (_matrix[0] & 0b1111111111) == 0;

            // Trash sent
            //int trash;
            //if (pc) trash = Game.PCTrash[cleared];
            //else if (tspin == 3) trash = Game.TSpinsTrash[cleared];
            //else trash = Game.LinesTrash[cleared];
            //if (B2Bbonus) trash++;
            //if (comb > 0) trash += Game.ComboTrash[Math.Min(comb, Game.ComboTrash.Length - 1)];

            // Trash sent v2
            //_trash = new double[] { 0, 0, 1, 2, 4 }[cleared];
            if (pc) _trash = 5;
            else _trash = new double[] { 0, 0, 1, 1.5, 5 }[cleared];
            //if (tspin == 3) _trash += new double[] { 0, 2, 3, 4 }[cleared];
            if (tspin == 3) _trash += new double[] { 0, 2, 5, 8.5 }[cleared];
            if (B2Bbonus) _trash++;
            //if (pc) _trash += 4;
            //if (comb > 0) _trash += new double[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 9)];
            if (comb > 0) _trash += new double[] { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 11)]; //jstris combo table
            // Modify _trash(use trash sent * APL and offset it slightly)
            //if (cleared != 0 && comb > 1) _trash = Math.FusedMultiplyAdd(_trash, _trash / cleared, -1.5);

            return new ReadOnlySpan<int>(new int[] { _b2b, tspin, comb });
        }

        ulong HashBoard(Matrix10x24 _matrix, Piece _piece, Piece _hold, int _nexti, int _depth)
        {
            ulong hash = _matrix.Hash();
            hash ^= PieceHashTable[_piece];
            hash ^= HoldHashTable[_hold];
            for (int i = 0; (_nexti + i < Next.Length) && (i < _depth); i++)
                hash ^= NextHashTable[i][Next[_nexti + i]];

            return hash;
        }
    }

    int DistanceFromWall(Piece piece, List<Moves> moves)
    {
        int x = piece.StartX;
        foreach (Moves move in moves)
        {
            if (move == Moves.Right) x--;
            else if (move == Moves.Left) x++;
            else if (move == Moves.RotateCW) piece += Piece.ROTATION_CW;
            else if (move == Moves.RotateCCW) piece += Piece.ROTATION_CCW;
            else if (move == Moves.SoftDrop) break;
        }
        return Math.Min(x, piece.MaxX - x);
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

enum ActivationType
{
    Sigmoid,
    TanH,
    ReLU,
    LeakyReLU,
    ELU,
    SELU,
    SoftPlus,
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

        public Node(int id, List<Node> network, ActivationType activationType, List<Connection> inputs = null, List<Connection> outputs = null)
        {
            Id = id;
            Network = network;
            Inputs = inputs ?? new List<Connection>();
            Outputs = outputs ?? new List<Connection>();
            switch (activationType)
            {
                case ActivationType.Sigmoid:
                    Activation = Sigmoid;
                    break;
                case ActivationType.TanH:
                    Activation = TanH;
                    break;
                case ActivationType.ReLU:
                    Activation = ReLU;
                    break;
                case ActivationType.LeakyReLU:
                    Activation = LeakyReLU;
                    break;
                case ActivationType.ELU:
                    Activation = ELU;
                    break;
                case ActivationType.SELU:
                    Activation = SELU;
                    break;
                case ActivationType.SoftPlus:
                    Activation = SoftPlus;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(activationType), activationType, null);
            }
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
                Nodes.Add(new Node(Nodes.Count, Nodes, ActivationType.ReLU));
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
}
