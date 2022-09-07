namespace Masks;

using FastConsole;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    static unsafe void Main2()
    {
        FConsole.Initialise();
        FConsole.Set(80, 26, 80, 26);
        FConsole.CursorVisible = false;

        Matrix10x24 board = new Matrix10x24();
        int x = 4, y = 20;
        Piece t = Piece.T;
        while (true)
        {
            board.Place(t, x, y);
            FConsole.WriteAt(board, 13, 1);
            FConsole.WriteAt(board.H.ToString().PadRight(79), 1, 25);
            board.TogglePlace(t, x, y);

            if (Console.KeyAvailable)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.LeftArrow:
                        if (x < t.MaxX)
                        {
                            x++;
                            if (board.Collides(t, x, y)) x--;
                        }
                        break;
                    case ConsoleKey.RightArrow:
                        if (x > 0)
                        {
                            x--;
                            if (board.Collides(t, x, y)) x++;
                        }
                        break;
                    case ConsoleKey.DownArrow:
                        if (y > 0)
                        {
                            y--;
                            if (board.Collides(t, x, y)) y++;
                        }
                        break;
                    case ConsoleKey.UpArrow:
                        board.TryRotateCW(ref t, ref x, ref y);
                        break;
                    case ConsoleKey.Z:
                        board.TryRotateCCW(ref t, ref x, ref y);
                        break;
                    case ConsoleKey.A:
                        board.TryRotate180(ref t, ref x, ref y);
                        break;
                    case ConsoleKey.Spacebar:
                        board.MoveToGround(t, x, ref y);
                        board.PlaceAndClear(t, x, y);
                        t = new Random().Next(1, 8);
                        x = t.StartX; y = t.StartY;
                        break;
                }
            }
        }
    }
}

public class Piece
{
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

    private static readonly Piece[] Pieces =
    {
        // Empty
        new Piece(EMPTY,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000),
        // T
        new Piece(T,
                        0b0000000000,
                        0b0000000000,
                        0b0000000010,
                        0b0000000111),
        // I
        new Piece(I,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000,
                        0b0000001111),
        // L
        new Piece(L,
                        0b0000000000,
                        0b0000000000,
                        0b0000000001,
                        0b0000000111),
        // J
        new Piece(J,
                        0b0000000000,
                        0b0000000000,
                        0b0000000100,
                        0b0000000111),
        // S
        new Piece(S,
                        0b0000000000,
                        0b0000000000,
                        0b0000000011,
                        0b0000000110),
        // Z
        new Piece(Z,
                        0b0000000000,
                        0b0000000000,
                        0b0000000110,
                        0b0000000011),
        // O
        new Piece(O,
                        0b0000000000,
                        0b0000000000,
                        0b0000000011,
                        0b0000000011),
        // Empty R
        new Piece(EMPTY | ROTATION_CW,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000),
        // T R
        new Piece(T | ROTATION_CW,
                        0b0000000000,
                        0b0000000010,
                        0b0000000011,
                        0b0000000010),
        // I R
        new Piece(I | ROTATION_CW,
                        0b0000000001,
                        0b0000000001,
                        0b0000000001,
                        0b0000000001),
        // L R
        new Piece(L | ROTATION_CW,
                        0b0000000000,
                        0b0000000010,
                        0b0000000010,
                        0b0000000011),
        // J R
        new Piece(J | ROTATION_CW,
                        0b0000000000,
                        0b0000000011,
                        0b0000000010,
                        0b0000000010),
        // S R
        new Piece(S | ROTATION_CW,
                        0b0000000000,
                        0b0000000010,
                        0b0000000011,
                        0b0000000001),
        // Z R
        new Piece(Z | ROTATION_CW,
                        0b0000000000,
                        0b0000000001,
                        0b0000000011,
                        0b0000000010),
        // O R
        new Piece(O | ROTATION_CW,
                        0b0000000000,
                        0b0000000000,
                        0b0000000011,
                        0b0000000011),
        // Empty 180
        new Piece(EMPTY | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000),
        // T 180
        new Piece(T | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000111,
                        0b0000000010),
        // I 180
        new Piece(I | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000,
                        0b0000001111),
        // L 180
        new Piece(L | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000111,
                        0b0000000100),
        // J 180
        new Piece(J | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000111,
                        0b0000000001),
        // S 180
        new Piece(S | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000011,
                        0b0000000110),
        // Z 180
        new Piece(Z | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000110,
                        0b0000000011),
        // O 180
        new Piece(O | ROTATION_180,
                        0b0000000000,
                        0b0000000000,
                        0b0000000011,
                        0b0000000011),
        // Empty L
        new Piece(EMPTY | ROTATION_CCW,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000,
                        0b0000000000),
        // T L
        new Piece(T | ROTATION_CCW,
                        0b0000000000,
                        0b0000000001,
                        0b0000000011,
                        0b0000000001),
        // I L
        new Piece(I | ROTATION_CCW,
                        0b0000000001,
                        0b0000000001,
                        0b0000000001,
                        0b0000000001),
        // L L
        new Piece(L | ROTATION_CCW,
                        0b0000000000,
                        0b0000000011,
                        0b0000000001,
                        0b0000000001),
        // J L
        new Piece(J | ROTATION_CCW,
                        0b0000000000,
                        0b0000000001,
                        0b0000000001,
                        0b0000000011),
        // S L
        new Piece(S | ROTATION_CCW,
                        0b0000000000,
                        0b0000000010,
                        0b0000000011,
                        0b0000000001),
        // Z L
        new Piece(Z | ROTATION_CCW,
                        0b0000000000,
                        0b0000000001,
                        0b0000000011,
                        0b0000000010),
        // O L
        new Piece(O | ROTATION_CCW,
                        0b0000000000,
                        0b0000000000,
                        0b0000000011,
                        0b0000000011),
    };

