namespace Tetris;

using FastConsole;
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

// TODO: change callbacks to events or use tasks?
sealed class Piece
{
    private static readonly int[] KicksX = { 0, -1, -1, 0, -1 }, IKicksX = { 0, -2, 1, -2, 1 };
    private static readonly int[] KicksY = { 0, 0, -1, 2, 2 }, IKicksY = { 0, 0, 0, 1, -2 };

    // [piece][rot][layer]
    private static readonly int[][][] PieceX = { new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 } }, //empty
                                      new int[][] { new int[] { 0, 0, -1, 1}, new int[] { 0, 0, 1, 0 }, new int[] { 0, -1, 1, 0 }, new int[] { 0, 0, -1, 0 } }, //T
                                      new int[][] { new int[] { 0, -1, 1, 2 }, new int[] { 1, 1, 1, 1 }, new int[] { -1, 0, 1, 2 }, new int[] { 0, 0, 0, 0 } }, //I
                                      new int[][] { new int[] { 1, -1, 1, 0 }, new int[] { 0, 0, 0, 1 }, new int[] { 0, -1, 1, -1 }, new int[] { 0, -1, 0, 0 } }, //L
                                      new int[][] { new int[] { -1, -1, 1, 0 }, new int[] { 0, 1, 0, 0 }, new int[] { 0, -1, 1, 1 }, new int[] { 0, 0, 0, -1 } }, //J
                                      new int[][] { new int[] { 0, 1, 0, -1 }, new int[] { 0, 0, 1, 1 }, new int[] { 0, 1, 0, -1 }, new int[] { -1, 0, -1, 0 } }, //S
                                      new int[][] { new int[] { -1, 0, 0, 1 }, new int[] { 1, 1, 0, 0 }, new int[] { 0, -1, 0, 1 }, new int[] { 0, 0, -1, -1 } }, //Z
                                      new int[][] { new int[] { 0, 1, 0, 1 }, new int[] { 0, 1, 0, 1 }, new int[] { 0, 1, 0, 1 }, new int[] { 0, 1, 0, 1 } } }; //O
    // Must be sorted in ascending order
    private static readonly int[][][] PieceY = { new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 } }, //empty
                                      new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, 0, 0, 1 } }, //T
                                      new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { -1, 0, 1, 2 }, new int[] { 1, 1, 1, 1 }, new int[] { -1, 0, 1, 2 } }, //I
                                      new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, 0, 1, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, -1, 0, 1 } }, //L
                                      new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, -1, 0, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, 0, 1, 1 } }, //J
                                      new int[][] { new int[] { -1 ,-1, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 1, 1 }, new int[] { -1, 0, 0, 1 } }, //S
                                      new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 1, 1 }, new int[] { -1, 0, 0, 1 } }, //Z
                                      new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 } } }; //O

    public const int PIECE_BITS = 0x7, ROTATION_BITS = 0x18;
    public const int EMPTY = 0x0,
                     T = 0x1,
                     I = 0x2,
                     L = 0x3,
                     J = 0x4,
                     S = 0x5,
                     Z = 0x6,
                     O = 0x7;
    public const int ROTATION_NONE = 0x0,
                     ROTATION_CW = 0x8,
                     ROTATION_180 = 0x10,
                     ROTATION_CCW = 0x18;
    public const int Garbage = 8,
                     Bedrock = 9; // Trash that can't get cleared

    private static readonly Piece[] Pieces = GetPieces();

    public readonly int Id;
    public readonly int PieceType, R;
    public readonly int[] X, Y;
    public readonly int MinX, MaxX, MaxY;
    internal readonly int[] KicksCWX, KicksCWY;
    internal readonly int[] KicksCCWX, KicksCCWY;
    internal readonly int Kick180X = 0, Kick180Y = 0;

    public readonly ulong Mask;
    
    private Piece(int id)
    {
        Id = id;
        PieceType = id & PIECE_BITS;
        R = id & ROTATION_BITS;

        X = PieceX[PieceType][R >> 3];
        Y = PieceY[PieceType][R >> 3];

        MinX = -X.Min();
        MaxX = 9 - X.Max();
        MaxY = 39 - Y.Max();

        GetKicksTable(true, this, out KicksCWX, out KicksCWY);
        GetKicksTable(false, this, out KicksCCWX, out KicksCCWY);

        // Center at (7, 37); Mask shows a view of the last 4 rows
        Mask = 0;
        for (int i = 0; i < 4; i++)
            Mask |= 1UL << ((2 - X[i]) + 10 * (2 - Y[i]));
    }

    private static Piece[] GetPieces()
    {
        int piece_count = (PIECE_BITS | ROTATION_BITS) + 1;
        Piece[] pieces = new Piece[piece_count];
        for (int i = 0; i < piece_count; i++)
        {
            pieces[i] = new Piece(i);
        }
        return pieces;
    }

    // Helper function to convert from old format
    private static void GetKicksTable(bool clockwise, Piece piece, out int[] kicksx, out int[] kicksy)
    {
        bool vertial = (piece.R & ROTATION_CW) == ROTATION_CW;
        int xmul = (!clockwise ^ (piece.R > ROTATION_CW) ^ (vertial && clockwise)) ? -1 : 1;
        int ymul = vertial ? -1 : 1;
        if (piece.PieceType == I) ymul *= ((piece.R > ROTATION_CW) ^ clockwise) ? -1 : 1;
        int[] testorder = piece.PieceType == I && (vertial ^ !clockwise) ? new int[] { 0, 2, 1, 4, 3 } : new int[] { 0, 1, 2, 3, 4 };
        kicksx = new int[5];
        kicksy = new int[5];
        for (int i = 0; i < 5; i++)
        {
            kicksx[i] = (piece.PieceType == I ? IKicksX[testorder[i]] : KicksX[testorder[i]]) * xmul;
            kicksy[i] = (piece.PieceType == I ? IKicksY[testorder[i]] : KicksY[testorder[i]]) * ymul;
        }
    }

    public static implicit operator Piece(int i) =>
        Pieces[i & (PIECE_BITS | ROTATION_BITS)];

    public static implicit operator int(Piece i) => i.Id;
    
    public override string ToString()
    {
        string name = "";
        switch (R)
        {
            case ROTATION_CW:
                name = "CW ";
                break;
            case ROTATION_180:
                name = "180 ";
                break;
            case ROTATION_CCW:
                name = "CCW";
                break;
        }
        switch (PieceType)
        {
            case EMPTY:
                name += "Empty";
                break;
            case T:
                name += "T";
                break;
            case I:
                name += "I";
                break;
            case L:
                name += "L";
                break;
            case J:
                name += "J";
                break;
            case S:
                name += "S";
                break;
            case Z:
                name += "Z";
                break;
            case O:
                name += "O";
                break;
        }

        return name;
    }
}

