namespace Tetris;

using NEAT;
using System;
using System.Diagnostics;
using System.Numerics;

public abstract class Bot
{
    protected const double DISCOUNT_FACTOR = 0.95;

    protected Game _Game;
    public Game Game
    {
        get => _Game;
        set
        {
            if (_Game != null)
            {
                _Game.SoftG = Game.Settings.SoftG;
                _Game.IsBot = false;
            }
            _Game = value;
            if (_Game != null)
            {
                _Game.SoftG = 40;
                _Game.IsBot = true;
            }
        }
    }
    protected GameBase Sim;
    protected readonly NN Network;

    protected int CurrentDepth;
    protected double MaxDepth;

    protected readonly Dictionary<ulong, double> CachedValues = new Dictionary<ulong, double>();
    protected readonly Dictionary<ulong, double> CachedStateValues = new Dictionary<ulong, double>();
    protected readonly ulong[][] MatrixHashTable = RandomArray(16, 65536), NextHashTable;
    protected readonly ulong[] PieceHashTable = RandomArray(8), HoldHashTable = RandomArray(8);
    protected readonly double[] Discounts;

    protected long ThinkTicks = Stopwatch.Frequency;
    protected abstract double MoveTreshStart { get; }
    protected double MoveTresh;
    protected readonly Stopwatch Sw = new Stopwatch();
    protected bool TimesUp;

    public Thread BotThread { get; private set; }
    protected bool ToStop;
    protected const int RunAvgCount = 20;
    protected long[] NodeCounts;

    protected Bot(Game game)
    {
        Game = game;
        NextHashTable = RandomArray(game.Next.Length, 8);
        Discounts = new double[game.Next.Length];
        for (int i = 0; i < game.Next.Length; i++) Discounts[i] = Math.Pow(DISCOUNT_FACTOR, i);
        MoveTresh = MoveTreshStart;
    }

    public Bot(NN network, Game game) : this(game)
    {
        Network = network;
        game.Name = network.Name ?? "Bot";
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
        BotThread = new Thread(() =>
        {
            while (!Game.IsDead)
            {
                // Find moves
                Game.Tick();
                List<Moves> moves = FindMoves();
                // Wait out excess think time
                while (Sw.ElapsedTicks < ThinkTicks) Thread.Sleep(0);
                // Play moves
                foreach (Moves move in moves)
                {
                    Game.Play(move);
                    if (move_delay != 0)
                    {
                        Game.Tick();
                        Thread.Sleep(move_delay);
                    }
                }
                // Stats
                Game.WriteAt(0, Game.GameHeight - 1, ConsoleColor.White, $"Depth: {MaxDepth}".PadRight(Game.GameWidth));
                long count = NodeCounts.Aggregate(0, (aggregate, next) => (next == 0) ? aggregate : aggregate + 1);
                if (count == 0) count++;
                Game.WriteAt(0, Game.GameHeight, ConsoleColor.White, $"Nodes: {NodeCounts.Sum() / count}".PadRight(Game.GameWidth));
                Game.WriteAt(0, Game.GameHeight + 1, ConsoleColor.White, $"Tresh: {Math.Round(MoveTresh, 6)}".PadRight(Game.GameWidth));
                for (int i = NodeCounts.Length - 1; i > 0; i--)
                    NodeCounts[i] = NodeCounts[i - 1];
                NodeCounts[0] = 0;
                // Check if should stop
                if (ToStop) break;
            }

            ToStop = false;
            Game.IsBot = false;
            Game.SoftG = Game.Settings.SoftG;
            return;
        });
        BotThread.Priority = ThreadPriority.Highest;
        BotThread.Start();

        // Write stats
        Game.WriteAt(0, Game.GameHeight - 1, ConsoleColor.White, $"Depth: 0".PadRight(Game.GameWidth));
        Game.WriteAt(0, Game.GameHeight, ConsoleColor.White, $"Nodes: 0".PadRight(Game.GameWidth));
        Game.WriteAt(0, Game.GameHeight + 1, ConsoleColor.White, $"Tresh: 0.000000".PadRight(Game.GameWidth));
    }

    public void Stop()
    {
        ToStop = true;
        BotThread.Join();
    }