    internal readonly ulong Raw;
    public readonly int Id;
    public readonly int PieceType;
    public readonly int R;

    public readonly int MaxX;
    public readonly int H; // 0, 1, 2, 3 or 4
    internal readonly int H10;
    public readonly int StartX, StartY;
    internal readonly int[] KicksCWX, KicksCWY;
    internal readonly int[] KicksCCWX, KicksCCWY;
    internal readonly int Kick180X, Kick180Y;
    //internal readonly int[] Kicks180X, Kicks180Y;
    
    private Piece(int id, ulong row0, ulong row1, ulong row2, ulong row3)
    {
        if ((row0 | row1 | row2 | row3) > 1023)
            throw new ArgumentException("Each row should not be larger than 1023 (2 ^ 10 - 1)!");
        Raw = row3 | (row2 << 10) | (row1 << 20) | (row0 << 30);
        Id = id;
        PieceType = id & PIECE_BITS;
        R = id & ROTATION_BITS;

        // Get no. of digits
        H10 = 64 - BitOperations.LeadingZeroCount(Raw);
        // Round up to the smallest multiple of 10
        if (H10 % 10 != 0)
            H10 += 10 - (H10 % 10);
        H = H10 / 10;

        // Get max X
        MaxX = 10;
        for (int i = 10; i <= H10; i += 10)
            MaxX = Math.Min(MaxX, BitOperations.LeadingZeroCount(Raw << (64 - i)));

        // Get start X and Y
        TetrisHelper.GetOffset(id & PIECE_BITS, id >> 3, out StartX, out StartY);
        StartX = 5 - StartX; StartY = 20 - StartY;

        // Get kicks
        TetrisHelper.GetKicksTable(true, id & PIECE_BITS, id >> 3, out KicksCWX, out KicksCWY);
        TetrisHelper.GetKicksTable(false, id & PIECE_BITS, id >> 3, out KicksCCWX, out KicksCCWY);
        TetrisHelper.GetKicks180Table(id & PIECE_BITS, id >> 3, out Kick180X, out Kick180Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Piece(int i) =>
        Pieces[i & (PIECE_BITS | ROTATION_BITS)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                name = "CCW" ;
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

// Optimised some corners so it only works with tetriminos and not all possible piece masks
[StructLayout(LayoutKind.Explicit)]
public unsafe struct Matrix10x24
{
    const ulong FULLROW = 0b1111111111;
    const ulong TOP10 = FULLROW << (64 - 10);

    [FieldOffset(0)]
    internal int H = 0; // Height of highest piece
    [FieldOffset(4)]
    internal readonly ulong Item0 = 0;
    [FieldOffset(12)]
    internal readonly ulong Item1 = 0;
    [FieldOffset(20)]
    internal readonly ulong Item2 = 0;
    [FieldOffset(28)]
    internal readonly ulong Item3 = 0;
    // 64 + (40(largest line(s) clear) - 32) = 72
    // 72 extra bits so we won't have to worry about line clears
    [FieldOffset(36)]
    internal readonly ulong Item4 = 0;
    [FieldOffset(44)]
    internal readonly byte Item5 = 0;
    
    [FieldOffset(45)]
    internal int* Ptr;

    public ulong this[int i]
    {
        get => *(ulong*)(Ptr + i);
        private set => *(ulong*)(Ptr + i) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix10x24()
    {
        fixed (ulong* ptr = &Item0)
            Ptr = (int*)ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Matrix10x24(in Matrix10x24 original)
    {
        fixed (ulong* ptr = &Item0)
            Ptr = (int*)ptr;
        H = original.H;
        Item0 = original.Item0;
        Item1 = original.Item1;
        Item2 = original.Item2;
        Item3 = original.Item3;
    }

    public Matrix10x24(ConsoleColor[][] board, ConsoleColor empty) : this()
    {
        for (int i = 0; i < 240; i++)
        {
            if (board[i / 10][i % 10] != empty)
            {
                this[i / 32] |= 1UL << (i % 32);
                H = i / 10 + 1;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Collides(in Piece piece, int x, int y)
    {
        if (y > H) return false;
        
        int i = 10 * y + x;
        int div = i >> 5; // Divide 32 (2^5)
        int rem = i & 31; // Mod 32
        return (this[div] & piece.Raw << rem) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnGround(in Piece piece, int x, int y)
    {
        if (y > H) return false;
        if (y == 0) return true;
        return Collides(piece, x, y - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveToGround(in Piece piece, int x, ref int y)
    {
        if (y > H) y = H;
        while (!OnGround(piece, x, y)) y--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRotateCW(ref Piece piece, ref int x, ref int y)
    {
        Piece newPiece = piece.Id + Piece.ROTATION_CW;
        for (int i = 0; i < 5; i++)
        {
            int newX = x + piece.KicksCWX[i], newY = y + piece.KicksCWY[i];
            if (newX < 0 || newX > newPiece.MaxX || newY < 0)
                continue;
            if (Collides(newPiece, newX, newY))
                continue;
            x = newX; y = newY;
            piece = newPiece;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRotateCCW(ref Piece piece, ref int x, ref int y)
    {
        Piece newPiece = piece.Id + Piece.ROTATION_CCW;
        for (int i = 0; i < 5; i++)
        {
            int newX = x + piece.KicksCCWX[i], newY = y + piece.KicksCCWY[i];
            if (newX < 0 || newX > newPiece.MaxX || newY < 0)
                continue;
            if (Collides(newPiece, newX, newY))
                continue;
            x = newX; y = newY;
            piece = newPiece;
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryRotate180(ref Piece piece, ref int x, ref int y)
    {
        Piece newPiece = piece.Id + Piece.ROTATION_180;
        int newX = x + piece.Kick180X, newY = y + piece.Kick180Y;
        if (newX < 0 || newX > newPiece.MaxX || newY < 0)
            return false;
        if (Collides(newPiece, newX, newY))
            return false;
        
        x = newX; y = newY;
        piece = newPiece;
        return true;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //public bool TryRotate180(ref PieceMask piece, ref int x, ref int y)
    //{
    //    PieceMask newPiece = PieceMask.GetPiece(piece.Id + PieceMask.ROTATION_180);
    //    for (int i = 0; i < 1; i++)
    //    {
    //        int newX = x + piece.Kicks180X[i], newY = y + piece.Kicks180Y[i];
    //        if (newX < 0 || newX > newPiece.MaxX || newY < 0)
    //            continue;
    //        if (Collides(newPiece, newX, newY))
    //            continue;
    //        x = newX; y = newY;
    //        piece = newPiece;
    //        return true;
    //    }

    //    return false;
    //}

    /// <returns>The number of lines cleared.</returns>
    public int PlaceAndClear(Piece piece, int x, int y)
    {
        int ypos = 10 * y;
        int pos = ypos + x;
        int div = pos >> 5; // Divide 32 (2^5)
        int rem = pos & 31; // Mod 32
        // Place piece
        ulong raw = (this[div] |= piece.Raw << rem);

        // If piece is higher than any other piece, there can't be a clear
        if (y >= H)
        {
            H = y + piece.H;
            return 0;
        }
        else
        {
            H = Math.Max(H, y + piece.H);
            //int shift = ypos + new int[] { 0, -32, -64, -96, -128, -160, -192, -224 }[div];
            int shift = ypos - (div << 5);
            // Shift the raw bits to allign them with the right edge of the board
            ulong lines = (shift > 0) ? raw >> shift : raw << -shift;
            // Get the bottom few bits if needed
            if (shift < 0) lines |= this[div - 2] >> (64 + shift); // Needs the 4 bytes (the int H, currently) in front to not read from protected memory
            // Get the top few bits if needed
            // This could have some extras past bit 40, but that shouldn't matter
            if (shift + piece.H10 > 64)
            {
                ///lines |= this[div + 1] << new int[] { 2, 4, 6, 8, 0, 2, 4 }[div];
                ///lines |= this[div + 1] << ((div + 1) * 32) - ypos;
                ///lines |= this[div + 1] << ((pos & ~31 + 32) - ypos);
                ///lines |= this[div + 1] << ((div << 5) - ypos + 32);
                lines |= this[div + 1] << 32 - shift;
            }
            // Clear lines (maybe move some stuff to the top?)
            int cleared10 = 0;
            for (int i = 0; i < piece.H10; i += 10)
            {
                if ((lines >> i & FULLROW) == FULLROW)
                {
                    int offset = shift + i - cleared10; // Bit where the line starts
                    H--;
                    cleared10 += 10;
                    if (offset < 0)
                    {
                        ulong below_mask = (1UL << (64 + offset)) - 1UL;
                        //this[div - 2] = (this[div - 2] & below_mask) | ((lines << (64 - cleared10 + offset)) & ~below_mask);
                        this[div - 2] = (this[div - 2] & below_mask) | ((this[div] << 54) & ~below_mask);
                        this[div] = (this[div] >> 10) | ((this[div + 2] << 54) & TOP10);
                    }
                    else if (offset >= 54)
                    {
                        ulong below_mask = (1UL << offset) - 1UL; // Replace this and ~this with arrays instead
                        this[div] = (this[div] & below_mask) | ((this[div + 2] << (64 - cleared10)) & (FULLROW << offset));
                    }
                    else
                    {
                        ulong below_mask = (1UL << offset) - 1UL; // Replace this and ~this with arrays instead
                        this[div] = (this[div] & below_mask) | ((this[div] >> 10) & ~below_mask) | ((this[div + 2] << (64 - cleared10)) & TOP10);
                    }
                }
            }
            // Propagate line clears upwards
            if (cleared10 != 0)
            {
                for (int i = div + 2; i < 8; i += 2)
                    this[i] = (this[i] >> cleared10) | (this[i + 2] << (64 - cleared10));
            }

            return cleared10 / 10;
        }
    }

    /// <summary>0 = No T-Spin; 2 = T-Spin Mini; 3 = T-Spin</summary>
    public int GetTSpinKind(bool rotatedLast, in Piece piece, int x, int y)
    {
        const int ALL_CORNERS = (0b101 << 20) | 0b101;
        const int NO_BR = (0b101 << 20) | 0b100;
        const int NO_BL = (0b101 << 20) | 0b001;
        const int NO_TL = (0b001 << 20) | 0b101;
        const int NO_TR = (0b100 << 20) | 0b101;
        
        if (!rotatedLast || ((piece.Id & Piece.PIECE_BITS) != Piece.T))
            return 0;

        int i = 10 * y + x;
        int r = piece.Id & Piece.ROTATION_BITS;
        if (r == Piece.ROTATION_CCW) i--;

        int div = i >> 5; // Divide 32 (2^5)
        int rem = i & 31; // Mod 32
        ulong corners = (this[div] >> rem) & ALL_CORNERS;
        
        if (corners == ALL_CORNERS) return 3;
        switch (piece.Id & Piece.ROTATION_BITS)
        {
            case Piece.ROTATION_NONE:
                if (corners == NO_BR || corners == NO_BL) return 3;
                if (corners == NO_TR || corners == NO_TL) return 2;
                break;
            case Piece.ROTATION_CW:
                if (corners == NO_TL || corners == NO_BL) return 3;
                if (corners == NO_TR || corners == NO_BR) return 2;
                break;
            case Piece.ROTATION_180:
                if (corners == NO_TR || corners == NO_TL) return 3;
                if (corners == NO_BR || corners == NO_BL) return 2;
                break;
            case Piece.ROTATION_CCW:
                if (corners == NO_TR || corners == NO_BR) return 3;
                if (corners == NO_TL || corners == NO_BL) return 2;
                break;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopCount()
    {
        return BitOperations.PopCount(this[0]) + 
               BitOperations.PopCount(this[2]) + 
               BitOperations.PopCount(this[4]) + 
               BitOperations.PopCount(this[6]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Hash()
    {
        return (ulong)this[0].GetHashCode() ^ 
               ((ulong)this[2].GetHashCode() << 11) ^ 
               ((ulong)this[4].GetHashCode() << 22) ^ 
               ((ulong)this[6].GetHashCode() << 33);
    }
    
    // For debugging
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(512);
        for (int i = 240 - 1; i >= 0; i--)
        {
            sb.Append((this[i / 32] >> (i % 32) & 1) == 0 ? "▒▒" : "██");
            if ((i != 0) && (i % 10 == 0)) sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string ToBlocks(ulong x)
    {
        StringBuilder sb = new StringBuilder(148);
        sb.Append("            ");
        for (int i = 63; i >= 0; i--)
        {
            sb.Append(((x >> i) & 1) == 0 ? "▒▒" : "██");
            if ((i != 0) && (i % 10 == 0)) sb.AppendLine();
        }

        return sb.ToString();
    }

    public BigInteger Value()
    {
        BigInteger value = Item0;
        value |= (BigInteger)Item1 << 64;
        value |= (BigInteger)Item2 << 128;
        value |= (BigInteger)Item3 << 196;
        return value;
    }

    internal void Place(Piece piece, int x, int y)
    {
        int i = 10 * y + x;
        int div = i >> 5; // Divide 32 (2^5)
        int rem = i & 31; // Mod 32
        this[div] |= piece.Raw << rem;
    }

    internal void Unplace(Piece piece, int x, int y)
    {
        int i = 10 * y + x;
        int div = i >> 5; // Divide 32 (2^5)
        int rem = i & 31; // Mod 32
        this[div] &= BitOperations.RotateLeft(~piece.Raw, rem);
    }

    internal void TogglePlace(Piece piece, int x, int y)
    {
        int i = 10 * y + x;
        int div = i >> 5; // Divide 32 (2^5)
        int rem = i & 31; // Mod 32
        this[div] ^= piece.Raw << rem;
    }
}

public static class TetrisHelper
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
    private static readonly int[][][] PieceY = { new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 } }, //empty
                                      new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, 0, 0, 1 } }, //T
                                      new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { -1, 0, 1, 2 }, new int[] { 1, 1, 1, 1 }, new int[] { -1, 0, 1, 2 } }, //I
                                      new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, 0, 1, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, -1, 0, 1 } }, //L
                                      new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, -1, 0, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, 0, 1, 1 } }, //J
                                      new int[][] { new int[] { -1 ,-1, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 1, 1 }, new int[] { -1, 0, 0, 1 } }, //S
                                      new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 1, 1 }, new int[] { -1, 0, 0, 1 } }, //Z
                                      new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 } } }; //O

    public static double[] ExractFeat(in Matrix10x24 matrix, double _trash)
    {
        // Find heightest block in each column
        Span<double> heights = new double[10];
        for (int x = 0; x < 10; x++)
        {
            int height = matrix.H;
            int pos = (matrix.H - 1) * 10 + x;
            for ( ; pos >= 0; height--)
            {
                int div = pos >> 5; // div 32
                int rem = pos & 31; // mod 32
                if ((matrix[div] >> rem & 1) == 1) break;
                pos -= 10;
            }
            heights[x] = height;
        }
        // Standard height
        double std_h = 0;
        //if (Network.Visited[0])
        //{
        //    for (int i = 0; i < heights.Length; i++) std_h += heights[i] * heights[i];
        //    std_h = Math.Sqrt(std_h);
        //}
        // "caves"
        double caves = 0;
        //if (Network.Visited[1])
        {
            int div = 0, rem = 0;
            int div2 = 0, rem2 = 10;
            for (int y = 0; y < heights[0] - 1; y++)
            {
                if ((matrix[div] >> rem & 1) == 0 && (matrix[div2] >> rem2 & 1) == 1)
                    if (y >= heights[1]) 
                        caves += heights[0] - y;
                rem += 10;
                if (rem >= 32)
                {
                    div++;
                    rem -= 32;
                }
                rem2 += 10;
                if (rem2 >= 32)
                {
                    div2++;
                    rem2 -= 32;
                }
            }

            for (int x = 1; x < 9; x++)
            {
                div = 0; rem = x;
                div2 = 0; rem2 = 10 + x;
                for (int y = 0; y < heights[x] - 1; y++)
                {
                    if ((matrix[div] >> rem & 1) == 0 && (matrix[div2] >> rem2 & 1) == 1)
                        if (y >= Math.Max(heights[x - 1], heights[x + 1]))
                            caves += heights[x] - y;
                    rem += 10;
                    if (rem >= 32)
                    {
                        div++;
                        rem -= 32;
                    }
                    rem2 += 10;
                    if (rem2 >= 32)
                    {
                        div2++;
                        rem2 -= 32;
                    }
                }
            }

            div = 0; rem = 9;
            div2 = 0; rem2 = 19;
            for (int y = 0; y < heights[9] - 1; y++)
            {
                if ((matrix[div] >> rem & 1) == 0 && (matrix[div2] >> rem2 & 1) == 1)
                    if (y >= heights[8])
                        caves += heights[9] - y;
                rem += 10;
                if (rem >= 32)
                {
                    div++;
                    rem -= 32;
                }
                rem2 += 10;
                if (rem2 >= 32)
                {
                    div2++;
                    rem2 -= 32;
                }
            }
        }
        // Pillars
        double pillars = 0;
        //if (Network.Visited[2])
        //{
        //    for (int x = 0; x < 10; x++)
        //    {
        //        double diff;
        //        // Don't punish for tall towers at the side
        //        if (x != 0 && x != 9) diff = Math.Min(Math.Abs(heights[x - 1] - heights[x]), Math.Abs(heights[x + 1] - heights[x]));
        //        else diff = x == 0 ? Math.Max(0, heights[1] - heights[0]) : Math.Min(0, heights[8] - heights[9]);
        //        if (diff > 2) pillars += diff * diff;
        //        else pillars += diff;
        //    }
        //}
        // Can use x & (x - 1) (set last set bit to 0) and other stuff to speed these up
        // Row trasitions
        double rowtrans = 0;
        //if (Network.Visited[3])
        {
            int div = 0, rem = 0;
            for (int y = 0; y < matrix.H; y++)
            {
                bool empty = (matrix[div] >> rem & 1) == 0;
                rem++;
                if (rem == 32)
                {
                    rem = 0;
                    div++;
                }

                for (int x = 1; x < 10; x++)
                {
                    bool isempty = (matrix[div] >> rem & 1) == 0;
                    if (empty ^ isempty)
                    {
                        rowtrans++;
                        empty = isempty;
                    }
                    rem++;
                    if (rem == 32)
                    {
                        rem = 0;
                        div++;
                    }
                }
            }
        }
        // Column trasitions
        double coltrans = 0;
        //if (Network.Visited[4])
        //{
        //    for (int x = 0; x < 10; x++)
        //    {
        //        int div = 0, rem = x;
        //        bool empty = (matrix[div] >> rem & 1) == 0;
        //        rem += 10;

        //        for (int y = 1; y < heights[x] + 1; y++)
        //        {
        //            bool isempty = (matrix[div] >> rem & 1) == 0;
        //            if (empty ^ isempty)
        //            {
        //                coltrans++;
        //                empty = isempty;
        //            }
        //            rem += 10;
        //            if (rem >= 32)
        //            {
        //                rem -= 32;
        //                div++;
        //            }
        //        }
        //    }
        //}

        return new double[] { std_h, caves, pillars, rowtrans, coltrans, _trash };
    }

    internal static void GetOffset(int piece, int r, out int x, out int y)
    {
        x = int.MinValue; y = int.MinValue;
        for (int i = 0; i < 4; i++)
        {
            x = Math.Max(x, PieceX[piece][r][i]);
            y = Math.Max(y, PieceY[piece][r][i]);
        }
    }
    
    internal static void GetKicksTable(bool clockwise, int piece, int r, out int[] kicksx, out int[] kicksy)
    {
        GetOffset(piece, r, out int x_offset, out int y_offset);
        GetOffset(piece, (clockwise ? r + 1 : r - 1) & 3, out int new_x_offset, out int new_y_offset);

        bool vertial = (r & 1) == 1;
        int xmul = (!clockwise ^ (r > 1) ^ (vertial && clockwise)) ? -1 : 1;
        int ymul = vertial ? -1 : 1;
        if ((piece & Piece.PIECE_BITS) == Piece.I) ymul *= ((r > 1) ^ clockwise) ? -1 : 1;
        int[] testorder = (piece & Piece.PIECE_BITS) == Piece.I && (vertial ^ !clockwise) ? new int[] { 0, 2, 1, 4, 3 } : new int[] { 0, 1, 2, 3, 4 };
        kicksx = new int[5];
        kicksy = new int[5];
        for (int i = 0; i < 5; i++)
        {
            int kickX = ((piece & Piece.PIECE_BITS) == Piece.I ? IKicksX[testorder[i]] : KicksX[testorder[i]]) * xmul;
            int kickY = ((piece & Piece.PIECE_BITS) == Piece.I ? IKicksY[testorder[i]] : KicksY[testorder[i]]) * ymul;
            kicksx[i] = -kickX + (x_offset - new_x_offset);
            kicksy[i] = -kickY + (y_offset - new_y_offset);
        }
    }

    internal static void GetKicks180Table(int piece, int r, out int[] kicksx, out int[] kicksy)
    {
        GetOffset(piece, r, out int x_offset, out int y_offset);
        GetOffset(piece, (r + 2) & 3, out int new_x_offset, out int new_y_offset);
        
        kicksx = new int[] { x_offset - new_x_offset };
        kicksy = new int[] { y_offset - new_y_offset };
    }

    internal static void GetKicks180Table(int piece, int r, out int kickx, out int kicky)
    {
        GetOffset(piece, r, out int x_offset, out int y_offset);
        GetOffset(piece, (r + 2) & 3, out int new_x_offset, out int new_y_offset);

        kickx = x_offset - new_x_offset;
        kicky = y_offset - new_y_offset;
    }
}
