//using Tetris;
//using System.Diagnostics;
//using System.IO;
//using System.Text;

//class Bot : GameBase
//{
//    const double THINKTIMEINSECONDS = 0.3,
//                 MINTRESH = -1, MAXTRESH = -0.01, MOVEMUL = 1.05, MOVETARGET = 5,
//                 DISCOUNT_FACTOR = 0.95;

//    public Game Game;
//    bool Done;

//    private readonly NN Network;
//    bool UsePCFinder = true;
//    long ThinkTicks = (long)(THINKTIMEINSECONDS * Stopwatch.Frequency);
//    double MoveTresh = -0.1;

//    int CurrentDepth;
//    double MaxDepth;

//    readonly Dictionary<long, double> CachedValues = new Dictionary<long, double>();
//    readonly Dictionary<long, double> CachedStateValues = new Dictionary<long, double>();
//    readonly long[][] MatrixHashTable, NextHashTable;
//    readonly long[] PieceHashTable, HoldHashTable;
//    readonly double[] Discounts;

//    readonly Stopwatch Timer = new Stopwatch();
//    bool TimesUp;


//    const int run_avg_count = 20;
//    long[] nodec = new long[run_avg_count];

//    public Bot(string filePath, Game game)
//    {
//        Network = NN.LoadNN(filePath);
//        AttachGame(game);

//        MatrixHashTable = RandomArray(10, 40);
//        PieceHashTable = RandomArray(8);
//        HoldHashTable = RandomArray(8);
//        NextHashTable = RandomArray(game.Next.Length, 8);

//        Discounts = new double[game.Next.Length];
//        for (int i = 0; i < game.Next.Length; i++) Discounts[i] = Math.Pow(DISCOUNT_FACTOR, i);

//        CachedValues.EnsureCapacity(200000);
//        CachedStateValues.EnsureCapacity(5000000);
//    }

//    static long[] RandomArray(int size)
//    {
//        long[] arr = new long[size];
//        byte[] bytes = new byte[size * sizeof(long)];
//        Random rand = new Random(Guid.NewGuid().GetHashCode());
//        rand.NextBytes(bytes);
//        Buffer.BlockCopy(bytes, 0, arr, 0, bytes.Length);

//        return arr;
//    }

//    static long[][] RandomArray(int size1, int size2)
//    {
//        long[][] arr = new long[size1][];
//        for (int i = 0; i < size1; i++)
//            arr[i] = RandomArray(size2);
//        return arr;
//    }

//    public void AttachGame(Game game)
//    {
//        Game = game;
//        SyncState();
//    }

//    public void SyncState()
//    {
//        GameBaseInfo gameInfo = Game.GetInfo();

//        for (int i = 0; i < 40; i++)
//        {
//            Matrix[i] = new int[10];
//            Buffer.BlockCopy(gameInfo.Matrix[i], 0, Matrix[i], 0, sizeof(int) * 10);
//        }

//        X = gameInfo.X; Y = gameInfo.Y;

//        Current = gameInfo.Current; Hold = gameInfo.Hold;
//        Next = new int[gameInfo.Next.Length];
//        Buffer.BlockCopy(gameInfo.Next, 0, Next, 0, Next.Length);

//        B2B = gameInfo.B2B; Combo = gameInfo.Combo;
//    }

//    public void Start(int ThinkDelay, int MoveDelay)
//    {
//        Game.TickingCallback = () =>
//        {
//            Done = true;
//        };
//        Thread main = new Thread(() =>
//        {
//            while (!Game.IsDead)
//            {
//                List<ConsoleKey> moves = FindMoves();
//                // Think Delay
//                Thread.Sleep(ThinkDelay);
//                while (moves.Count != 0)
//                {
//                    Game.Play(moves[0]);
//                    moves.RemoveAt(0);
//                    // Move delay
//                    Thread.Sleep(MoveDelay);
//                }
//                Done = false;
//                while (!Done) Thread.Sleep(0);
//                Game.WriteAt(1, 0, ConsoleColor.White, Math.Round(nodec.Average()).ToString().PadRight(9));
//                Game.WriteAt(1, 22, ConsoleColor.White, MaxDepth.ToString().PadRight(6));
//                Game.WriteAt(1, 23, ConsoleColor.White, MoveTresh.ToString().PadRight(12));
//                for (int i = nodec.Length - 1; i > 0; i--)
//                    nodec[i] = nodec[i - 1];
//                nodec[0] = 0;
//            }
//        });
//        main.Priority = ThreadPriority.Highest;
//        main.Start();
//    }

