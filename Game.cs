namespace Tetris;

using FastConsole;
using Masks;
using System.Diagnostics;
using System.Windows.Input;

enum Moves
{
    None,
    Hold,
    Left,
    Right,
    DASLeft,
    DASRight,
    RotateCW,
    RotateCCW,
    Rotate180,
    SoftDrop,
    HardDrop
}

enum TargetModes
{
    Random,
    All,
    Self
}

class GameBase
{
    protected Matrix10x24 _Matrix = new Matrix10x24();
    public Matrix10x24 Matrix {
        get => _Matrix;
        protected set => _Matrix = value; 
    }
    protected int X, Y;
    protected int R
    {
        get => Current & Piece.ROTATION_BITS;
        set => Current = (Current & Piece.PIECE_BITS) | (value & Piece.ROTATION_BITS);
    }

    protected Piece _Current;
    public Piece Current { 
        get => _Current; 
        protected set => _Current = value; 
    }
    public Piece[] Next { get; protected set; }
    public int NextLength { get => Next.Length; }

    private readonly Random PieceRand;
    protected int BagIndex;
    protected Piece[] Bag = new Piece[] { Piece.T, Piece.I, Piece.L, Piece.J, Piece.S, Piece.Z, Piece.O };

    public virtual long Score { get; set; } = 0;
    public virtual long Lines { get; set; } = 0;
    public virtual long Level
    {
        get => Lines / 10 + 1;
        set { }
    }
    public int B2B { get; protected set; } = -1;
    public int Combo { get; protected set; } = -1;

    public double G = 0.02, SoftG = 40;
    protected double Vel = 0;

    public GameBase(int next_length, int seed)
    {
        BagIndex = Bag.Length;
        Next = new Piece[next_length];
        PieceRand = new Random(seed);

        for (int i = 0; i < Next.Length; i++) Next[i] = NextPiece();
    }

    protected Piece NextPiece()
    {
        if (BagIndex == Bag.Length)
        {
            // Re-shuffle bag
            // Each permutation has an equal chance of appearing by right
            for (int i = 0; i < Bag.Length; i++)
            {
                int swapIndex = PieceRand.Next(Bag.Length - i) + i;
                (Bag[i], Bag[swapIndex]) = (Bag[swapIndex], Bag[i]);
            }
            BagIndex = 0;
        }

        return Bag[BagIndex++];
    }

    protected bool TrySlide(int dx)
    {
        bool right = dx > 0, moved = false;
        while (dx != 0)
        {
            if (right)
            {
                if (X <= 0) return moved;
                if (Matrix.Collides(Current, X - 1, Y)) return moved;
                X--;
            }
            else
            {
                if (X >= Current.MaxX) return moved;
                if (Matrix.Collides(Current, X + 1, Y)) return moved;
                X++;
            }
            dx -= right ? 1 : -1;
            moved = true;
        }
        
        return moved;
    }

    protected bool TryRotate(int dr)
    {
        dr &= 3;
        if (dr == 1)
            return Matrix.TryRotateCW(ref _Current, ref X, ref Y);
        else if (dr == 2)
            return Matrix.TryRotate180(ref _Current, ref X, ref Y);
        else if (dr == 3)
            return Matrix.TryRotateCCW(ref _Current, ref X, ref Y);

        return false;
    }

    /// <returns>The actual number of cells moved down by.</returns>
    protected int TryDrop(int dy)
    {
        int moved = 0;
        for ( ; dy > 0 && !Matrix.OnGround(Current, X, Y); dy--, Y--) moved++;
        return moved;
    }

    protected void ResetPiece()
    {
        // Move new piece to top
        X = Current.StartX; Y = Current.StartY;
        R = Piece.ROTATION_NONE;
        // Drop immediately if possible
        if (!Matrix.OnGround(Current, X, Y)) Y--;
    }

    /// <returns>The number of lines cleared</returns>
    protected unsafe int PlaceAndClear()
    {
        Matrix.MoveToGround(Current, X, ref Y);
        return Matrix.PlaceAndClear(Current, X, Y);
    }
}