class GameBase
{
    protected static readonly int[] GarbageLine = { Piece.Garbage, Piece.Garbage, Piece.Garbage, Piece.Garbage, Piece.Garbage, Piece.Garbage, Piece.Garbage, Piece.Garbage, Piece.Garbage, Piece.Garbage };

    public readonly int[][] Matrix = new int[40][]; // [y][x]
    internal int X, Y;

    public Piece Current { get; internal set; }
    public Piece Hold { get; internal set; } = Piece.EMPTY;
    public Piece[] Next { get; protected set; }

    protected Random PieceRand;
    protected int BagIndex;
    protected Piece[] Bag = new Piece[] { Piece.T, Piece.I, Piece.L, Piece.J, Piece.S, Piece.Z, Piece.O };

    protected GameBase()
    {
        BagIndex = Bag.Length;
        for (int i = 0; i < 40; i++) Matrix[i] = new int[10];
    }

    public GameBase(int next_length, int seed) : this()
    {
        Next = new Piece[next_length];
        PieceRand = new Random(seed);

        for (int i = 0; i < Next.Length; i++) Next[i] = NextPiece();
    }

    protected int BlockX(int i) => X + Current.X[i];

    protected int BlockY(int i) => Y + Current.Y[i];

    protected Piece NextPiece()
    {
        if (BagIndex == Bag.Length)
        {
            // Re-shuffle bag
            // Each permutation has an equal chance of appearing by right
            for (int i = 0; i < Bag.Length; i++)
            {
                int swapIndex = PieceRand.Next(Bag.Length - i) + i;
                Piece temp = Bag[swapIndex];
                Bag[swapIndex] = Bag[i];
                Bag[i] = temp;
            }
            BagIndex = 0;
        }

        return Bag[BagIndex++];
    }

    public bool OnGround()
    {
        if (Y >= Current.MaxY) return true;
        for (int i = 0; i < 4; i++)
            if (Matrix[BlockY(i) + 1][BlockX(i)] != Piece.EMPTY)
                return true;
        
        return false;
    }
    