//    int[] SearchScore(int[][] matrix, bool _b2b, int _x, int _y, int piece, int rot, int comb, bool lastrot, out double _trash)
//    {
//        // { scoreadd, B2B, T - spin, combo, clears } //first bit of B2B = B2B chain status
//        // Check for t - spins
//        int tspin = TSpin(matrix, _x, _y, piece, rot, lastrot); //0 = no spin, 2 = mini, 3 = t-spin
//        // Write piece onto screen
//        for (int i = 0; i < 4; i++) matrix[_y + PieceY[piece][rot][i]][_x + PieceX[piece][rot][i]] = piece;
//        // find cleared lines
//        int cleared = 0;
//        int[] clears = new int[4];
//        for (int i = 0; i < 4 && cleared != 4; i++)
//        {
//            if (i > 0) if (PieceY[piece][rot][i] == PieceY[piece][rot][i - 1]) continue;

//            int y = _y + PieceY[piece][rot][i];
//            bool clear = true;
//            for (int x = 0; x < 10 && clear; x++) if (matrix[y][x] == 0) clear = false;
//            if (clear) clears[cleared++] = y;
//        }
//        //clear lines
//        if (cleared != 0)
//        {
//            int movedown = 1;
//            for (int y = clears[cleared - 1] - 1; y > 13; y--)
//            {
//                if (movedown == cleared) matrix[y + movedown] = matrix[y];
//                else if (clears[cleared - movedown - 1] == y) movedown++;
//                else matrix[y + movedown] = matrix[y];
//            }
//            //add new empty rows
//            for (; movedown > 0; movedown--) matrix[13 + movedown] = new int[10];
//        }
//        //combo
//        comb = cleared == 0 ? -1 : comb + 1;
//        //perfect clear
//        bool pc = true;
//        for (int x = 0; x < 10; x++) if (matrix[39][x] != 0) pc = false;
//        //compute score
//        int b2b = _b2b ? 1 : 0;
//        if (tspin == 0 && cleared != 4 && cleared != 0) b2b = 0;
//        else if (tspin + cleared > 3)
//        {
//            if (_b2b) b2b |= 2;
//            b2b |= 1;
//        }

//        int[] info = new int[4 + cleared];
//        info[0] = 0;
//        info[1] = b2b;
//        info[2] = tspin;
//        info[3] = comb;
//        for (int i = 4; i < cleared + 4; i++) info[i] = clears[i - 4];

//        //_trash = new double[] { 0, 0, 1, 2, 4 }[cleared];
//        if (pc) _trash = 5;
//        else _trash = new double[] { 0, 0, 1, 1.5, 5 }[cleared];
//        //if (info[2] == 3) _trash += new double[] { 0, 2, 3, 4 }[cleared];
//        if (info[2] == 3) _trash += new double[] { 0, 2, 5, 8.5 }[cleared];
//        if ((info[1] & 2) == 2) _trash++;
//        //if (pc) _trash += 4;
//        //if (comb > 0) _trash += new double[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 9)];
//        if (comb > 0) _trash += new double[] { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 11)]; //jstris combo table
//        // Modify _trash(use trash sent * APL and offset it slightly)
//        //if (cleared != 0 && comb > 1) _trash = Math.FusedMultiplyAdd(_trash, _trash / cleared, -1.5);

//        return info;
//    }

//    public List<ConsoleKey> FindMoves()
//    {
//        SyncState();

//        // Remove excess cache
//        if (CachedValues.Count < 200000) CachedValues.Clear();
//        if (CachedStateValues.Count < 5000000) CachedStateValues.Clear();

//        // Call the dedicated PC Finder
//        //

//        Timer.Restart();
//        TimesUp = false;

//        List<ConsoleKey> bestMoves = new List<ConsoleKey>();
//        double bestScore = double.MinValue;

//        // Keep searching until time's up or no next pieces left
//        MaxDepth = 0;
//        int[][] vscreen = Clone(Matrix);
//        double out1 = Network.FeedFoward(ExtrFeat(vscreen, 0))[0];
//        for (int depth = 0; !TimesUp && depth < Next.Length; depth++)
//        {
//            CurrentDepth = depth;
//            bool swap;
//            for (int s = 0; s < 2; s++)
//            {
//                swap = s == 1;
//                // Get piece based on swap
//                int piece = swap ? Hold : Current, _hold = swap ? Current : Hold, nexti = 0;
//                if (swap && Hold == 0)
//                {
//                    piece = Next[0];
//                    nexti++;
//                }