class Game : GameBase
{
    public const int GAMEWIDTH = 45, GAMEHEIGHT = 24;
    const string BLOCKSOLID = "██", BLOCKGHOST = "▒▒";
    static readonly string[] ClearText = { "SINGLE", "DOUBLE", "TRIPLE", "TETRIS" };
    static readonly ConsoleColor[] PieceColors = {
        ConsoleColor.Black,         // Empty
        ConsoleColor.Magenta,       // T
        ConsoleColor.Cyan,          // I
        ConsoleColor.DarkYellow,    // L
        ConsoleColor.DarkBlue,      // J
        ConsoleColor.Green,         // S
        ConsoleColor.DarkRed,       // Z
        ConsoleColor.Yellow,        // O
        ConsoleColor.Gray,          // Garbage
        ConsoleColor.DarkGray       // Bedrock
    };
    const ConsoleColor GARBAGE = ConsoleColor.Gray, BEDROCK = ConsoleColor.DarkGray;

    long LockDelay = 1000, EraseDelay = 1000, GarbageDelay = 500; // In miliseconds
    int AutoLockGrace = 15; // In moves

    public int[] LinesTrash { get; private set; } = { 0, 0, 1, 2, 4 };
    public  int[] TSpinsTrash { get; private set; } = { 0, 2, 4, 6 };
    // Jstris combo table = { 0, 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }
    public int[] ComboTrash { get; private set; } = { 0, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 };
    public int[] PCTrash { get; private set; } = { 0, 10, 10, 10, 10 };

    public static Game[] Games { get; private set; }
    public static readonly Stopwatch GlobalTime = Stopwatch.StartNew();
    public static bool IsPausedGlobal { get; private set; } = false;

    #region // Fields and Properties
    protected int XOffset = 0;
    protected int YOffset = 0;

    public Action TickingCallback;
    public bool IsTicking { get; private set; } = false;

    private bool _IsDead = true;
    public virtual bool IsDead
    {
        get => _IsDead;
        private set
        {
            if (_IsDead == value) return;

            _IsDead = value;
            if (_IsDead)
            {
                EraseClearStats();
                DrawTrashMeter();
            }
        }
    }

    public Piece Hold { get; private set; } = Piece.EMPTY;
    protected ConsoleColor[][] MatrixColors = new ConsoleColor[24][];

    private long _Score = 0;
    public override long Score
    {
        get => _Score;
        set
        {
            _Score = value;
            WriteAt(2, 9, ConsoleColor.White, _Score.ToString().PadRight(8));
        }
    }
    private long _Lines = 0;
    public override long Lines
    {
        get => _Lines;
        set
        {
            _Lines = value;
            WriteAt(26, 0, ConsoleColor.White, _Lines.ToString().PadRight(45 - 26));
        }
    }

    protected readonly Queue<Moves> MoveQueue = new Queue<Moves>();

    double LastFrameT;
    int MoveCount = 0;
    bool IsLastMoveRotate = false, AlreadyHeld = false;
    readonly Stopwatch LockT = new Stopwatch(),
                       EraseT = new Stopwatch();

    readonly Random GarbageRand;
    int GarbageLock = 0;
    readonly List<(int, long)> Garbage = new List<(int, long)>();

    int GameIndex;
    double TargetChangeInteval = 0.5, LastTargetChangeT;
    TargetModes TargetMode = TargetModes.Random;
    readonly List<Game> Targets = new List<Game>();
    #endregion

    public Game() : base(6, Guid.NewGuid().GetHashCode())
    {
        GarbageRand = new Random(Guid.NewGuid().GetHashCode());
        for (int i = 0; i < MatrixColors.Length; i++) MatrixColors[i] = new ConsoleColor[10];
    }

    public Game(int next_length, int seed) : base(next_length, seed)
    {
        GarbageRand = new Random(seed.GetHashCode());
        for (int i = 0; i < MatrixColors.Length; i++) MatrixColors[i] = new ConsoleColor[10];
    }

    public void Initialise()
    {
        IsDead = false;
        SpawnNextPiece();
        // Timekeeping
        LastFrameT = GlobalTime.Elapsed.TotalSeconds;
        LastTargetChangeT = GlobalTime.Elapsed.TotalSeconds;
    }

    public void Restart()
    {
        Matrix = new Matrix10x24();
        for (int i = 0; i < MatrixColors.Length; i++) MatrixColors[i] = new ConsoleColor[MatrixColors[i].Length];
        BagIndex = Bag.Length;
        for (int i = 0; i < Next.Length; i++) Next[i] = NextPiece();
        Hold = Piece.EMPTY;

        B2B = -1; Combo = -1;

        IsDead = false;
        Score = 0;
        Lines = 0;

        Vel = 0;
        IsLastMoveRotate = false;
        AlreadyHeld = false;
        MoveCount = 0;
        MoveQueue.Clear();

        LastFrameT = GlobalTime.Elapsed.TotalSeconds;
        LastTargetChangeT = GlobalTime.Elapsed.TotalSeconds;
        LockT.Reset();
        EraseT.Reset();
        Garbage.Clear();
        //TargetMode = TargetModes.Random;
        Targets.Clear();

        SpawnNextPiece();
        DrawAll();
    }