    public int TSpin(bool rotatedLast)
    {
        if (rotatedLast && Current.PieceType == Piece.T)
        {
            // 3 corner rule
            int count = 0;
            // Top and bottom left
            if (X - 1 < 0) count += 2;
            else
            {
                // Top left
                if (Matrix[Y - 1][X - 1] != Piece.EMPTY) count++;
                // Bottom left
                if (Y + 1 > 39) count++;
                else if (Matrix[Y + 1][X - 1] != Piece.EMPTY) count++;
            }
            // Top and bottom right
            if (X + 1 > 9) count += 2;
            else
            {
                // Top right
                if (Matrix[Y - 1][X + 1] != Piece.EMPTY) count++;
                // Bottom right
                if (Y + 1 > 39) count++;
                else if (Matrix[Y + 1][X + 1] != Piece.EMPTY) count++;
            }

            if (count > 2)
            {
                // Check if mini
                switch (Current.R)
                {
                    case Piece.ROTATION_NONE:
                        if (Matrix[Y - 1][X - 1] == Piece.EMPTY || Matrix[Y - 1][X + 1] == Piece.EMPTY) return 2;
                        break;
                    case Piece.ROTATION_CW:
                        if (Matrix[Y + 1][X + 1] == Piece.EMPTY || Matrix[Y - 1][X + 1] == Piece.EMPTY) return 2;
                        break;
                    case Piece.ROTATION_180:
                        if (Matrix[Y + 1][X - 1] == Piece.EMPTY || Matrix[Y + 1][X + 1] == Piece.EMPTY) return 2;
                        break;
                    case Piece.ROTATION_CCW:
                        if (Matrix[Y - 1][X - 1] == Piece.EMPTY || Matrix[Y + 1][X - 1] == Piece.EMPTY) return 2;
                        break;
                }
                return 3;
            }
        }
        return 0;
    }

    public bool TryRotateCW()
    {
        Piece rotated = Current + Piece.ROTATION_CW;
        for (int i = 0; i < 5; i++)
        {
            bool pass = true;
            int x = X + Current.KicksCWX[i];
            int y = Y + Current.KicksCWY[i];

            if (x < rotated.MinX || x > rotated.MaxX || y > rotated.MaxY)
                continue;
            for (int j = 0; j < 4 && pass; j++)
                if (Matrix[y + rotated.Y[j]][x + rotated.X[j]] != Piece.EMPTY)
                    pass = false;
            if (!pass)
                continue;
            
            X = x;
            Y = y;
            Current = rotated;
            return true;
        }

        return false;
    }

    public bool TryRotateCCW()
    {
        Piece rotated = Current + Piece.ROTATION_CCW;
        for (int i = 0; i < 5; i++)
        {
            bool pass = true;
            int x = X + Current.KicksCCWX[i];
            int y = Y + Current.KicksCCWY[i];

            if (x < rotated.MinX || x > rotated.MaxX || y > rotated.MaxY)
                continue;
            for (int j = 0; j < 4 && pass; j++)
                if (Matrix[y + rotated.Y[j]][x + rotated.X[j]] != Piece.EMPTY)
                    pass = false;
            if (!pass)
                continue;

            X = x;
            Y = y;
            Current = rotated;
            return true;
        }

        return false;
    }

    public bool TryRotate180()
    {
        Piece rotated = Current + Piece.ROTATION_180;

        if (X < rotated.MinX || X > rotated.MaxX || Y > rotated.MaxY)
            return false;
        for (int j = 0; j < 4; j++)
            if (Matrix[Y + rotated.Y[j]][X + rotated.X[j]] != Piece.EMPTY)
                return false;

        Current = rotated;
        return true;
    }

    // Returns true if the piece was rotated; Otherwise, returns false
    // Currently no 180 kicks
    public bool TryRotate(int dr)
    {
        dr &= 3;
        if (dr == 1)
            return TryRotateCW();
        else if (dr == 2)
            return TryRotate180();
        else if (dr == 3)
            return TryRotateCCW();

        return false;
    }

    public bool TrySlide(bool right)
    {
        if (right)
        {
            if (X < Current.MaxX)
            {
                for (int i = 0; i < 4; i++)
                    if (Matrix[BlockY(i)][BlockX(i) + 1] != Piece.EMPTY)
                        return false;
                X++;
                return true;
            }
        }
        else
        {
            if (X > Current.MinX)
            {
                for (int i = 0; i < 4; i++)
                    if (Matrix[BlockY(i)][BlockX(i) - 1] != Piece.EMPTY)
                        return false;
                X--;
                return true;
            }
        }

        return false;
    }

    // Returns true if the piece was moved; Otherwise, returns false
    public bool TrySlide(int dx)
    {
        bool right = dx > 0, moved = false;
        while (dx != 0)
        {
            if (!TrySlide(right)) break;
            moved = true;
            dx -= right ? 1 : -1;
        }

        return moved;
    }

    // Returns number of blocks moved down by
    public int TryDrop(int dy)
    {
        int moved = 0;
        for ( ; dy > 0 && !OnGround(); dy--, Y++) moved++;
        return moved;
    }

    public void ResetPiece()
    {
        // Move new piece to top
        X = 4; Y = 19;
        Current = Current.PieceType;
        // Drop immediately if possible
        if (!OnGround()) Y++;
    }