//                int xmul, ymul;
//                for (int rot = 0; rot < 4; rot++)
//                {
//                    if (TimesUp) break;

//                    List<ConsoleKey> tempMoves = new List<ConsoleKey>();
//                    if (rot == 3) tempMoves.Add(ConsoleKey.Z);
//                    else for (int i = 0; i < rot; i++) tempMoves.Add(ConsoleKey.UpArrow);

//                    int left = pieceleft[piece][rot], right = pieceright[piece][rot];
//                    for (int i = 0; i < 5 + left; i++) tempMoves.Add(ConsoleKey.LeftArrow);

//                    for (int orix = -left; orix < 10 - right; orix++)
//                    {
//                        if (TimesUp) break;

//                        if (tempMoves.Count == 0) tempMoves.Add(ConsoleKey.RightArrow);
//                        else if (tempMoves[tempMoves.Count - 1] == ConsoleKey.LeftArrow) tempMoves.RemoveAt(tempMoves.Count - 1);
//                        else tempMoves.Add(ConsoleKey.RightArrow);

//                        int oriy = 18;
//                        // Hard drop
//                        while (!OnGround(Matrix, orix, oriy, piece, rot)) oriy++;
//                        // Update screen
//                        double garbage;
//                        int[] info = SearchScore(vscreen, B2B >= 0, orix, oriy, piece, rot, Combo, false, out garbage);
//                        // Check if better
//                        double newvalue = Search(vscreen, depth, nexti + 1, Next[nexti], _hold, false, info[3], (info[1] & 1) == 1, out1, garbage);
//                        if (newvalue >= bestScore)
//                        {
//                            if (newvalue > bestScore || bestMoves.Contains(ConsoleKey.DownArrow) || DistanceFromWall(piece, tempMoves) < DistanceFromWall(swap ^ bestMoves.Contains(ConsoleKey.C) ? piece : _hold, bestMoves))
//                            {
//                                bestScore = newvalue;
//                                bestMoves = new List<ConsoleKey>(tempMoves);
//                                if (swap) bestMoves.Insert(0, ConsoleKey.C);
//                            }
//                        }
//                        Revert(vscreen, piece, rot, orix, oriy, info);
//                        // Only vertical pieces can be spun
//                        if ((rot & 1) == 1 && piece != 7)
//                        {
//                            for (int i = 0; i < 2; i++)
//                            {
//                                bool clockwise = i == 0;

//                                tempMoves.Add(ConsoleKey.DownArrow);
//                                int x = orix, y = oriy, rotate;
//                                int test = RotateTest(Matrix, x, y, piece, rot, clockwise, out xmul, out ymul);
//                                for (rotate = clockwise ? 1 : 3; test != -1; rotate += clockwise ? 1 : -1)
//                                {
//                                    tempMoves.Add(clockwise ? ConsoleKey.UpArrow : ConsoleKey.Z);
//                                    int r = (rot + rotate) % 4;
//                                    // Apply kick
//                                    if (piece == 2)
//                                    {
//                                        x += IKicksX[test] * xmul;
//                                        y += IKicksY[test] * ymul;
//                                    }
//                                    else
//                                    {
//                                        x += KicksX[test] * xmul;
//                                        y += KicksY[test] * ymul;
//                                    }
//                                    if (!OnGround(Matrix, x, y, piece, r)) break;
//                                    // update screen
//                                    info = SearchScore(vscreen, B2B >= 0, x, y, piece, r, Combo, true, out garbage);
//                                    // check if better
//                                    newvalue = Search(vscreen, depth, nexti + 1, Next[nexti], _hold, false, info[3], (info[1] & 1) == 1, out1, garbage);
//                                    if (newvalue > bestScore)
//                                    {
//                                        bestScore = newvalue;
//                                        bestMoves = new List<ConsoleKey>(tempMoves);
//                                        if (swap) bestMoves.Insert(0, ConsoleKey.C);
//                                    }
//                                    // revert screen
//                                    Revert(vscreen, piece, r, x, y, info);

//                                    // only try to spin T pieces twice(for TSTs)
//                                    if (rotate == 2 || piece != 1) break;
//                                    test = RotateTest(Matrix, x, y, piece, r, clockwise, out xmul, out ymul);
//                                }