    protected abstract List<Moves> FindMoves();

    protected virtual int[] SearchScore(bool last_rot, ref int combo, ref int b2b, out double trash, out int cleared)
    {
        // { scoreadd, B2B, T - spin, combo, clears } //first bit of B2B = B2B chain status
        // Check for t - spins
        int tspin = Sim.TSpinType(last_rot);
        // Clear lines
        int[] clears = Sim.Place(out cleared);
        // Combo
        combo = cleared == 0 ? -1 : combo + 1;
        // Perfect clear
        bool pc = Sim.Matrix.GetRow(0) == 0;
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

        trash = pc ? Game.Settings.PCTrash[cleared] :
                tspin == 3 ? Game.Settings.TSpinTrash[cleared] :
                             Game.Settings.LinesTrash[cleared];
        if (combo > 0) trash += Game.Settings.ComboTrash[Math.Min(combo, Game.Settings.ComboTrash.Length) - 1];
        if ((tspin + cleared > 3) && b2b > 0) trash++;

        return clears;
    }

    protected virtual ulong HashBoard(Piece piece, Piece _hold, int nexti, int depth, double trash)
    {
        ulong hash = PieceHashTable[piece.PieceType] ^ HoldHashTable[_hold.PieceType];
        for (int i = 0; nexti + i < NextHashTable.Length && i < depth; i++)
            hash ^= NextHashTable[i][Sim.Next[nexti + i]];

        return hash ^ HashState(trash);
    }

    protected virtual ulong HashState(double trash)
    {
        ulong hash = BitConverter.DoubleToUInt64Bits(trash);
        for (int i = 0, shift = 0; i < 4; i++)
        {
            hash ^= MatrixHashTable[i + 0][(Sim.Matrix.LowLow >> shift) & 0xFFFF];
            hash ^= MatrixHashTable[i + 4][(Sim.Matrix.LowHigh >> shift) & 0xFFFF];
            hash ^= MatrixHashTable[i + 8][(Sim.Matrix.HighLow >> shift) & 0xFFFF];
            hash ^= MatrixHashTable[i + 12][(Sim.Matrix.HighHigh >> shift) & 0xFFFF];
            shift += 16;
        }

        return hash;
    }
}

public sealed class BotOld : Bot
{
    const double MIN_TRESH = -1, MAX_TRESH = -0.01, MOVE_MUL = 1.05, MOVE_TARGET = 6;

    public bool UsePCFinder = true;
    protected override double MoveTreshStart { get => -0.05; }

    public BotOld(NN network, Game game) : base(network, game) { }

    public BotOld(string filePath, Game game) : base(filePath, game) { }

    protected override List<Moves> FindMoves()
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
                        Sim.Y = GameBase.START_Y;
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
                        Sim.Unplace(clears);
                        // Only vertical pieces can be spun
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
                                    Sim.Unplace(clears);

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
        // Remove excess cache
        CachedValues.Clear();
        CachedStateValues.Clear();

        // Check if PC found
        //
        // Adjust movetresh
        TimesUp = true;
        double time_remaining = (double)(ThinkTicks - Sw.ElapsedTicks) / ThinkTicks;
        if (time_remaining > 0)
            MoveTresh *= Math.Pow(MOVE_MUL, time_remaining / ((1 + Math.E) / Math.E - time_remaining));
        else
            MoveTresh *= Math.Pow(MOVE_MUL, MaxDepth - MOVE_TARGET);
        MoveTresh = Math.Min(Math.Max(MoveTresh, MIN_TRESH), MAX_TRESH);