    public unsafe void TickAsync()
    {
        if (IsDead || IsPausedGlobal) return;

        IsTicking = true;
        new Thread(() =>
        {
            fixed (ulong* ptr = &_Matrix.Item0)
            {
                _Matrix.Ptr = (int*)ptr;
                IntPtr a = new IntPtr(ptr);
                WriteAt(0, 23, ConsoleColor.White, a.ToString().PadRight(16));

                // Timekeeping
                double deltaT = GlobalTime.Elapsed.TotalSeconds - LastFrameT;
                LastFrameT = GlobalTime.Elapsed.TotalSeconds;

                // Erase stats
                if (EraseT.ElapsedMilliseconds > EraseDelay) EraseClearStats();

                // Play queued moves (it's up to the input adapter to time the moves, the queue is a buffer jic)
                bool softDrop = false;
                while (MoveQueue.Count != 0 && !IsDead)
                {
                    switch (MoveQueue.Dequeue())
                    {
                        case Moves.Hold:
                            HoldPiece();
                            break;
                        case Moves.Left:
                            Slide(-1);
                            break;
                        case Moves.Right:
                            Slide(1);
                            break;
                        case Moves.DASLeft:
                            Slide(-10);
                            break;
                        case Moves.DASRight:
                            Slide(10);
                            break;
                        case Moves.SoftDrop:
                            softDrop = true;
                            Drop((int)SoftG, 1);
                            Vel += SoftG - Math.Floor(SoftG);
                            break;
                        case Moves.HardDrop:
                            Drop(40, 2);
                            PlacePiece();
                            break;
                        case Moves.RotateCW:
                            Rotate(1);
                            break;
                        case Moves.RotateCCW:
                            Rotate(-1);
                            break;
                        case Moves.Rotate180:
                            Rotate(2);
                            break;
                    }
                }

                // Handle locking and gravity
                Vel += G * deltaT * FConsole.Framerate;
                if (Matrix.OnGround(Current, X, Y))
                {
                    Vel = 0;
                    if (MoveCount > AutoLockGrace) PlacePiece(); // Lock piece
                    LockT.Start();
                }
                else
                {
                    if (MoveCount < AutoLockGrace) LockT.Reset();
                    Vel -= Drop((int)Vel, softDrop ? 1 : 0); // Round Vel down
                }
                if (LockT.ElapsedMilliseconds > LockDelay)
                    if (Matrix.OnGround(Current, X, Y))
                        PlacePiece();

                DrawTrashMeter();

                // Ded
                //if (IsDead)
                //{
                //    Restart();
                //}

                IsTicking = false;
                TickingCallback?.Invoke();
                
                WriteAt(0, 22, ConsoleColor.White, Matrix.H.ToString().PadRight(2));
                FConsole.WriteAt(Matrix.ToString().Substring(88), 13, 3);
            }
        }).Start();
    }

    public static void SetGames(Game[] games)
    {
        Games = games;
        GlobalTime.Restart();

        // Find width and height (2:1 ratio)
        int width = (int)Math.Sqrt(Games.Length / 2) * 2, height = width / 2;
        if (width * height < Games.Length) width++;
        if (width * height < Games.Length) height++;

        FConsole.Set(width * GAMEWIDTH + 1, height * GAMEHEIGHT + 1, width * GAMEWIDTH + 1, height * GAMEHEIGHT + 1);

        foreach (Game g in Games) g.ClearScreen();

        // Set up and re-draw games
        for (int i = 0; i < Games.Length; i++)
        {
            Games[i].XOffset = (i % width) * GAMEWIDTH;
            Games[i].YOffset = (i / width) * GAMEHEIGHT + 1;
            Games[i].GameIndex = i;
            if (Games[i].IsDead) Games[i].Initialise();
            Games[i].DrawAll();
        }
    }

    public static List<Game> GetAliveGames()
    {
        List<Game> games = new List<Game>();
        foreach (Game g in Games)
            if (!g.IsDead)
                games.Add(g);
        return games;
    }