//                                while (tempMoves[tempMoves.Count - 1] == (clockwise ? ConsoleKey.UpArrow : ConsoleKey.Z)) tempMoves.RemoveAt(tempMoves.Count - 1);
//                                tempMoves.Remove(ConsoleKey.DownArrow);
//                            }
//                        }
//                    }
//                    // o pieces can't be rotated
//                    if (piece == 7)
//                    {
//                        MaxDepth += 1d / 2;
//                        break;
//                    }
//                    else MaxDepth += (1d / 2) / 4;
//                }
//            }
//        }
//        CachedValues.Clear();
//        Timer.Stop();

//        // Check if pc found
//        //
//        // Otherwise use ai's moves
//        bestMoves.Add(ConsoleKey.Spacebar);
//        // Adjust movetresh
//        double time_remaining = (double)(ThinkTicks - Timer.ElapsedTicks) / ThinkTicks;
//        if (time_remaining > 0)
//            MoveTresh *= Math.Pow(MOVEMUL, time_remaining / ((1 + Math.E) / Math.E - time_remaining));
//        else
//            MoveTresh *= Math.Pow(MOVEMUL, MaxDepth - MOVETARGET);
//        MoveTresh = Math.Min(Math.Max(MoveTresh, MINTRESH), MAXTRESH);

//        return bestMoves;
//    }

//    double Search(int[][] matrix, int depth, int nexti, int piece, int _hold, bool swapped, int comb, bool b2b, double prevstate, double _trash)
//    {
//        //if (TimesUp || pc_found) return double.MinValue;
//        if (TimesUp) return double.MinValue;
//        if (Timer.ElapsedTicks > ThinkTicks)
//        {
//            TimesUp = true;
//            return double.MinValue;
//        }

//        if (depth == 0 || nexti == Next.Length)
//        {
//            nodec[0]++;

//            long hash = BitConverter.DoubleToInt64Bits(_trash);
//            for (int x = 0; x < 10; x++)
//                for (int y = 17; y < 40; y++)
//                    if (matrix[y][x] != 0)
//                        hash ^= MatrixHashTable[x][y];
//            if (CachedStateValues.ContainsKey(hash))
//                return CachedStateValues[hash];

//            double[] feat = ExtrFeat(matrix, _trash);
//            double val = Network.FeedFoward(feat)[0];
//            CachedStateValues.Add(hash, val);
//            return val;
//        }
//        else
//        {
//            double _value = double.MinValue;
//            // Have we seen this situation before?
//            long hash = HashBoard(matrix, piece, _hold, nexti, depth);
//            if (CachedValues.ContainsKey(hash)) return CachedValues[hash];
//            // Check if this move should be explored
//            double discount = Discounts[CurrentDepth - depth];
//            double[] outs = Network.FeedFoward(ExtrFeat(matrix, _trash));
//            if (outs[0] - prevstate < MoveTresh)
//            {
//                long statehash = BitConverter.DoubleToInt64Bits(_trash);
//                for (int x = 0; x < 10; x++)
//                    for (int y = 17; y < 40; y++)
//                        if (matrix[y][x] != 0)
//                            statehash ^= MatrixHashTable[x][y];
//                if (!CachedStateValues.ContainsKey(statehash))
//                    CachedStateValues.Add(statehash, outs[0]);
//                return outs[0];
//            }
//            // Check value for swap
//            if (!swapped)
//            {
//                if (Hold == 0)
//                    _value = Math.Max(_value, Search(matrix, depth, nexti + 1, Next[nexti], piece, true, comb, b2b, prevstate, _trash));
//                else
//                    _value = Math.Max(_value, Search(matrix, depth, nexti, _hold, piece, true, comb, b2b, prevstate, _trash));
//                if (CachedValues.ContainsKey(hash))
//                    return CachedValues[hash];
//            }
//            // Check all landing spots
//            int xmul, ymul;
//            for (int rot = 0; rot < 4; rot++)
//            {
//                int left = pieceleft[piece][rot], right = pieceright[piece][rot];

//                for (int orix = -left; orix < 10 - right; orix++)
//                {
//                    if (TimesUp) break;