    public int[] Place(out int cleared)
    {
        // Put piece into matrix
        for (int i = 0; i < 4; i++) Matrix[BlockY(i)][BlockX(i)] = Current.PieceType;

        // Find cleared lines
        cleared = 0;
        int[] clears = new int[4];
        for (int i = 0; i < 4; i++)
        {
            // Skip if this Y level has already been checked
            if (i > 0)
                if (Current.Y[i] == Current.Y[i - 1])
                    continue;

            int y = BlockY(i);
            bool clear = true;
            for (int x = 0; x < 10 && clear; x++)
                if (Matrix[y][x] == Piece.EMPTY)
                    clear = false;
            if (clear) clears[cleared++] = y;
        }

        // Clear and move lines down
        if (cleared != 0)
        {
            int movedown = 1;
            for (int y = clears[cleared - 1] - 1; y >= 17; y--)
            {
                if (movedown == cleared) Matrix[y + movedown] = Matrix[y];
                else if (clears[cleared - movedown - 1] == y) movedown++;
                else Matrix[y + movedown] = Matrix[y];
            }
            // Add new empty rows
            for (; movedown > 0; movedown--) Matrix[16 + movedown] = new int[10];
        }

        return clears;
    }

    public void Unplace(int[] clears, int cleared)
    {
        // Add back cleared lines
        if (cleared != 0)
        {
            int moveup = cleared;
            for (int y = 16; moveup != 0; y++)
            {
                if (y == clears[cleared - moveup])
                {
                    moveup--;
                    Matrix[y] = new int[10];
                    Buffer.BlockCopy(GarbageLine, 0, Matrix[y], 0, 10 * sizeof(int));
                }
                else Matrix[y] = Matrix[y + moveup];
            }
        }

        // Remove piece
        for (int i = 0; i < 4; i++) Matrix[BlockY(i)][BlockX(i)] = Piece.EMPTY;
    }

    public GameBase Clone()
    {
        GameBase clone = new(Next.Length, 0);
        for (int i = 0; i < 40; i++) clone.Matrix[i] = (int[])Matrix[i].Clone();
        clone.X = X;
        clone.Y = Y;
        clone.Current = Current;
        clone.Hold = Hold;
        clone.Next = (Piece[])Next.Clone();
        return clone;
    }

    public bool PathFind(Piece end_piece, int end_x, int end_y, out List<Moves> moves)
    {
        moves = null;
        // No possible route if piece is out of bounds
        if (end_x < end_piece.MinX || end_x > end_piece.MaxX || end_y < 0 || end_y > end_piece.MaxY)
            return false;
        
        GameBase clone = Clone();
        // Check if hold is needed
        bool hold = false;
        if (clone.Current.PieceType != end_piece.PieceType)
        {
            clone.Current = clone.Hold == Piece.EMPTY ? clone.Next[0] : clone.Hold;
            // No possible route if holding doesn't give correct piece type
            if (clone.Current.PieceType != end_piece.PieceType)
                return false;
            hold = true;
            clone.ResetPiece();
        }

        // Queue of nodes to try
        Queue<(Piece p, int x, int y, List<Moves> m)> nodes = new Queue<(Piece, int, int, List<Moves>)>();
        // Set of seen nodes
        HashSet<int> seen = new HashSet<int>();
        // Breadth first search
        nodes.Enqueue((clone.Current, clone.X, clone.Y, new List<Moves>()));
        seen.Add((clone.Current, clone.X, clone.Y).GetHashCode());
        while (nodes.Count != 0)
        {
            (Piece piece, int x, int y, List<Moves> m) = nodes.Dequeue();
            clone.Current = piece;
            clone.Y = y;
            // Try all different moves
            // Slide left/right
            for (int i = 0; i < 2; i++)
            {
                clone.X = x;
                bool right = i == 1;
                if (clone.TrySlide(right))
                    UpdateWith(m, right ? Moves.Right : Moves.Left);
            }
            // DAS left/right
            for (int i = 0; i < 2; i++)
            {
                clone.X = x;
                bool right = i == 1;
                if (clone.TrySlide(right ? 10 : -10))
                    UpdateWith(m, right ? Moves.DASRight : Moves.DASLeft);
            }
            // Rotate CW/CCW/180
            for (int i = 1; i <= 3; i++)
            {
                clone.Current = piece;
                clone.X = x;
                clone.Y = y;
                if (clone.TryRotate(i))
                    UpdateWith(m, i == 1 ? Moves.RotateCW :
                                  i == 2 ? Moves.Rotate180 :
                                           Moves.RotateCCW);
            }
            // Hard/soft drop
            clone.Current = piece;
            clone.X = x;
            clone.Y = y;
            clone.TryDrop(40);
            if (MaskMatch(piece, clone.X, clone.Y))
            {
                moves = m;
                moves.Add(Moves.HardDrop);
                if (hold) moves.Insert(0, Moves.Hold);
                return true;
            }
            UpdateWith(m, Moves.SoftDrop);
        }

        return false;


        void UpdateWith(List<Moves> m, Moves move)
        {
            int hash = (clone.Current, clone.X, clone.Y).GetHashCode();
            if (!seen.Contains(hash))
            {
                seen.Add(hash);
                List<Moves> new_m = new List<Moves>(m);
                new_m.Add(move);
                nodes.Enqueue((clone.Current, clone.X, clone.Y, new_m));
            }
        }

        bool MaskMatch(Piece p, int x, int y)
        {
            ulong end_piece_mask = end_piece.Mask;
            ulong p_mask = p.Mask;

            int x_shift = Math.Abs(x - end_x);
            if (x < end_x) p_mask <<= x_shift;
            else end_piece_mask <<= x_shift;

            int y_shift = Math.Abs(y - end_y) * 10;
            if (y_shift >= 64) return false;
            if (y < end_y) p_mask <<= y_shift;
            else end_piece_mask <<= y_shift;

            return end_piece_mask == p_mask;
        }
    }
}