    public void SendTrash(int trash)
    {
        long time = GlobalTime.ElapsedMilliseconds;
        // Select targets
        switch (TargetMode)
        {
            case TargetModes.Random:
                if (time - LastTargetChangeT > TargetChangeInteval)
                {
                    LastTargetChangeT = time;
                    Targets.Clear();
                    List<Game> aliveGames = GetAliveGames();
                    if (aliveGames.Count <= 1) break;
                    int i = new Random().Next(aliveGames.Count - 1);
                    GameIndex = aliveGames.IndexOf(this);
                    if (i >= GameIndex) i = (i + 1) % aliveGames.Count;
                    Targets.Add(aliveGames[i]);
                }
                break;
            case TargetModes.All:
                Targets.Clear();
                foreach (Game g in GetAliveGames())
                    Targets.Add(g);
                break;
            case TargetModes.Self:
                Targets.Clear();
                Targets.Add(this);
                break;
        }
        // Send trash
        foreach (Game victim in Targets)
        {
            victim.Garbage.Add((trash, time));
            victim.DrawTrashMeter();
        }
    }

    void SpawnNextPiece()
    {
        // Undraw next
        for (int i = 0; i < Math.Min(6, Next.Length); i++)
            DrawPiece(Next[i], 38, 3 + 3 * i, 0, true);
        // Update current and next
        Current = Next[0];
        for (int i = 1; i < Next.Length; i++) Next[i - 1] = Next[i];
        Next[Next.Length - 1] = NextPiece();
        // Reset piece
        ResetPiece();
        IsLastMoveRotate = false;
        AlreadyHeld = false;
        Vel = 0;
        MoveCount = 0;
        LockT.Restart();
        // Check for block out
        if (Matrix.Collides(Current, X, Y)) IsDead = true;
        // Check for lock out
        if (Y >= 20) IsDead = true;
        // Draw next
        for (int i = 0; i < Math.Min(6, Next.Length); i++)
            DrawPiece(Next[i], 38, 3 + 3 * i, 0, false);
        // Draw current
        DrawCurrent(false);
    }

    #region // Player methods
    public void Play(Moves move)
    {
        MoveQueue.Enqueue(move);
    }

    public void Slide(int dx)
    {
        DrawCurrent(true);
        if (TrySlide(dx))
        {
            IsLastMoveRotate = false;
            if (MoveCount++ < AutoLockGrace) LockT.Restart();
        }
        DrawCurrent(false);
    }

    public int Drop(int dy, int scorePerDrop)
    {
        DrawCurrent(true);
        int moved = TryDrop(dy);
        Score += moved * scorePerDrop;
        if (moved != 0) IsLastMoveRotate = false;
        DrawCurrent(false);

        return moved;
    }

    public void Rotate(int dr)
    {
        DrawCurrent(true);
        if (TryRotate(dr))
        {
            IsLastMoveRotate = true;
            if (MoveCount++ < AutoLockGrace) LockT.Restart();
        }
        DrawCurrent(false);
    }

    public void HoldPiece()
    {
        if (!AlreadyHeld)
        {
            AlreadyHeld = true;
            IsLastMoveRotate = false;

            // Undraw
            DrawCurrent(true);
            DrawPiece(Hold, 4, 3, 0, true);

            int oldhold = Hold & Piece.PIECE_BITS;
            Hold = Current & Piece.PIECE_BITS;
            MoveCount = 0;
            if (oldhold == Piece.EMPTY) SpawnNextPiece();
            else
            {
                Current = oldhold;
                ResetPiece();
            }

            // Redraw
            DrawPiece(Hold, 4, 3, 0, false);
            DrawCurrent(false);
            LockT.Restart();
        }
    }