//                    int oriy = 18;
//                    //Hard drop
//                    while (!OnGround(matrix, orix, oriy, piece, rot)) oriy++;
//                    double garbage;
//                    int[] info;
//                    int new_h;
//                    if ((piece != 5 && piece != 6) || rot < 2)
//                    {
//                        // Update matrix
//                        info = SearchScore(matrix, b2b, orix, oriy, piece, rot, comb, false, out garbage);
//                        // Check if better
//                        _value = Math.Max(_value, Search(matrix, depth - 1, nexti + 1, Next[nexti], _hold, false, info[3], (info[1] & 1) == 1, outs[0], discount * garbage + _trash));
//                        // Revert matrix
//                        Revert(matrix, piece, rot, orix, oriy, info);
//                    }
//                    // Only vertical pieces can be spun
//                    if ((rot & 1) == 1 && piece != 7)
//                    {
//                        for (int i = 0; i < 2; i++)
//                        {
//                            bool clockwise = i == 0;
//                            // Try spin clockwise
//                            int x = orix, y = oriy;
//                            int test = RotateTest(matrix, x, y, piece, rot, clockwise, out xmul, out ymul);
//                            for (int rotate = clockwise ? 1 : 3; test != -1; rotate += clockwise ? 1 : -1)
//                            {
//                                int r = (rot + rotate) % 4;
//                                // Apply kick
//                                if (piece == 2)
//                                {
//                                    x += IKicksX[test] * xmul;
//                                    y += IKicksY[test] * ymul;
//                                }
//                                else
//                                {
//                                    x += KicksX[test] * xmul;
//                                    y += KicksY[test] * ymul;
//                                }
//                                if (!OnGround(matrix, x, y, piece, r)) break;
//                                //update matrix
//                                info = SearchScore(matrix, b2b, x, y, piece, r, comb, true, out garbage);
//                                //check if better
//                                _value = Math.Max(_value, Search(matrix, depth - 1, nexti + 1, Next[nexti], _hold, false, info[3], (info[1] & 1) == 1, outs[0], discount * garbage + _trash));
//                                // Revert matrix
//                                Revert(matrix, piece, r, x, y, info);

//                                // Only try to spin T pieces twice(for TSTs)
//                                if (rotate == 2 || piece != 1) break;
//                                test = RotateTest(matrix, x, y, piece, r, clockwise, out xmul, out ymul);
//                            }
//                        }
//                    }
//                }
//                //o pieces can't be rotated
//                if (piece == 7) break;
//            }

//            CachedValues.Add(hash, _value);
//            return _value;
//        }
//    }

//    static bool OnGround(int[][] matrix, int _x, int _y, int piece, int rot)
//    {
//        try
//        {
//            for (int i = 0; i < 4; i++)
//            {
//                int y = _y + PieceY[piece][rot][i] + 1;
//                if (y == 40) return true;
//                if (matrix[y][_x + PieceX[piece][rot][i]] != 0) return true;
//            }
//        }
//        catch { }
//        return false;
//    }

//    static int RotateTest(int[][] matrix, int _x, int _y, int piece, int rot, bool clockwise, out int xmul, out int ymul)
//    {
//        xmul = !clockwise ^ rot > 1 ^ (rot % 2 == 1 && clockwise) ? -1 : 1;
//        ymul = rot % 2 == 1 ? -1 : 1;
//        int testrotation = clockwise ? (rot + 1) % 4 : (rot + 3) % 4;
//        if (piece == 2) ymul *= rot > 1 ^ clockwise ? -1 : 1;
//        int[] testorder = piece == 2 && (rot % 2 == 1 ^ !clockwise) ? new int[] { 0, 2, 1, 4, 3 } : new int[] { 0, 1, 2, 3, 4 };
//        foreach (int test in testorder)
//        {
//            bool pass = true;
//            for (int i = 0; i < 4 && pass; i++)
//            {
//                int x, y;
//                if (piece == 2)
//                {
//                    x = _x + PieceX[piece][testrotation][i] + (IKicksX[test] * xmul);
//                    y = _y + PieceY[piece][testrotation][i] + (IKicksY[test] * ymul);
//                }
//                else
//                {
//                    x = _x + PieceX[piece][testrotation][i] + (KicksX[test] * xmul);
//                    y = _y + PieceY[piece][testrotation][i] + (KicksY[test] * ymul);
//                }
//                if (x < 0 || x > 9 || y > 39) pass = false;
//                else if (matrix[y][x] != 0) pass = false;
//            }
//            if (pass) return test;
//        }

//        return -1;
//    }

//    int TSpin(int[][] matrix, int _x, int _y, int piece, int rot, bool lastrot)
//    {
//        rot = (rot + 4) % 4;
//        if (lastrot && piece == 1)
//        {
//            bool[] corners = new bool[4]; //{ top left, top right, bottom left, bottom right }
//            if (_x - 1 < 0)
//            {
//                corners[0] = true;
//                corners[2] = true;
//            }
//            else
//            {
//                corners[0] = matrix[_y - 1][_x - 1] != 0;
//                if (_y + 1 > 39) corners[2] = true;
//                else corners[2] = matrix[_y + 1][_x - 1] != 0;
//            }
//            if (_x + 1 > 9)
//            {
//                corners[1] = true;
//                corners[3] = true;
//            }
//            else
//            {
//                corners[1] = matrix[_y - 1][_x + 1] != 0;
//                if (_y + 1 > 39) corners[3] = true;
//                else corners[3] = matrix[_y + 1][_x + 1] != 0;
//            }