        return bestMoves;
    }

    private double Search(int depth, int nexti, Piece current, Piece _hold, bool swapped, int comb, int b2b, double prevstate, double _trash)
    {
        //if (pc_found) return double.MinValue;
        if (Sw.ElapsedTicks > ThinkTicks)
        {
            TimesUp = true;
            return double.MinValue;
        }

        if (depth == 0 || nexti == NextHashTable.Length)
        {
            NodeCounts[0]++;

            ulong hash = HashState(_trash);
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
            ulong hash = HashBoard(current, _hold, nexti, depth, _trash);
            if (CachedValues.ContainsKey(hash)) return CachedValues[hash];
            // Check if this move should be explored
            double discount = Discounts[CurrentDepth - depth];
            double[] outs = Network.FeedFoward(ExtrFeat(_trash));
            if (outs[0] - prevstate < MoveTresh)
            {
                ulong statehash = HashState(_trash);
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
                    Sim.Y = GameBase.START_Y;
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
                        Sim.Unplace(clears);
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
                                Sim.Unplace(clears);

                                // Only try to spin T pieces twice(for TSTs)
                                if (current.PieceType != Piece.T) break;
                            }
                        }
                    }
                }
                // O pieces can't be rotated
                if (current == Piece.O) break;
            }

            CachedValues.Add(hash, _value);
            return _value;
        }
    }

    protected override int[] SearchScore(bool last_rot, ref int combo, ref int b2b, out double trash, out int cleared)
    {
        // { scoreadd, B2B, T - spin, combo, clears } //first bit of B2B = B2B chain status
        // Check for t - spins
        int tspin = Sim.TSpinType(last_rot);
        // Clear lines
        int[] clears = Sim.Place(out cleared);
        // Combo
        combo = cleared == 0 ? -1 : combo + 1;
        // Perfect clear
        bool pc = Sim.Matrix.GetRow(0) == 0;
        // Compute score
        if (tspin == 0 && cleared != 4 && cleared != 0) b2b = -1;
        else if (tspin + cleared > 3) b2b++;

        //_trash = new double[] { 0, 0, 1, 2, 4 }[cleared];
        if (pc) trash = 5;
        else trash = new double[] { 0, 0, 1, 1.5, 5 }[cleared];
        //if (info[2] == 3) _trash += new double[] { 0, 2, 3, 4 }[cleared];
        if (tspin == 3) trash += new double[] { 0, 2, 5, 8.5 }[cleared];
        if ((tspin + cleared > 3) && b2b > 0) trash++;
        //if (pc) _trash += 4;
        //if (combo > 0) trash += new double[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(combo - 1, 9)];
        if (combo > 0) trash += new double[] { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(combo - 1, 11)]; //jstris combo table
        // Modify _trash(use trash sent * APL and offset it slightly)
        //if (cleared != 0 && combo > 1) trash = Math.FusedMultiplyAdd(trash, trash / cleared, -1.5);

        return clears;
    }

    [Obsolete]
    private double[] ExtrFeat(double trash)
    {
        uint[] rows = Sim.Matrix.GetRows();
        uint[] block_bits = new uint[10];
        // Find heightest block in each column
        int[] heights = new int[10];
        for (int x = 0; x < 10; x++)
        {
            int height = Sim.Highest + 1;
            block_bits[x] = 1U << (9 - x);
            for (int y = Sim.Highest; y >= 0; y--)
            {
                if ((rows[y] & block_bits[x]) != 0) break;
                height--;
            }
            heights[x] = height;
        }

        // Standard height
        double h = 0;
        if (Network.Visited[0])
        {
            int sqr_sum = 0;
            foreach (int height in heights)
                sqr_sum += height * height;
            h = Math.Sqrt(sqr_sum);
        }
        // "Caves"
        int caves = 0;
        if (Network.Visited[1])
        {
            for (int y = heights[0] - 2; y >= 0; y--)
                if ((rows[y] & block_bits[0]) == 0 && (rows[y + 1] & block_bits[0]) != 0)
                    if (y < heights[1])
                        caves += heights[0] - y;
            for (int x = 1; x < 9; x++)
            {
                for (int y = heights[x] - 2; y >= 0; y--)
                {
                    if ((rows[y] & block_bits[x]) == 0 && (rows[y + 1] & block_bits[x]) != 0)
                        if (y <= Math.Max(heights[x - 1], heights[x + 1]))
                            caves += heights[x] - y;
                }
            }
            for (int y = heights[9] - 2; y >= 0; y--)
                if ((rows[y] & block_bits[9]) == 0 && (rows[y + 1] & block_bits[9]) != 0)
                    if (y <= heights[8])
                        caves += heights[9] - y;
        }
        // Pillars
        int pillars = 0;
        if (Network.Visited[2])
        {
            for (int x = 0; x < 10; x++)
            {
                int diff;
                // Don't punish for tall towers at the side
                if (x != 0 && x != 9) diff = Math.Min(Math.Abs(heights[x - 1] - heights[x]), Math.Abs(heights[x + 1] - heights[x]));
                else diff = x == 0 ? Math.Max(0, heights[1] - heights[0]) : Math.Min(0, heights[8] - heights[9]);
                if (diff > 2) pillars += diff * diff;
                else pillars += diff;
            }
        }
        // Row trasitions
        int rowtrans = 0;
        if (Network.Visited[3])
        {
            MatrixMask mask = new MatrixMask()
            {
                HighHigh = 0b000000_1111111110_1111111110_1111111110_1111111110_1111111110_11111111,
                HighLow = 0b10_1111111110_1111111110_1111111110_1111111110_1111111110_1111111110_11,
                LowHigh = 0b11111110_1111111110_1111111110_1111111110_1111111110_1111111110_111111,
                LowLow = 0b1110_1111111110_1111111110_1111111110_1111111110_1111111110_0000000000
            };
            rowtrans = ((Sim.Matrix ^ (Sim.Matrix << 1)) & mask).PopCount();
        }
        // Column trasitions
        int coltrans = 0;
        if (Network.Visited[4])
        {
            uint prev_row = 0;
            for (int y = Sim.Highest; y >= 0; y--)
            {
                coltrans += BitOperations.PopCount(prev_row ^ rows[y]);
                prev_row = rows[y];
            }
        }

        return new double[] { h, caves, pillars, rowtrans, coltrans, trash };
    }
}