    public unsafe void PlacePiece()
    {
        int tspin = Matrix.GetTSpinKind(IsLastMoveRotate, Current, X, Y); //0 = no spin, 2 = mini, 3 = t-spin
        // Place piece
        for (int x = 0; x < 10 - Current.MaxX; x++)
            for (int y = 0; y < Current.H; y++)
                if ((Current.Raw >> (10 * y + x) & 1) == 1)
                    MatrixColors[y + Y][x + X] = PieceColors[Current & Piece.PIECE_BITS];
        // Clear lines
        PlaceAndClear();
        int cleared = 0;
        for (int i = 0; i < Current.H; i++)
        {
            // Check if line is full
            bool line_cleared = true;
            for (int j = 0; j < 10 && line_cleared; j++)
                if (MatrixColors[i + Y - cleared][j] == PieceColors[Piece.EMPTY])
                    line_cleared = false;
            if (!line_cleared) continue;

            for (int j = i - cleared; j < Current.H; j++)
                MatrixColors[j + Y] = MatrixColors[j + Y + 1];
            cleared++;
        }
        for (int i = Y + Current.H - cleared; i < MatrixColors.Length; i++)
        {
            MatrixColors[i] = (i + cleared >= MatrixColors.Length) ?
                            new ConsoleColor[MatrixColors[i].Length] :
                            MatrixColors[i + cleared];
        }
        // Line clears
        int scoreadd = 0;
        scoreadd += new int[] { 0, 100, 300, 500, 800 }[cleared];
        // T-spins
        if (tspin == 3) scoreadd += new int[] { 400, 700, 900, 1100 }[cleared];
        else if (tspin == 2) scoreadd += 100;
        // Perfect clear
        bool pc = (Matrix[0] & 0b1111111111) == 0;
        if (pc) scoreadd += new int[] { 800, 1200, 1800, 2000 }[cleared - 1];
        // B2B
        bool B2Bbonus = (tspin + cleared > 3) && B2B > -1;
        if (tspin == 0 && cleared != 4 && cleared != 0) B2B = -1; // Reset B2B
        else if (tspin + cleared > 3)
        {
            B2B++;
            if (B2Bbonus) scoreadd += scoreadd / 2; //B2B bonus
        }
        // Combo
        Combo = cleared == 0 ? -1 : Combo + 1;
        if (Combo > -1) scoreadd += 50 * Combo;
        // Score
        Score += scoreadd * Level;
        // Check if leveled up
        Lines += cleared;
        if (Level % 10 < cleared) // Assumes cleared < 10
        {

        }

        // Write stats to console
        WriteAt(2, 11, ConsoleColor.White, Level.ToString());
        // Write clear stats and play sound
        if (tspin > 0 || cleared > 0 || Combo > 0 || pc) EraseClearStats();
        if (B2Bbonus) WriteAt(5, 14, ConsoleColor.White, "B2B");
        if (tspin == 2) WriteAt(1, 15, ConsoleColor.White, "T-SPIN MINI");
        else if (tspin == 3) WriteAt(3, 15, ConsoleColor.White, "T-SPIN");
        if (cleared > 0) WriteAt(3, 16, ConsoleColor.White, ClearText[cleared - 1]);
        if (Combo > 0) WriteAt(2, 17, ConsoleColor.White, Combo + " COMBO!");
        if (pc) WriteAt(1, 18, ConsoleColor.White, "ALL CLEAR!");

        // Trash sent
        int trash;
        if (pc) trash = PCTrash[cleared];
        else if (tspin == 3) trash = TSpinsTrash[cleared];
        else trash = LinesTrash[cleared];
        if (B2Bbonus) trash++;
        if (Combo > 0) trash += ComboTrash[Math.Min(Combo, ComboTrash.Length - 1)];

        // Garbage
        try
        {
            // Aquire lock
            while (1 == Interlocked.Exchange(ref GarbageLock, 1)) Thread.Sleep(0);
            // Garbage cancelling
            while (Garbage.Count != 0 && trash != 0)
            {
                if (Garbage[0].Item1 <= trash)
                {
                    trash -= Garbage[0].Item1;
                    Garbage.RemoveAt(0);
                }
                else
                {
                    Garbage[0] = (Garbage[0].Item1 - trash, Garbage[0].Item2);
                    trash = 0;
                }
            }
            // Dump the trash
            if (cleared == 0)
            {
                while (Garbage.Count != 0)
                {
                    if (GlobalTime.ElapsedMilliseconds - Garbage[0].Item2 < GarbageDelay) break;

                    int linesToAdd = Garbage[0].Item1;
                    Garbage.RemoveAt(0);
                    int hole = GarbageRand.Next(10);
                    for (int y = MatrixColors.Length - 1; y >= linesToAdd; y--) MatrixColors[y] = MatrixColors[y - linesToAdd]; // Move stuff up
                    for (int y = 0; y < linesToAdd; y++)
                    {
                        // Make new empty row
                        MatrixColors[y] = new ConsoleColor[10];
                        // Fill it with trash
                        for (int x = 0; x < 10; x++)
                            MatrixColors[y][x] = x == hole ? PieceColors[Piece.EMPTY] : GARBAGE;
                    }
                }
                Matrix = new Matrix10x24(MatrixColors, PieceColors[Piece.EMPTY]);
            }
        }
        finally
        {
            // Release lock
            Interlocked.Exchange(ref GarbageLock, 0);
        }
        SendTrash(trash);

        // Redraw board
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 20; y++)
                WriteAt(31 - (x * 2), 21 - y, MatrixColors[y][x], BLOCKSOLID);