//            //3 corner rule
//            int count = 0;
//            foreach (bool corner in corners) if (corner) count++;
//            if (count > 2)
//            {
//                //check if mini
//                switch (rot)
//                {
//                    case 0:
//                        {
//                            if (matrix[_y - 1][_x - 1] == 0 || matrix[_y - 1][_x + 1] == 0) return 2;
//                            break;
//                        }
//                    case 1:
//                        {
//                            if (matrix[_y + 1][_x + 1] == 0 || matrix[_y - 1][_x + 1] == 0) return 2;
//                            break;
//                        }
//                    case 2:
//                        {
//                            if (matrix[_y + 1][_x - 1] == 0 || matrix[_y + 1][_x + 1] == 0) return 2;
//                            break;
//                        }
//                    case 3:
//                        {
//                            if (matrix[_y - 1][_x - 1] == 0 || matrix[_y + 1][_x - 1] == 0) return 2;
//                            break;
//                        }
//                }
//                return 3;
//            }
//        }
//        return 0;
//    }

//    int DistanceFromWall(int piece, List<ConsoleKey> _moves)
//    {
//        int _x = 4, _r = 0;
//        foreach (ConsoleKey key in _moves)
//        {
//            if (key == ConsoleKey.LeftArrow) _x--;
//            else if (key == ConsoleKey.RightArrow) _x++;
//            else if (key == ConsoleKey.UpArrow) _r++;
//            else if (key == ConsoleKey.Z) _r += 3;
//            else if (key == ConsoleKey.DownArrow) break;
//        }
//        //_r %= 4;
//        if (piece == 7) _r = 0;
//        return Math.Min(_x + pieceleft[piece][_r], _x - 9 + pieceright[piece][_r]);
//    }

//    void Revert(int[][] matrix, int piece, int rot, int _x, int _y, int[] info)
//    {
//        //add cleared lines
//        if (info.Length != 4)
//        {
//            int moveup = info.Length - 4;
//            for (int y = 13; moveup != 0; y++)
//            {
//                if (y == info[info.Length - moveup])
//                {
//                    moveup--;
//                    matrix[y] = new int[10];
//                    for (int x = 0; x < 10; x++) matrix[y][x] = 8;
//                }
//                else matrix[y] = matrix[y + moveup];
//            }
//        }
//        //remove piece
//        for (int i = 0; i < 4; i++) matrix[_y + PieceY[piece][rot][i]][_x + PieceX[piece][rot][i]] = 0;
//    }

//    double[] ExtrFeat(int[][] matrix, double _trash)
//    {
//        // Find heightest block in each column
//        double[] heights = new double[10];
//        for (int x = 0; x < 10; x++)
//        {
//            double height = 20;
//            for (int y = 20; matrix[y][x] == 0; y++)
//            {
//                height--;
//                if (y == 39) break;
//            }
//            heights[x] = height;
//        }
//        // Standard height
//        double h = 0;
//        if (Network.Visited[0])
//        {
//            foreach (double height in heights) h += height * height;
//            h = Math.Sqrt(h);
//        }
//        // "caves"
//        double caves = 0;
//        if (Network.Visited[1])
//        {
//            for (int y = 39 - (int)heights[0]; y < 40; y++) if (matrix[y][0] == 0 && matrix[y - 1][0] != 0) if (y < 39 - heights[1]) caves += heights[0] + y - 39;
//            for (int x = 1; x < 9; x++) for (int y = 39 - (int)heights[x]; y < 40; y++) if (matrix[y][x] == 0 && matrix[y][x] != 0) if (y <= Math.Min(39 - heights[x - 1], 39 - heights[x + 1])) caves += heights[x] + y - 39;
//            for (int y = 39 - (int)heights[9]; y < 40; y++) if (matrix[y][9] == 0 && matrix[y][9] != 0) if (y <= 39 - heights[8]) caves += heights[9] + y - 39;
//        }
//        // Pillars
//        double pillars = 0;
//        if (Network.Visited[2])
//        {
//            for (int x = 0; x < 10; x++)
//            {
//                double diff;
//                // Don't punish for tall towers at the side
//                if (x != 0 && x != 9) diff = Math.Min(Math.Abs(heights[x - 1] - heights[x]), Math.Abs(heights[x + 1] - heights[x]));
//                else diff = x == 0 ? Math.Max(0, heights[1] - heights[0]) : Math.Min(0, heights[8] - heights[9]);
//                if (diff > 2) pillars += diff * diff;
//                else pillars += diff;
//            }
//        }
//        // Row trasitions
//        double rowtrans = 0;
//        if (Network.Visited[3])
//        {
//            for (int y = 19; y < 40; y++)
//            {
//                bool empty = matrix[y][0] == 0;
//                for (int x = 1; x < 10; x++)
//                {
//                    bool isempty = matrix[y][x] == 0;
//                    if (empty ^ isempty)
//                    {
//                        rowtrans++;
//                        empty = isempty;
//                    }
//                }
//            }
//        }
//        // Column trasitions
//        double coltrans = 0;
//        if (Network.Visited[4])
//        {
//            for (int x = 0; x < 10; x++)
//            {
//                bool empty = matrix[19][x] == 0;
//                for (int y = 20; y < 40; y++)
//                {
//                    bool isempty = matrix[y][x] == 0;
//                    if (empty ^ isempty)
//                    {
//                        coltrans++;
//                        empty = isempty;
//                    }
//                }
//            }
//        }