public sealed class BotByScore : Bot
{
    const double MIN_TRESH = -1, MAX_TRESH = -0.01, MOVE_MUL = 1.05, MOVE_TARGET = 5;

    protected override double MoveTreshStart { get => -0.05; }
    public bool UsePCFinder = true;

    public BotByScore(NN network, Game game) : base(network, game) { }

    public BotByScore(string filePath, Game game) : base(NN.LoadNN(filePath), game) { }

    protected override List<Moves> FindMoves()
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
        double[] outs = Network.FeedFoward(ExtrFeat(0, 0, 0));
        ulong hash = HashState(0, 0, 0);
        if (!CachedStateValues.ContainsKey(hash))
            CachedStateValues.Add(hash, outs[0]);
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
                        Sim.Y = GameBase.START_Y;
                        // Hard drop
                        Sim.TryDrop(40);
                        int oriy = Sim.Y;
                        // Update screen
                        int new_combo = combo, new_b2b = b2b;
                        int[] clears = SearchScore(false, ref new_combo, ref new_b2b, out double garbage, out int cleared);
                        // Check if better
                        double newvalue = Search(depth, nexti + 1, Sim.Next[nexti], hold, false, new_combo, new_b2b, cleared, garbage, outs[0], outs[1]);
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
                        Sim.Unplace(clears);
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
                                    newvalue = Search(depth, nexti + 1, Sim.Next[nexti], hold, false, new_combo, new_b2b, cleared, garbage, outs[0], outs[1]);
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
                                    Sim.Unplace(clears);

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
        // Remove excess cache
        if (CachedValues.Count < 200000) CachedValues.Clear();
        if (CachedStateValues.Count < 5000000) CachedStateValues.Clear();

        // Check if PC found
        //

        // Adjust movetresh
        TimesUp = true;
        double time_remaining = (double)(ThinkTicks - Sw.ElapsedTicks) / ThinkTicks;
        if (time_remaining > 0)
            MoveTresh *= Math.Pow(MOVE_MUL, time_remaining / ((1 + Math.E) / Math.E - time_remaining));
        else
            MoveTresh *= Math.Pow(MOVE_MUL, MaxDepth - MOVE_TARGET);
        MoveTresh = Math.Min(Math.Max(MoveTresh, MIN_TRESH), MAX_TRESH);