        SpawnNextPiece();
    }
    #endregion

    #region // Drawing methods
    public void WriteAt(int x, int y, ConsoleColor color, string text) =>
        FConsole.WriteAt(text, x + XOffset, y + YOffset, foreground: color);

    void DrawTrashMeter()
    {
        int y = 21;
        if (Garbage.Count != 0)
        {
            for (int i = 0; i < Garbage.Count; i++)
            {
                ConsoleColor color = (GlobalTime.ElapsedMilliseconds - Garbage[i].Item2 > GarbageDelay) ?
                                     ConsoleColor.Red :
                                     ConsoleColor.Gray;
                for (int j = y; y > j - Garbage[i].Item1 && y > 1; y--)
                {
                    WriteAt(34, y, color, "█");
                }
            }
        }
        for (; y >= 0; y--) WriteAt(34, y, ConsoleColor.Black, " ");
    }

    void EraseClearStats()
    {
        for (int i = 14; i < 19; i++) WriteAt(1, i, ConsoleColor.Black, "".PadLeft(11));
        EraseT.Restart();
    }

    void DrawAll()
    {
        ClearScreen();
        if (IsDead) return;

        #region // Draw outlines
        WriteAt(18, 0, ConsoleColor.White, "LINES - ");
        WriteAt(26, 0, ConsoleColor.White, Lines.ToString().PadRight(45 - 26));

        WriteAt(1, 1, ConsoleColor.White, "╔══HOLD══╗");
        for (int i = 2; i < 5; i++)
        {
            WriteAt(1, i, ConsoleColor.White, "║");
            WriteAt(10, i, ConsoleColor.White, "║");
        }
        WriteAt(1, 5, ConsoleColor.White, "╚════════╝");

        WriteAt(12, 1, ConsoleColor.White, "╔════════════════════╗");
        for (int i = 2; i < 2 + 20; i++)
        {
            WriteAt(12, i, ConsoleColor.White, "║");
            WriteAt(33, i, ConsoleColor.White, "║");
        }
        WriteAt(12, 22, ConsoleColor.White, "╚════════════════════╝");

        WriteAt(35, 1, ConsoleColor.White, "╔══NEXT══╗");
        for (int i = 2; i < 2 + 17; i++)
        {
            WriteAt(35, i, ConsoleColor.White, "║");
            WriteAt(44, i, ConsoleColor.White, "║");
        }
        WriteAt(35, 19, ConsoleColor.White, "╚════════╝");

        WriteAt(1, 7, ConsoleColor.White, "╔════════╗");
        for (int i = 8; i < 12; i++)
        {
            WriteAt(1, i, ConsoleColor.White, "║");
            WriteAt(10, i, ConsoleColor.White, "║");
        }
        WriteAt(1, 12, ConsoleColor.White, "╚════════╝");
        WriteAt(2, 8, ConsoleColor.White, "SCORE");
        WriteAt(2, 9, ConsoleColor.White, Score.ToString().PadRight(8));
        WriteAt(2, 10, ConsoleColor.White, "LEVEL");
        WriteAt(2, 11, ConsoleColor.White, Level.ToString().PadRight(8));
        #endregion

        // Draw next
        for (int i = 0; i < Math.Min(6, Next.Length); i++)
            DrawPiece(Next[i], 38, 3 + 3 * i, 0, false);
        // Draw hold
        DrawPiece(Hold, 4, 3, 0, false);
        // Draw board
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 20; y++)
                WriteAt(31 - (x * 2), 21 - y, MatrixColors[y][x], BLOCKSOLID);
        // Draw current piece
        DrawCurrent(false);
        // Draw trash meter
        DrawTrashMeter();
    }

    protected void DrawCurrent(bool black)
    {
        //if (IsDead) return;

        // Ghost
        int old_y = Y;
        Matrix.MoveToGround(Current, X, ref Y);
        for (int x = 0; x < 10 - Current.MaxX; x++)
            for (int y = 0; y < Current.H; y++)
                if (y + Y < 20) // If visible
                    if ((Current.Raw >> (10 * y + x) & 1) == 1)
                        WriteAt(31 - ((x + X) * 2), 21 - (y + Y), black ? ConsoleColor.Black : PieceColors[Current & Piece.PIECE_BITS], BLOCKGHOST);
        Y = old_y;
        // Piece
        for (int x = 0; x < 10 - Current.MaxX; x++)
            for (int y = 0; y < Current.H; y++)
                if (y + Y < 20) // If visible
                    if ((Current.Raw >> (10 * y + x) & 1) == 1)
                        WriteAt(31 - ((x + X) * 2), 21 - (y + Y), black ? ConsoleColor.Black : PieceColors[Current & Piece.PIECE_BITS], BLOCKSOLID);
    }

    protected void DrawPiece(Piece piece, int x, int y, int r, bool black)
    {
        //if (IsDead) return;

        int width = 10 - piece.MaxX;
        for (int i = 0; i < width; i++)
            for (int j = 0; j < piece.H; j++)
                if ((piece.Raw >> (10 * j + i) & 1) == 1)
                    // Center piece
                    WriteAt(x - (i * 2) + (width / 2 * 2), y - j, black ? ConsoleColor.Black : PieceColors[piece & Piece.PIECE_BITS], BLOCKSOLID);
    }

    protected void ClearScreen()
    {
        // if (IsDead) return;

        // Clear console section
        for (int i = 0; i < GAMEHEIGHT; i++)
            WriteAt(1, i, ConsoleColor.White, "".PadLeft(GAMEWIDTH - 1));
    }
    #endregion
    
    public static bool AllReady()
    {
        if (Games == null) return false;

        bool allReady = true;
        foreach (Game g in Games)
        {
            if (g.IsTicking)
            {
                allReady = false;
                break;
            }
        }

        return allReady;
    }
}