//        return new double[] { h, caves, pillars, rowtrans, coltrans, _trash };
//    }

//    int[][] Clone(int[][] array)
//    {
//        int[][] clone = new int[40][];
//        for (int i = 0; i < 40; i++)
//        {
//            clone[i] = new int[10];
//            Buffer.BlockCopy(array[i], 0, clone[i], 0, sizeof(int) * 10);
//        }

//        return clone;
//    }

//    long HashBoard(int[][] matrix, int piece, int _hold, int nexti, int depth)
//    {
//        long hash = PieceHashTable[piece] ^ HoldHashTable[_hold];
//        for (int i = 0; nexti + i < Next.Length && i < depth; i++) hash ^= NextHashTable[i][Next[nexti + i]];
//        for (int x = 0; x < 10; x++) for (int y = 17; y < 40; y++) if (matrix[y][x] != 0) hash ^= MatrixHashTable[x][y];
//        return hash;
//    }

//    bool MatrixToUlong(int[][] matrix, out ulong ulong_matrix)
//    {
//        //pc_moves = null;
//        ulong_matrix = 0;
//        for (int y = 17; y < matrix.Length - 4; y++) for (int x = 0; x < matrix[0].Length; x++) if (matrix[y][x] != 0) return false;
//        for (int i = 0; i < 40; i++)
//        {
//            if (matrix[(i / 10) + matrix.Length - 4][i % 10] != 0) ulong_matrix |= 1ul << (39 - i);
//        }
//        return true;
//    }
//}

//enum ActivationType
//{
//    Sigmoid,
//    TanH,
//    ReLU,
//    LeakyReLU,
//    ELU,
//    SELU,
//    SoftPlus,
//}

//class NN
//{
//    internal class Node
//    {
//        public List<Node> Network;
//        public double Value = 0;
//        public int Id;
//        public List<Connection> Inputs, Outputs;
//        readonly Func<double, double> Activation;

//        public Node(int id, List<Node> network, ActivationType activationType, List<Connection> inputs = null, List<Connection> outputs = null)
//        {
//            Id = id;
//            Network = network;
//            Inputs = inputs ?? new List<Connection>();
//            Outputs = outputs ?? new List<Connection>();
//            switch (activationType)
//            {
//                case ActivationType.Sigmoid:
//                    Activation = Sigmoid;
//                    break;
//                case ActivationType.TanH:
//                    Activation = TanH;
//                    break;
//                case ActivationType.ReLU:
//                    Activation = ReLU;
//                    break;
//                case ActivationType.LeakyReLU:
//                    Activation = LeakyReLU;
//                    break;
//                case ActivationType.ELU:
//                    Activation = ELU;
//                    break;
//                case ActivationType.SELU:
//                    Activation = SELU;
//                    break;
//                case ActivationType.SoftPlus:
//                    Activation = SoftPlus;
//                    break;
//                default:
//                    throw new ArgumentOutOfRangeException(nameof(activationType), activationType, null);
//            }
//        }

//        public double UpdateValue()
//        {
//            // sum activations * weights
//            Value = 0;
//            foreach (Connection c in Inputs)
//                if (c.Enabled)
//                    Value += Network[c.Input].Value * c.Weight;
//            Value = Activation(Value);

//            return Value;
//        }