        return bestMoves;
    }

    private double Search(int depth, int nexti, Piece current, Piece _hold, bool swapped, int comb, int b2b, double cleared, double _trash, double prevstate, double intent)
    {
        //if (pc_found) return double.MinValue;
        if (Sw.ElapsedTicks > ThinkTicks)
        {
            TimesUp = true;
            return double.MinValue;
        }

        if (depth == 0 || nexti == NextHashTable.Length)
        {
            NodeCounts[0]++;

            ulong hash = HashState(cleared, _trash, intent);
            if (CachedStateValues.ContainsKey(hash))
                return CachedStateValues[hash];

            double[] feat = ExtrFeat(cleared, _trash, intent);
            double val = Network.FeedFoward(feat)[0];
            CachedStateValues.Add(hash, val);
            return val;
        }
        else
        {
            double _value = double.MinValue;
            // Have we seen this situation before?
            ulong hash = HashBoard(current, _hold, nexti, depth, cleared, _trash, intent);
            if (CachedValues.ContainsKey(hash)) return CachedValues[hash];
            // Check if this move should be explored
            double discount = Discounts[CurrentDepth - depth];
            double[] outs = Network.FeedFoward(ExtrFeat(cleared, _trash, intent));
            if (outs[0] - prevstate < MoveTresh)
            {
                ulong statehash = HashState(cleared, _trash, intent);
                if (!CachedStateValues.ContainsKey(statehash))
                    CachedStateValues.Add(statehash, outs[0]);
                return outs[0];
            }
            // Check value for swap
            if (!swapped)
            {
                if (_hold.PieceType == Piece.EMPTY)
                    _value = Math.Max(_value, Search(depth, nexti + 1, Sim.Next[nexti], current, true, comb, b2b, cleared, _trash, outs[0], intent));
                else
                    _value = Math.Max(_value, Search(depth, nexti, _hold, current, true, comb, b2b, cleared, _trash, outs[0], intent));
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
                    Sim.Y = GameBase.START_Y;
                    Sim.Current = piece;
                    Sim.TryDrop(40);
                    int oriy = Sim.Y;
                    int[] clears;
                    if ((piece.PieceType != Piece.S && piece.PieceType != Piece.Z) || rot < Piece.ROTATION_180)
                    {
                        // Update matrix
                        int new_comb = comb, new_b2b = b2b;
                        clears = SearchScore(false, ref new_comb, ref new_b2b, out double garbage, out int _cleared);
                        // Check if better
                        _value = Math.Max(_value, Search(depth - 1, nexti + 1, Sim.Next[nexti], _hold, false, new_comb, new_b2b, cleared + _cleared, Math.FusedMultiplyAdd(discount, garbage, _trash), outs[0], outs[1]));
                        // Revert matrix
                        Sim.X = orix;
                        Sim.Y = oriy;
                        Sim.Current = piece;
                        Sim.Unplace(clears);
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
                                clears = SearchScore(true, ref new_comb, ref new_b2b, out double garbage, out int _cleared);
                                // Check if better
                                _value = Math.Max(_value, Search(depth - 1, nexti + 1, Sim.Next[nexti], _hold, false, new_comb, new_b2b, cleared + _cleared, Math.FusedMultiplyAdd(discount, garbage, _trash), outs[0], outs[1]));
                                // Revert matrix
                                Sim.X = x;
                                Sim.Y = y;
                                Sim.Current = rotated;
                                Sim.Unplace(clears);

                                // Only try to spin T pieces twice(for TSTs)
                                if (current.PieceType != Piece.T) break;
                            }
                        }
                    }
                }
                // O pieces can't be rotated
                if (current == Piece.O) break;
            }

            CachedValues.Add(hash, _value);
            return _value;
        }
    }

    private double[] ExtrFeat(double cleared, double sent, double intent)
    {
        uint[] rows = Sim.Matrix.GetRows();
        uint[] block_bits = new uint[10];
        // Find heightest block in each column
        int[] heights = new int[10];
        for (int x = 0; x < 10; x++)
        {
            int height = Sim.Highest + 1;
            block_bits[x] = 1U << (9 - x);
            for (int y = Sim.Highest; y >= 0; y--)
            {
                if ((rows[y] & block_bits[x]) != 0) break;
                height--;
            }
            heights[x] = height;
        }

        // Standard height
        double h = 0;
        if (Network.Visited[0])
        {
            int sqr_sum = 0;
            foreach (int height in heights)
                sqr_sum += height * height;
            h = Math.Sqrt(sqr_sum);
        }
        // "Caves"
        int caves = 0;
        if (Network.Visited[1])
        {
            for (int y = heights[0] - 2; y >= 0; y--)
                if ((rows[y] & block_bits[0]) == 0 && (rows[y + 1] & block_bits[0]) != 0)
                    if (y < heights[1])
                        caves += heights[0] - y;
            for (int x = 1; x < 9; x++)
            {
                for (int y = heights[x] - 2; y >= 0; y--)
                {
                    if ((rows[y] & block_bits[x]) == 0 && (rows[y + 1] & block_bits[x]) != 0)
                        if (y <= Math.Max(heights[x - 1], heights[x + 1]))
                            caves += heights[x] - y;
                }
            }
            for (int y = heights[9] - 2; y >= 0; y--)
                if ((rows[y] & block_bits[9]) == 0 && (rows[y + 1] & block_bits[9]) != 0)
                    if (y <= heights[8])
                        caves += heights[9] - y;
        }
        // Pillars
        int pillars = 0;
        if (Network.Visited[2])
        {
            for (int x = 0; x < 10; x++)
            {
                int diff;
                // Don't punish for tall towers at the side
                if (x != 0 && x != 9) diff = Math.Min(Math.Abs(heights[x - 1] - heights[x]), Math.Abs(heights[x + 1] - heights[x]));
                else diff = x == 0 ? Math.Max(0, heights[1] - heights[0]) : Math.Min(0, heights[8] - heights[9]);
                if (diff > 2) pillars += diff * diff;
                else pillars += diff;
            }
        }
        // Row trasitions
        int rowtrans = 0;
        if (Network.Visited[3])
        {
            MatrixMask mask = new MatrixMask()
            {
                HighHigh = 0b000000_1111111110_1111111110_1111111110_1111111110_1111111110_11111111,
                HighLow = 0b10_1111111110_1111111110_1111111110_1111111110_1111111110_1111111110_11,
                LowHigh = 0b11111110_1111111110_1111111110_1111111110_1111111110_1111111110_111111,
                LowLow = 0b1110_1111111110_1111111110_1111111110_1111111110_1111111110_0000000000
            };
            rowtrans = ((Sim.Matrix ^ (Sim.Matrix << 1)) & mask).PopCount();
        }
        // Column trasitions
        int coltrans = 0;
        if (Network.Visited[4])
        {
            uint prev_row = 0;
            for (int y = Sim.Highest; y >= 0; y--)
            {
                coltrans += BitOperations.PopCount(prev_row ^ rows[y]);
                prev_row = rows[y];
            }
        }

        return new double[] { h, caves, pillars, rowtrans, coltrans, sent, cleared, intent };
    }

    ulong HashBoard(Piece piece, Piece _hold, int nexti, int depth, double cleared, double trash, double intent)
    {
        ulong hash = PieceHashTable[piece.PieceType] ^ HoldHashTable[_hold.PieceType];
        for (int i = 0; nexti + i < NextHashTable.Length && i < depth; i++)
            hash ^= NextHashTable[i][Sim.Next[nexti + i]];

        return hash ^ HashState(cleared, trash, intent);
    }

    ulong HashState(double cleared, double trash, double intent)
    {
        ulong hash = 0;
        for (int i = 0, shift = 0; i < 4; i++)
        {
            hash ^= MatrixHashTable[i + 0][(Sim.Matrix.LowLow >> shift) & 0xFFFF];
            hash ^= MatrixHashTable[i + 4][(Sim.Matrix.LowHigh >> shift) & 0xFFFF];
            hash ^= MatrixHashTable[i + 8][(Sim.Matrix.HighLow >> shift) & 0xFFFF];
            hash ^= MatrixHashTable[i + 12][(Sim.Matrix.HighHigh >> shift) & 0xFFFF];
            shift += 16;
        }
        hash ^= BitConverter.DoubleToUInt64Bits(trash) << 3;
        hash ^= BitConverter.DoubleToUInt64Bits(cleared) >> 5;
        hash ^= BitConverter.DoubleToUInt64Bits(intent);
        return hash;
    }
}