class Program
{
    static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

    static void Main()
    {
        // Set up console
        Console.Title = "Budget Tetris AI";
        FConsole.Framerate = 30;
        FConsole.CursorVisible = false;
        FConsole.SetFont("Consolas", 18);
        FConsole.Initialise();

        // Set up games
        int seed = Guid.NewGuid().GetHashCode();
        Game main = new Game(6, seed);
        Game[] games = { new Game(5, seed), main };

        Game.SetGames(games);
        FConsole.SetRenderCallback(() =>
        {
            Game g = Game.Games[1];
                while (g.IsTicking) Thread.Sleep(0);
                g.TickAsync();
        });

        // Set up input handler
        FConsole.AddOnPressListener(Key.Left, () => main.Play(Moves.Left));
        //FastConsole.AddOnHoldListener(Key.Left, () => main.Play(Moves.Left), 133, 0);
        FConsole.AddOnHoldListener(Key.Left, () => main.Play(Moves.DASLeft), 133, 16);

        FConsole.AddOnPressListener(Key.Right, () => main.Play(Moves.Right));
        //FastConsole.AddOnHoldListener(Key.Right, () => main.Play(Moves.Right), 133, 0);
        FConsole.AddOnHoldListener(Key.Right, () => main.Play(Moves.DASRight), 133, 16);

        FConsole.AddOnPressListener(Key.Up, () => main.Play(Moves.RotateCW));
        FConsole.AddOnPressListener(Key.Z, () => main.Play(Moves.RotateCCW));
        FConsole.AddOnPressListener(Key.A, () => main.Play(Moves.Rotate180));

        FConsole.AddOnHoldListener(Key.Down, () => main.Play(Moves.SoftDrop), 0, 16);
        FConsole.AddOnPressListener(Key.Space, () => main.Play(Moves.HardDrop));

        FConsole.AddOnPressListener(Key.C, () => main.Play(Moves.Hold));
        FConsole.AddOnPressListener(Key.R, () => main.Restart());

        //new Bot(BaseDirectory + @"NNs\plan2.txt", games[0]).Start(0, 0);


        //while (true)
        //{
        //    ConsoleKey key = Console.ReadKey(true).Key;
        //    if (key >= ConsoleKey.D1 && key <= ConsoleKey.D7)
        //    {
        //        main.Current = key - ConsoleKey.D1 + 1;
        //    }
        //}
    }
}