sealed class Game : GameBase
{
    public const int GAMEWIDTH = 44, GAMEHEIGHT = 27;
    const string BLOCKSOLID = "██", BLOCKGHOST = "▒▒";
    static readonly string[] ClearText = { "SINGLE", "DOUBLE", "TRIPLE", "TETRIS" };
    static readonly ConsoleColor[] PieceColors =
    {
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
    
    // 0 for false, 1 for true
    public static int GamesLock = 0;
    public static Game[] Games { get; private set; }
    public static readonly Stopwatch GlobalTime = Stopwatch.StartNew();
    public static bool IsPausedGlobal { get; private set; } = false;
    
    public int[] LinesTrash { get; private set; } = { 0, 0, 1, 2, 4 };
    public int[] TSpinsTrash { get; private set; } = { 0, 2, 4, 6 };
    // Jstris combo table = { 0, 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }
    public int[] ComboTrash { get; private set; } = { 0, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 };
    public int[] PCTrash { get; private set; } = { 0, 10, 10, 10, 10 };

    #region // Fields and Properties
    public Action TickingCallback;
    public bool IsTicking { get; private set; } = false;

    int XOffset = 0;
    int YOffset = 0;

    private string _Name = "";
    public string Name
    {
        get => _Name;
        set
        {
            _Name = value;
            // Center text
            int space = GAMEWIDTH - value.Length;
            int right_space = space / 2;
            if (space < 0)
                WriteAt(0, -1, ConsoleColor.White, value.Substring(-right_space, 44));
            else
                WriteAt(0, -1, ConsoleColor.White, value.PadRight(right_space + value.Length).PadLeft(GAMEWIDTH));
        }
    }
    private bool _IsDead = true;
    public bool IsDead
    {
        get => _IsDead;
        private set
        {
            _IsDead = value;
            if (_IsDead) EraseClearStats();
        }
    }

    private long _Score = 0;
    public long Score
    {
        get => _Score;
        private set
        {
            _Score = value;
            WriteAt(1, 9, ConsoleColor.White, _Score.ToString().PadRight(8));
        }
    }
    private int _Lines = 0;
    public int Lines
    {
        get => _Lines;
        private set
        {
            _Lines = value;
            WriteAt(25, 0, ConsoleColor.White, _Lines.ToString().PadRight(45 - 26));
        }
    }
    public int Level { get => Lines / 10 + 1; }

    public int B2B { get; internal set; } = -1;
    public int Combo { get; internal set; } = -1;

    public double G = 0.03, SoftG = 1;
    double Vel = 0;
    readonly Queue<Moves> MoveQueue = new Queue<Moves>();

    double LastFrameT;
    public int LockDelay = 1000, EraseDelay = 1500, GarbageDelay = 1000; // In miliseconds
    public int AutoLockGrace = 15;
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
    List<Game> Targets = new List<Game>();
    #endregion

    public Game() : base(6, Guid.NewGuid().GetHashCode())
    {
        GarbageRand = new Random(Guid.NewGuid().GetHashCode());
    }

    public Game(int next_length, int seed) : base(next_length, seed)
    {
        GarbageRand = new Random(seed.GetHashCode());
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
        for (int i = 0; i < Matrix.Length; i++) Matrix[i] = new int[Matrix[i].Length];
        BagIndex = Bag.Length;
        for (int i = 0; i < Next.Length; i++) Next[i] = NextPiece();
        Hold = Piece.EMPTY;

        B2B = -1; Combo = -1;

        IsDead = false;
        Score = 0;
        Lines = 0;
        sent = 0;

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

    public void TickAsync()
    {
        if (IsDead || IsPausedGlobal) return;

        IsTicking = true;
        new Thread(() => {
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
            if (OnGround())
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
            if (LockT.ElapsedMilliseconds > LockDelay && OnGround()) PlacePiece();

            // Ded
            //if (IsDead)
            //{
            //    Restart();
            //}

            IsTicking = false;
            TickingCallback?.Invoke();
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

        FConsole.Set(width * (GAMEWIDTH + 1) + 1, height * GAMEHEIGHT + 1, width * (GAMEWIDTH + 1) + 1, height * GAMEHEIGHT + 1);

        foreach (Game g in Games) g.ClearScreen();

        // Set up and re-draw games
        for (int i = 0; i < Games.Length; i++)
        {
            Games[i].XOffset = (i % width) * (GAMEWIDTH + 1) + 1;
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
                Targets = GetAliveGames();
                break;
            case TargetModes.Self:
                Targets = new List<Game>() { this };
                break;
        }
        // Send trash
        foreach (Game victim in Targets)
        {
            victim.Garbage.Add((trash, time));
            victim.DrawTrashMeter();
            Task.Delay(GarbageDelay).ContinueWith(t => victim.DrawTrashMeter());
        }
    }

    void SpawnNextPiece()
    {
        // Undraw next
        for (int i = 0; i < Math.Min(6, Next.Length); i++)
            DrawPiece(Next[i], 37, 3 + 3 * i, true);
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
        for (int i = 0; i < 4; i++)
            if (Matrix[BlockY(i)][BlockX(i)] != 0)
                IsDead = true;
        // Check for lock out
        //bool isDead = true;
        //for (int i = 0; i < 4; i++)
        //    if (BlockY(i) > 19)
        //        isDead = false;
        //IsDead |= isDead;
        // Draw next
        for (int i = 0; i < Math.Min(6, Next.Length); i++)
            DrawPiece(Next[i], 37, 3 + 3 * i, false);
        // Draw current
        DrawCurrent(false);
    }

    #region // Player methods
    public void Play(Moves move) => MoveQueue.Enqueue(move);

    public void Slide(int dx)
    {
        DrawCurrent(true);
        if (TrySlide(dx))
        {
            //Playsfx(Sounds.smth);
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
        if (moved != 0)
        {
            //Playsfx(Sounds.smth);
            IsLastMoveRotate = false;
            //if (MoveCount < AutoLockGrace) LockT.Restart(); // Kinda pointless tbh
        }
        DrawCurrent(false);

        return moved;
    }

    public void Rotate(int dr)
    {
        if (Current.PieceType != Piece.O)
        {
            DrawCurrent(true);
            if (TryRotate(dr))
            {
                IsLastMoveRotate = true;
                if (MoveCount++ < AutoLockGrace) LockT.Restart();
            }
            DrawCurrent(false);
        }
    }

    public void HoldPiece()
    {
        if (!AlreadyHeld)
        {
            AlreadyHeld = true;
            IsLastMoveRotate = false;

            // Undraw
            DrawCurrent(true);
            DrawPiece(Hold, 3, 3, true);

            Piece oldhold = Hold.PieceType;
            Hold = Current.PieceType;
            MoveCount = 0;
            if (oldhold.PieceType == Piece.EMPTY)
                SpawnNextPiece();
            else
            {
                Current = oldhold;
                ResetPiece();
            }

            // Redraw
            DrawPiece(Hold, 3, 3, false);
            DrawCurrent(false);
            LockT.Restart();
        }
    }

    // Add sound later
    int sent = 0;
    public void PlacePiece()
    {
        int tspin = TSpin(IsLastMoveRotate); //0 = no spin, 2 = mini, 3 = t-spin
        // Clear lines
        Place(out int cleared);
        // Line clears
        int scoreadd = 0;
        scoreadd += new int[] { 0, 100, 300, 500, 800 }[cleared];
        // T-spins
        if (tspin == 3) scoreadd += new int[] { 400, 700, 900, 1100 }[cleared];
        if (tspin == 2) scoreadd += 100;
        // Perfect clear
        bool pc = true;
        for (int x = 0; x < 10 && pc; x++) if (Matrix[^1][x] != 0) pc = false;
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
        WriteAt(1, 11, ConsoleColor.White, Level.ToString());
        // Write clear stats and play sound
        if (tspin > 0 || cleared > 0 || Combo > 0 || pc) EraseClearStats();
        if (B2Bbonus) WriteAt(4, 14, ConsoleColor.White, "B2B");
        if (tspin == 2) WriteAt(0, 15, ConsoleColor.White, "T-SPIN MINI");
        else if (tspin == 3) WriteAt(2, 15, ConsoleColor.White, "T-SPIN");
        if (cleared > 0) WriteAt(2, 16, ConsoleColor.White, ClearText[cleared - 1]);
        if (Combo > 0) WriteAt(1, 17, ConsoleColor.White, Combo + " COMBO!");
        if (pc) WriteAt(0, 18, ConsoleColor.White, "ALL CLEAR!");

        // Trash sent
        int trash = LinesTrash[cleared];
        if (tspin == 3) trash += new int[] { 0, 2, 3, 4 }[cleared];
        if (B2Bbonus) trash++;
        //if (pc) trash += 4;
        if (Combo > 0) trash += new int[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(Combo - 1, 9)];
        //if (Combo > 0) _trash += new int[] { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(Combo - 1, 11)]; //jstris combo table
        if (pc) trash = 10;

        sent += trash;
        WriteAt(0, 6, ConsoleColor.White, $"Sent: {sent}".PadRight(11));
        WriteAt(0, 26, ConsoleColor.White, $"APL: {Math.Round((double)sent / Lines, 3)}".PadRight(GAMEWIDTH));

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
                    if (GlobalTime.ElapsedMilliseconds - Garbage[0].Item2 <= GarbageDelay) break;

                    int linesToAdd = Garbage[0].Item1;
                    Garbage.RemoveAt(0);
                    int hole = GarbageRand.Next(10);
                    for (int y = 17; y < 40; y++) Matrix[y - linesToAdd] = Matrix[y]; // Move stuff up
                    for (int y = 40 - linesToAdd; y < 40; y++)
                    {
                        // Make row of trash
                        Matrix[y] = new int[10];
                        Buffer.BlockCopy(GarbageLine, 0, Matrix[y], 0, 10 * sizeof(int));
                        // Add hole
                        Matrix[y][hole] = Piece.EMPTY;
                    }
                }
            }
            DrawTrashMeter();
        }
        finally
        {
            // Release lock
            Interlocked.Exchange(ref GarbageLock, 0);
        }
        if (trash > 0) SendTrash(trash);

        //redraw screen
        for (int x = 0; x < 10; x++)
            for (int y = 39; y > 19; y--)
                WriteAt(12 + x * 2, y - 18, PieceColors[Matrix[y][x]], BLOCKSOLID);

        SpawnNextPiece();
    }
    #endregion

    #region // Drawing methods
    public void WriteAt(int x, int y, ConsoleColor color, string text) =>
        FConsole.WriteAt(text, x + XOffset, y + YOffset, foreground: color);

    void DrawCurrent(bool black)
    {
        // Ghost
        int movedown = TryDrop(40);
        for (int i = 0; i < 4; i++)
            if (BlockY(i) > 19) // If visible
                WriteAt(BlockX(i) * 2 + 12, BlockY(i) - 18, black ? ConsoleColor.Black : PieceColors[Current.PieceType], BLOCKGHOST);
        Y -= movedown;
        // Piece
        for (int i = 0; i < 4; i++)
            if (BlockY(i) > 19) // If visible
                WriteAt(BlockX(i) * 2 + 12, BlockY(i) - 18, black ? ConsoleColor.Black : PieceColors[Current.PieceType], BLOCKSOLID);
    }

    void DrawPiece(Piece piece, int x, int y, bool black)
    {
        for (int i = 0; i < 4; i++)
            WriteAt(piece.X[i] * 2 + x, piece.Y[i] + y, black ? ConsoleColor.Black : PieceColors[piece.PieceType], BLOCKSOLID);
    }

    void DrawTrashMeter()
    {
        int y = 21;
        if (Garbage.Count != 0)
        {
            for (int i = 0; i < Garbage.Count; i++)
            {
                ConsoleColor color = GlobalTime.ElapsedMilliseconds - Garbage[i].Item2 > GarbageDelay ? ConsoleColor.Red : ConsoleColor.Gray;
                for (int j = y; y > j - Garbage[i].Item1 && y > 1; y--)
                {
                    WriteAt(33, y, color, "█");
                }
            }
        }
        for (; y >= 0; y--) WriteAt(33, y, ConsoleColor.Black, " ");
    }

    void EraseClearStats()
    {
        for (int i = 14; i < 19; i++) WriteAt(0, i, ConsoleColor.Black, "".PadLeft(11));
        EraseT.Restart();
    }

    void ClearScreen()
    {
        // Clear console section
        for (int i = 0; i < GAMEHEIGHT; i++)
            WriteAt(0, i, ConsoleColor.White, "".PadLeft(GAMEWIDTH));
    }

    void DrawAll()
    {
        ClearScreen();
        #region // Draw outlines
        WriteAt(17, 0, ConsoleColor.White, "LINES - ");
        WriteAt(25, 0, ConsoleColor.White, Lines.ToString().PadRight(45 - 26));

        WriteAt(0, 1, ConsoleColor.White, "╔══HOLD══╗");
        for (int i = 2; i < 5; i++)
        {
            WriteAt(0, i, ConsoleColor.White, "║");
            WriteAt(9, i, ConsoleColor.White, "║");
        }
        WriteAt(0, 5, ConsoleColor.White, "╚════════╝");

        WriteAt(11, 1, ConsoleColor.White, "╔════════════════════╗");
        for (int i = 2; i < 2 + 20; i++)
        {
            WriteAt(11, i, ConsoleColor.White, "║");
            WriteAt(32, i, ConsoleColor.White, "║");
        }
        WriteAt(11, 22, ConsoleColor.White, "╚════════════════════╝");

        WriteAt(34, 1, ConsoleColor.White, "╔══NEXT══╗");
        for (int i = 2; i < 2 + 17; i++)
        {
            WriteAt(34, i, ConsoleColor.White, "║");
            WriteAt(43, i, ConsoleColor.White, "║");
        }
        WriteAt(34, 19, ConsoleColor.White, "╚════════╝");

        WriteAt(0, 7, ConsoleColor.White, "╔════════╗");
        for (int i = 8; i < 12; i++)
        {
            WriteAt(0, i, ConsoleColor.White, "║");
            WriteAt(9, i, ConsoleColor.White, "║");
        }
        WriteAt(0, 12, ConsoleColor.White, "╚════════╝");
        WriteAt(1, 8, ConsoleColor.White, "SCORE");
        WriteAt(1, 9, ConsoleColor.White, Score.ToString().PadRight(8));
        WriteAt(1, 10, ConsoleColor.White, "LEVEL");
        WriteAt(1, 11, ConsoleColor.White, Level.ToString().PadRight(8));
        #endregion

        // Draw next
        for (int i = 0; i < Math.Min(6, Next.Length); i++)
            DrawPiece(Next[i], 37, 3 + 3 * i, false);
        // Draw hold
        DrawPiece(Hold, 3, 3, false);
        // Draw board
        for (int x = 0; x < 10; x++)
            for (int y = 20; y < 40; y++)
                WriteAt(x * 2 + 12, y - 18, PieceColors[Matrix[y][x]], BLOCKSOLID);
        // Draw current piece
        DrawCurrent(false);
        // Draw trash meter
        DrawTrashMeter();
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
        Console.Title = "Console Tetris Clone With Bots";
        FConsole.Framerate = 30;
        FConsole.CursorVisible = false;
        FConsole.SetFont("Consolas", 18);
        FConsole.Initialise(FrameEndCallback);

        // Set up games
        int seed = new Random().Next();
        Game[] games = { new Game(10, seed), new Game(10, seed), new Game(10, seed), new Game(10, seed), new Game(10, seed) };

        Game main = games[2];
        main.SoftG = 40;

        Game.SetGames(games);

        // Set up bots
        new Bot(BaseDirectory + @"NNs\plan3.txt", games[0]).Start(150, 100);
        new Bot(BaseDirectory + @"NNs\plan2.txt", games[1]).Start(150, 100);
        new Bot(BaseDirectory + @"NNs\plan3.txt", games[2]).Start(150, 100);
        new Bot(BaseDirectory + @"NNs\plan3.txt", games[3]).Start(150, 100);
        new Bot(BaseDirectory + @"NNs\plan2.txt", games[4]).Start(150, 100);
        
        // Set up input handler
        //SetupPlayerInput(main);
    }

    static void SetupPlayerInput(Game player_game)
    {
        FConsole.AddOnPressListener(Key.Left, () => player_game.Play(Moves.Left));
        //FastConsole.AddOnHoldListener(Key.Left, () => main.Play(Moves.Left), 133, 0);
        FConsole.AddOnHoldListener(Key.Left, () => player_game.Play(Moves.DASLeft), 133, 15);

        FConsole.AddOnPressListener(Key.Right, () => player_game.Play(Moves.Right));
        //FastConsole.AddOnHoldListener(Key.Right, () => main.Play(Moves.Right), 133, 0);
        FConsole.AddOnHoldListener(Key.Right, () => player_game.Play(Moves.DASRight), 133, 15);

        FConsole.AddOnPressListener(Key.Up, () => player_game.Play(Moves.RotateCW));
        FConsole.AddOnPressListener(Key.Z, () => player_game.Play(Moves.RotateCCW));
        FConsole.AddOnPressListener(Key.A, () => player_game.Play(Moves.Rotate180));

        FConsole.AddOnHoldListener(Key.Down, () => player_game.Play(Moves.SoftDrop), 0, 16);
        FConsole.AddOnPressListener(Key.Space, () => player_game.Play(Moves.HardDrop));

        FConsole.AddOnPressListener(Key.C, () => player_game.Play(Moves.Hold));
        FConsole.AddOnPressListener(Key.R, () => player_game.Restart());
    }
    
    static void FrameEndCallback()
    {
        // Wait for games to be ready
        while (!Game.AllReady()) Thread.Sleep(0);
        // Aquire lock
        while (Interlocked.Exchange(ref Game.GamesLock, 1) == 1) Thread.Sleep(0);

        foreach (Game game in Game.Games) game.TickAsync();

        // Release lock
        Interlocked.Exchange(ref Game.GamesLock, 0);
    }
}