//        #region // Activation functions
//        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
//        static double TanH(double x) => Math.Tanh(x);
//        static double ReLU(double x) => x >= 0 ? x : 0;
//        static double LeakyReLU(double x) => x >= 0 ? x : 0.01 * x;
//        static double ELU(double x) => x >= 0 ? x : Math.Exp(x) - 1;
//        static double SELU(double x) => 1.050700987355480 * (x >= 0 ? x : 1.670086994173469 * (Math.Exp(x) - 1));
//        static double SoftPlus(double x) => Math.Log(1 + Math.Exp(x));
//        #endregion
//    }

//    internal class Connection
//    {
//        public bool Enabled;
//        public int Input, Output, Id;
//        public double Weight;

//        public Connection(int input, int output, double weight, bool enabled = true)
//        {
//            Input = input;
//            Output = output;
//            Weight = weight;
//            Enabled = enabled;
//            // Assign the id according to the input and output nodes
//            Id = -1;
//            for (int i = 0; i < InNodes.Count && Id == -1; i++)
//                if (InNodes[i] == Input && OutNodes[i] == Output)
//                    Id = i;
//            if (Id == -1)
//            {
//                Id = InNodes.Count;
//                InNodes.Add(Input);
//                OutNodes.Add(Output);
//            }
//        }

//        public Connection Clone()
//        {
//            Connection clone = new Connection(Input, Output, Weight);
//            if (!Enabled) clone.Enabled = false;
//            return clone;
//        }
//    }

//    public int InputCount { get; private set; }
//    public int OutputCount { get; private set; }
//    private static readonly List<int> InNodes = new List<int>(), OutNodes = new List<int>();
//    readonly List<Node> Nodes = new List<Node>();
//    public bool[] Visited = Array.Empty<bool>();
//    readonly List<int> ConnectionIds = new List<int>();
//    readonly Dictionary<int, Connection> Connections = new Dictionary<int, Connection>();

//    NN(int inputs, int outputs, List<Connection> connections)
//    {
//        InputCount = inputs + 1; // Add 1 for bias
//        OutputCount = outputs;
//        foreach (Connection c in connections)
//        {
//            // Add connection to connection tracking lists
//            Connection newc = c.Clone();
//            ConnectionIds.Add(newc.Id);
//            Connections.Add(newc.Id, newc);
//            // Add nodes as nescessary
//            while (Nodes.Count <= newc.Input || Nodes.Count <= newc.Output)
//                Nodes.Add(new Node(Nodes.Count, Nodes, ActivationType.ReLU));
//            // Add connection to coresponding nodes
//            Nodes[c.Input].Outputs.Add(newc);
//            Nodes[c.Output].Inputs.Add(newc);
//        }
//        // Find all connected nodes
//        FindConnectedNodes();
//    }

//    public double[] FeedFoward(double[] input)
//    {
//        // Set input nodes
//        for (int i = 0; i < InputCount - 1; i++)
//            if (Visited[i])
//                Nodes[i].Value = input[i];
//        Nodes[InputCount - 1].Value = 1; // Bias node
//        // Update hidden all nodes
//        for (int i = InputCount + OutputCount; i < Nodes.Count; i++)
//            if (Visited[i])
//                Nodes[i].UpdateValue();
//        // Update ouput nodes and get output
//        double[] output = new double[OutputCount];
//        for (int i = 0; i < OutputCount; i++) output[i] = Nodes[i + InputCount].UpdateValue();

//        return output;
//    }

//    void FindConnectedNodes()
//    {
//        Visited = new bool[Nodes.Count];
//        for (int i = InputCount; i < InputCount + OutputCount; i++)
//            if (!Visited[i])
//                VisitDownstream(i);

//        void VisitDownstream(int i)
//        {
//            Visited[i] = true;
//            foreach (Connection c in Nodes[i].Inputs)
//                if (!Visited[c.Input] && c.Enabled)
//                    VisitDownstream(c.Input);
//        }
//    }

//    public static NN LoadNN(string path)
//    {
//        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
//        string[] inout = lines[1].Split(' ');
//        List<Connection> cons = new List<Connection>();
//        for (int i = 2; i < lines.Length; i++)
//        {
//            string[] split = lines[i].Split(' ');
//            if (split.Length != 4)
//                cons.Add(new Connection(Convert.ToInt32(split[0]), Convert.ToInt32(split[1]), Convert.ToDouble(split[2])));
//        }

//        return new NN(Convert.ToInt32(inout[0]), Convert.ToInt32(inout[1]), cons);
//    }
//}
