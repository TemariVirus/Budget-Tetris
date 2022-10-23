namespace Tetris;

using FastConsole;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

public enum Moves
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

public enum TargetModes
{
    Random,
    All,
    Self,
    None
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct MatrixMask
{
    public const ulong FULL_LINE = (1 << 10) - 1;

    public static readonly MatrixMask[] HeightMasks = new MatrixMask[25].Select((m, i) =>
    {
        m = ~new MatrixMask();
        m <<= i * 10;
        return ~m << 10;
    }).ToArray();
    public static readonly MatrixMask[] InverseHeightMasks = HeightMasks.Select((m) => ~m & HeightMasks[^1]).ToArray();

    public ulong LowLow { get; init; }
    public ulong LowHigh { get; init; }
    public ulong HighLow { get; init; }
    public ulong HighHigh { get; init; }

    #region // Logical operators
    public static MatrixMask operator ~(MatrixMask value) =>
        new MatrixMask()
        {
            LowLow = ~value.LowLow,
            LowHigh = ~value.LowHigh,
            HighLow = ~value.HighLow,
            HighHigh = ~value.HighHigh
        };
    public static MatrixMask operator &(MatrixMask left, ulong right) =>
        new MatrixMask()
        {
            LowLow = left.LowLow & right,
            LowHigh = left.LowHigh,
            HighLow = left.HighLow,
            HighHigh = left.HighHigh
        };
    public static MatrixMask operator &(MatrixMask matrix, PieceMask piece)
    {
        return piece.Offset switch
        {
            0 => new MatrixMask()
            {
                LowLow = matrix.LowLow & piece.Low,
                LowHigh = matrix.LowHigh & piece.High,
                HighLow = matrix.HighLow,
                HighHigh = matrix.HighHigh
            },
            1 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh & piece.Low,
                HighLow = matrix.HighLow & piece.High,
                HighHigh = matrix.HighHigh
            },
            2 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh,
                HighLow = matrix.HighLow & piece.Low,
                HighHigh = matrix.HighHigh & piece.High
            },
            3 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh,
                HighLow = matrix.HighLow,
                HighHigh = matrix.HighHigh & piece.Low
            },
            _ => matrix
        };
    }
    public static MatrixMask operator &(MatrixMask left, MatrixMask right) =>
        new MatrixMask()
        {
            LowLow = left.LowLow & right.LowLow,
            LowHigh = left.LowHigh & right.LowHigh,
            HighLow = left.HighLow & right.HighLow,
            HighHigh = left.HighHigh & right.HighHigh
        };
    public static MatrixMask operator |(MatrixMask left, ulong right) =>
        new MatrixMask()
        {
            LowLow = left.LowLow | right,
            LowHigh = left.LowHigh,
            HighLow = left.HighLow,
            HighHigh = left.HighHigh
        };
    public static MatrixMask operator |(MatrixMask matrix, PieceMask piece)
    {
        return piece.Offset switch
        {
            0 => new MatrixMask()
            {
                LowLow = matrix.LowLow | piece.Low,
                LowHigh = matrix.LowHigh | piece.High,
                HighLow = matrix.HighLow,
                HighHigh = matrix.HighHigh
            },
            1 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh | piece.Low,
                HighLow = matrix.HighLow | piece.High,
                HighHigh = matrix.HighHigh
            },
            2 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh,
                HighLow = matrix.HighLow | piece.Low,
                HighHigh = matrix.HighHigh | piece.High
            },
            3 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh,
                HighLow = matrix.HighLow,
                HighHigh = matrix.HighHigh | piece.Low
            },
            _ => matrix
        };
    }
    public static MatrixMask operator |(MatrixMask left, MatrixMask right) =>
        new MatrixMask() 
        {
            LowLow = left.LowLow | right.LowLow,
            LowHigh = left.LowHigh | right.LowHigh,
            HighLow = left.HighLow | right.HighLow,
            HighHigh = left.HighHigh | right.HighHigh
        };
    public static MatrixMask operator ^(MatrixMask matrix, PieceMask piece)
    {
        return piece.Offset switch
        {
            0 => new MatrixMask()
            {
                LowLow = matrix.LowLow ^ piece.Low,
                LowHigh = matrix.LowHigh ^ piece.High,
                HighLow = matrix.HighLow,
                HighHigh = matrix.HighHigh
            },
            1 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh ^ piece.Low,
                HighLow = matrix.HighLow ^ piece.High,
                HighHigh = matrix.HighHigh
            },
            2 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh,
                HighLow = matrix.HighLow ^ piece.Low,
                HighHigh = matrix.HighHigh ^ piece.High
            },
            3 => new MatrixMask()
            {
                LowLow = matrix.LowLow,
                LowHigh = matrix.LowHigh,
                HighLow = matrix.HighLow,
                HighHigh = matrix.HighHigh ^ piece.Low
            },
            _ => matrix
        };
    }
    public static MatrixMask operator ^(MatrixMask left, MatrixMask right) =>
        new MatrixMask()
        {
            LowLow = left.LowLow ^ right.LowLow,
            LowHigh = left.LowHigh ^ right.LowHigh,
            HighLow = left.HighLow ^ right.HighLow,
            HighHigh = left.HighHigh ^ right.HighHigh
        };
    public static MatrixMask operator <<(MatrixMask value, int shift)
    {
        if (shift < 0) return value >> -shift;
        
        // Special treatment for multiples of 64
        if ((shift & 63) == 0)
        {
            return (shift & 192) switch
            {
                192 => new MatrixMask()
                {
                    HighHigh = value.LowLow
                },
                128 => new MatrixMask()
                {
                    HighLow = value.LowLow,
                    HighHigh = value.LowHigh
                },
                64 => new MatrixMask()
                {
                    LowHigh = value.LowLow,
                    HighLow = value.LowHigh,
                    HighHigh = value.HighLow
                },
                _ => value
            };
        }
        
        shift &= 255;
        return shift switch
        {
            > 192 => new MatrixMask()
            {
                HighHigh = value.LowLow << (shift - 192)
            },
            > 128 => new MatrixMask()
            {
                HighLow = value.LowLow << (shift - 128),
                HighHigh = (value.LowHigh << (shift - 128)) | (value.LowLow >> (192 - shift))
            },
            > 64 => new MatrixMask()
            {
                LowHigh = value.LowLow << (shift - 64),
                HighLow = (value.LowHigh << (shift - 64)) | (value.LowLow >> (128 - shift)),
                HighHigh = (value.HighLow << (shift - 64)) | (value.LowHigh >> (128 - shift))
            },
            _ => new MatrixMask()
            {
                LowLow = value.LowLow << shift,
                LowHigh = (value.LowHigh << shift) | (value.LowLow >> (64 - shift)),
                HighLow = (value.HighLow << shift) | (value.LowHigh >> (64 - shift)),
                HighHigh = (value.HighHigh << shift) | (value.HighLow >> (64 - shift))
            }
        };
    }
    public static MatrixMask operator >>(MatrixMask value, int shift)
    {
        if (shift < 0) return value << -shift;

        // Special treatment for multiples of 64
        if ((shift & 63) == 0)
        {
            return (shift & 192) switch
            {
                192 => new MatrixMask()
                {
                    LowLow = value.HighHigh
                },
                128 => new MatrixMask()
                {
                    LowLow = value.HighLow,
                    LowHigh = value.HighHigh
                },
                64 => new MatrixMask()
                {
                    LowLow = value.LowHigh,
                    LowHigh = value.HighLow,
                    HighLow = value.HighHigh
                },
                _ => value
            };
        }

        shift &= 255;
        return shift switch
        {
            > 192 => new MatrixMask()
            {
                LowLow = value.HighHigh >> (shift - 192)
            },
            > 128 => new MatrixMask()
            {
                LowLow = (value.HighLow >> (shift - 128)) | (value.HighHigh << (192 - shift)),
                LowHigh = value.HighHigh >> (shift - 128)
            },
            > 64 => new MatrixMask()
            {
                LowLow = (value.LowHigh >> (shift - 64)) | (value.HighLow << (128 - shift)),
                LowHigh = (value.HighLow >> (shift - 64)) | (value.HighHigh << (128 - shift)),
                HighLow = value.HighHigh >> (shift - 64)
            },
            _ => new MatrixMask()
            {
                LowLow = (value.LowLow >> shift) | (value.LowHigh << (64 - shift)),
                LowHigh = (value.LowHigh >> shift) | (value.HighLow << (64 - shift)),
                HighLow = (value.HighLow >> shift) | (value.HighHigh << (64 - shift)),
                HighHigh = value.HighHigh >> shift
            }
        };
    }
    #endregion
    public static bool operator ==(MatrixMask left, MatrixMask right) =>
        left.LowLow == right.LowLow &&
        left.LowHigh == right.LowHigh &&
        left.HighLow == right.HighLow &&
        left.HighHigh == right.HighHigh;
    public static bool operator !=(MatrixMask left, MatrixMask right) =>
        left.LowLow != right.LowLow ||
        left.LowHigh != right.LowHigh ||
        left.HighLow != right.HighLow ||
        left.HighHigh != right.HighHigh;

    public int PopCount() =>
        BitOperations.PopCount(HighHigh & 0x03FFFFFFFFFFFFFFUL) + // Exclude top 6 bits
        BitOperations.PopCount(HighLow) +
        BitOperations.PopCount(LowHigh) +
        BitOperations.PopCount(LowLow & 0xFFFFFFFFFFFFFC00UL); // Exclude bottom 10 bits

    public ulong GetRow(int row) =>
        ((++row) switch
        {
            < 6 => LowLow >> (row * 10),
            6 => (LowLow >> 60) | (LowHigh << 4),
            < 12 => LowHigh >> ((row - 6) * 10 - 4),
            12 => (LowHigh >> 56) | (HighLow << 8),
            < 19 => HighLow >> ((row - 12) * 10 - 8),
            19 => (HighLow >> 62) | (HighHigh << 2),
            < 25 => HighHigh >> ((row - 19) * 10 - 2),
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        }) & FULL_LINE;

    public uint[] GetRows()
    {
        uint[] rows = new uint[24];
        int i = 0, shift;
        for (shift = 10; i < 5; shift += 10)
            rows[i++] = (uint)((LowLow >> shift) & FULL_LINE);
        rows[i++] = (uint)((LowLow >> 60) | (LowHigh << 4) & FULL_LINE);
        if (rows[i - 1] == 0) return rows;

        for (shift = 6; i < 11; shift += 10)
            rows[i++] = (uint)((LowHigh >> shift) & FULL_LINE);
        rows[i++] = (uint)((LowHigh >> 56) | (HighLow << 8) & FULL_LINE);
        if (rows[i - 1] == 0) return rows;

        for (shift = 2; i < 18; shift += 10)
            rows[i++] = (uint)((HighLow >> shift) & FULL_LINE);
        rows[i++] = (uint)((HighLow >> 62) | (HighHigh << 2) & FULL_LINE);
        if (rows[i - 1] == 0) return rows;

        for (shift = 8; i < 24; shift += 10)
            rows[i++] = (uint)((HighHigh >> shift) & FULL_LINE);

        return rows;
    }

    public bool Intersects(Piece piece, int x, int y)
    {
        ulong low, high;
        PieceMask mask = piece.GetMask(x, y);
        switch (mask.Offset)
        {
            case 0:
                low = mask.Low & LowLow;
                high = mask.High & LowHigh;
                //return (low | high) != 0;
                return low != 0 || high != 0;
            case 1:
                low = mask.Low & LowHigh;
                high = mask.High & HighLow;
                //return (low | high) != 0;
                return low != 0 || high != 0;
            case 2:
                low = mask.Low & HighLow;
                high = mask.High & HighHigh;
                //return (low | high) != 0;
                return low != 0 || high != 0;
            case 3:
                return (mask.Low & HighHigh) != 0;
            default:
                return false;
        }
    }

    public static explicit operator MatrixMask(PieceMask value) => value.Offset switch
    {
        0 => new MatrixMask() { LowLow = value.Low, LowHigh = value.High },
        1 => new MatrixMask() { LowHigh = value.Low, HighLow = value.High },
        2 => new MatrixMask() { HighLow = value.Low, HighHigh = value.High },
        3 => new MatrixMask() { HighHigh = value.Low },
        _ => new MatrixMask()
    };

    #nullable enable
    public override bool Equals(object? obj) =>
        obj is MatrixMask value && this == value;

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(275);
        for (int i = 249; i >= 10; i--)
        {
            sb.Append((this >> i).LowLow & 1);
            if (i % 10 == 0) sb.Append(' ');
        }
        return sb.ToString();
    }

    public ulong[] ToArray() => new ulong[] { LowLow, LowHigh, HighLow, HighHigh };
    
    public override int GetHashCode() =>
        (LowLow.GetHashCode() * 397) ^
        (LowHigh.GetHashCode() * 113) ^
        (HighLow.GetHashCode() / 239) ^
        ((HighHigh.GetHashCode() >> 7) + 43);
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct PieceMask
{
    public ulong Low { get; init; }
    public ulong High { get; init; }
    public int Offset { get; init; }

    public static PieceMask operator ~(PieceMask value) =>
        new PieceMask()
        {
            Low = ~value.Low,
            High = ~value.High,
            Offset = value.Offset
        };
    
    public static bool operator ==(PieceMask left, PieceMask right) =>
        left.Low == right.Low &&
        left.High == right.High &&
        left.Offset == right.Offset;
    public static bool operator !=(PieceMask left, PieceMask right) =>
        left.Low != right.Low ||
        left.High != right.High ||
        left.Offset != right.Offset;

    public override bool Equals(object? obj) =>
        obj is PieceMask value && this == value;

    public override int GetHashCode() =>
       (Low.GetHashCode() * 397) ^
       ((High.GetHashCode() >> 7) + 43);

    public override string ToString()
    {
        MatrixMask mask = new MatrixMask() { LowLow = Low, LowHigh = High };
        mask <<= 64 * Offset;
        return mask.ToString();
    }

}

// Try out having the mask left and bottom alligned to avoid some branches
public sealed class Piece
{
    public const int PIECE_BITS = 7, ROTATION_BITS = 24;
    public const int EMPTY = 0,
                     T = 1,
                     I = 2,
                     L = 3,
                     J = 4,
                     S = 5,
                     Z = 6,
                     O = 7;
    public const int ROTATION_NONE = 0,
                     ROTATION_CW = 8,
                     ROTATION_180 = 16,
                     ROTATION_CCW = 24;
    public const int Garbage = 8,
                     Bedrock = 9; // Trash that can't get cleared

    // [piece][rot][layer]
    private static readonly int[][][] PieceX = {
        new int[][] { new int[] { -1, -1, -1, -1 }, new int[] { -1, -1, -1, -1 }, new int[] { -1, -1, -1, -1 }, new int[] { -1, -1, -1, -1 } }, // Empty
        new int[][] { new int[] { -1, -1, -2,  0 }, new int[] { -1, -1,  0, -1 }, new int[] { -1, -2,  0, -1 }, new int[] { -1, -1, -2, -1 } }, // T
        new int[][] { new int[] { -1, -2,  0,  1 }, new int[] {  0,  0,  0,  0 }, new int[] { -2, -1,  0,  1 }, new int[] { -1, -1, -1, -1 } }, // I
        new int[][] { new int[] {  0, -2,  0, -1 }, new int[] { -1, -1, -1,  0 }, new int[] { -1, -2,  0, -2 }, new int[] { -1, -2, -1, -1 } }, // L
        new int[][] { new int[] { -2, -2,  0, -1 }, new int[] { -1,  0, -1, -1 }, new int[] { -1, -2,  0,  0 }, new int[] { -1, -1, -1, -2 } }, // J
        new int[][] { new int[] { -1,  0, -1, -2 }, new int[] { -1, -1,  0,  0 }, new int[] { -1,  0, -1, -2 }, new int[] { -2, -1, -2, -1 } }, // S
        new int[][] { new int[] { -2, -1, -1,  0 }, new int[] {  0,  0, -1, -1 }, new int[] { -1, -2, -1,  0 }, new int[] { -1, -1, -2, -2 } }, // Z
        new int[][] { new int[] { -1,  0, -1,  0 }, new int[] { -1,  0, -1,  0 }, new int[] { -1,  0, -1,  0 }, new int[] { -1,  0, -1,  0 } }  // O
    };
    // Innermost arrays must be sorted in ascending order
    private static readonly int[][][] PieceY = {
        new int[][] { new int[] { -1, -1, -1, -1 }, new int[] { -1, -1, -1, -1 }, new int[] { -1, -1, -1, -1 }, new int[] { -1, -1, -1, -1 } }, // Empty
        new int[][] { new int[] { -2, -1, -1, -1 }, new int[] { -2, -1, -1,  0 }, new int[] { -1, -1, -1,  0 }, new int[] { -2, -1, -1,  0 } }, // T
        new int[][] { new int[] { -1, -1, -1, -1 }, new int[] { -2, -1,  0,  1 }, new int[] {  0,  0,  0,  0 }, new int[] { -2, -1,  0,  1 } }, // I
        new int[][] { new int[] { -2, -1, -1, -1 }, new int[] { -2, -1,  0,  0 }, new int[] { -1, -1, -1,  0 }, new int[] { -2, -2, -1,  0 } }, // L
        new int[][] { new int[] { -2, -1, -1, -1 }, new int[] { -2, -2, -1,  0 }, new int[] { -1, -1, -1,  0 }, new int[] { -2, -1,  0,  0 } }, // J
        new int[][] { new int[] { -2, -2, -1, -1 }, new int[] { -2, -1, -1,  0 }, new int[] { -1, -1,  0,  0 }, new int[] { -2, -1, -1,  0 } }, // S
        new int[][] { new int[] { -2, -2, -1, -1 }, new int[] { -2, -1, -1,  0 }, new int[] { -1, -1,  0,  0 }, new int[] { -2, -1, -1,  0 } }, // Z
        new int[][] { new int[] { -2, -2, -1, -1 }, new int[] { -2, -2, -1, -1 }, new int[] { -2, -2, -1, -1 }, new int[] { -2, -2, -1, -1 } }  // O
    };

    private static readonly int[] KicksX = { 0, -1, -1, 0, -1 }, IKicksX = { 0, -2, 1, -2, 1 };
    private static readonly int[] KicksY = { 0, 0, 1, -2, -2 }, IKicksY = { 0, 0, 0, -1, 2 };

    private static readonly Piece[] Pieces = GetPieces();


    public readonly int Id;
    public readonly int PieceType, R;
    public readonly int Highest, Lowest, Height;
    public readonly int MinX, MaxX, MinY;
    private readonly PieceMask _Mask;
    private readonly int[] _X, _Y;
    private readonly int[] _KicksCWX, _KicksCWY;
    private readonly int[] _KicksCCWX, _KicksCCWY;

    private Piece(int id)
    {
        Id = id;
        PieceType = id & PIECE_BITS;
        R = id & ROTATION_BITS;

        _X = PieceX[PieceType][R >> 3];
        _Y = PieceY[PieceType][R >> 3];

        MinX = -_X.Min();
        MaxX = 9 - _X.Max();
        MinY = _Y.Max();
        Highest = -_Y.Min();
        Lowest = -_Y.Max();
        Height = Highest - Lowest + 1;

        GetKicksTable(true, this, out _KicksCWX, out _KicksCWY);
        GetKicksTable(false, this, out _KicksCCWX, out _KicksCCWY);

        // Center at (0, 1)
        ulong mask = 0;
        for (int i = 0; i < 4; i++)
        {
            int pos = -_X[i] + 10 * (1 - _Y[i]) + 9;
            mask |= 1UL << pos;
        }
        _Mask = new PieceMask() { Low = mask };
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

    public int X(int i) => _X[i];
    public int Y(int i) => _Y[i];
    public int KicksCWX(int i) => _KicksCWX[i];
    public int KicksCWY(int i) => _KicksCWY[i];
    //public int Kicks180X(int i) => 0;
    //public int Kicks180Y(int i) => 0;
    public int KicksCCWX(int i) => _KicksCCWX[i];
    public int KicksCCWY(int i) => _KicksCCWY[i];

    public static implicit operator Piece(int i) => Pieces[i & (PIECE_BITS | ROTATION_BITS)];
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PieceMask GetMask(int x, int y)
    {
        int shift = 10 * y - x;

        // Note: offset and high should always be zero for Mask
        if (shift <= 0)
        {
            return new PieceMask() { Low = _Mask.Low >> -shift };
        }
        else
        {
            int offset = shift >> 6;
            shift &= 63;
            ulong low = _Mask.Low << shift;
            ulong high = shift == 0 ? 0 : _Mask.Low >> (64 - shift);
            //ulong high = Mask.Low >> (63 - shift);
            //high >>= 1;

            //return low == 0 ? new PieceMask() { Low = high, Offset = offset + 1 } :
            //                  new PieceMask() { Low = low, High = high, Offset = offset };
            return new PieceMask() { Low = low, High = high, Offset = offset }; // Always keeping low non-zero results in more conditionals
        }
    }
}

public class GameBase
{
    public const int START_X = 5, START_Y = 19;

    // Try out array of heights as well
    public int Highest { get; private set; } = 0;
    public MatrixMask Matrix = new MatrixMask();
    internal int X, Y;

    public Piece Current { get; internal set; } = Piece.EMPTY;
    public Piece Hold { get; internal set; } = Piece.EMPTY;
    public Piece[] Next { get; protected set; } = Array.Empty<Piece>();

    protected Random PieceRand = new Random();
    protected int BagIndex;
    protected Piece[] Bag = new Piece[] { Piece.T, Piece.I, Piece.L, Piece.J, Piece.S, Piece.Z, Piece.O };

    protected GameBase()
    {
        BagIndex = Bag.Length;
    }

    public GameBase(int next_length, int seed) : this()
    {
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
                Piece temp = Bag[swapIndex];
                Bag[swapIndex] = Bag[i];
                Bag[i] = temp;
            }
            BagIndex = 0;
        }

        return Bag[BagIndex++];
    }

    public bool Fits(Piece piece, int x, int y)
    {
        if (x < piece.MinX || x > piece.MaxX || y < piece.MinY) return false;
        // if (Y + piece.Lowest > Highest) return true;

        return !Matrix.Intersects(piece, x, y);
    }

    public bool OnGround()
    {
        // if (Y + Current.Lowest > Highest) return false; // Ald checked in most cases, wouldn't speed things up
        if (Y <= Current.MinY) return true;

        return Matrix.Intersects(Current, X, Y - 1);
    }

    public int TSpinType(bool rotatedLast)
    {
        const int ALL_CORNERS = (0b101 << 20) | 0b101;
        const int NO_BR = (0b101 << 20) | 0b100;
        const int NO_BL = (0b101 << 20) | 0b001;
        const int NO_TL = (0b001 << 20) | 0b101;
        const int NO_TR = (0b100 << 20) | 0b101;

        if (!rotatedLast || (Current.PieceType != Piece.T)) return 0;

        int i = 10 * Y - X + 19; // 19 = 10 + T.MaxX
        ulong corners = (Matrix >> i).LowLow & ALL_CORNERS;

        if (corners == ALL_CORNERS) return 3;
        switch (Current.R)
        {
            case Piece.ROTATION_NONE:
                if (Y == Current.MinY) corners |= 0b101;
                if (corners == NO_BR || corners == NO_BL) return 3;
                if (corners == NO_TR || corners == NO_TL) return 2;
                break;
            case Piece.ROTATION_CW:
                if (X == Current.MinX) corners |= (0b100 << 20) | 0b100;
                if (corners == NO_TL || corners == NO_BL) return 3;
                if (corners == NO_TR || corners == NO_BR) return 2;
                break;
            case Piece.ROTATION_180:
                if (corners == NO_TR || corners == NO_TL) return 3;
                if (corners == NO_BR || corners == NO_BL) return 2;
                break;
            case Piece.ROTATION_CCW:
                if (X == Current.MaxX) corners |= (0b001 << 20) | 0b001;
                if (corners == NO_TR || corners == NO_BR) return 3;
                if (corners == NO_TL || corners == NO_BL) return 2;
                break;
        }

        return 0;
    }

    public bool TryRotateCW()
    {
        Piece rotated = Current + Piece.ROTATION_CW;
        for (int i = 0; i < 5; i++)
        {
            int new_x = X + Current.KicksCWX(i);
            int new_y = Y + Current.KicksCWY(i);

            if (!Fits(rotated, new_x, new_y))
                continue;

            X = new_x;
            Y = new_y;
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
            int new_x = X + Current.KicksCCWX(i);
            int new_y = Y + Current.KicksCCWY(i);

            if (!Fits(rotated, new_x, new_y))
                continue;

            X = new_x;
            Y = new_y;
            Current = rotated;
            return true;
        }
        return false;
    }

    public bool TryRotate180()
    {
        Piece rotated = Current + Piece.ROTATION_180;

        if (!Fits(rotated, X, Y))
            return false;

        Current = rotated;
        return true;
    }
    
    /// <returns>true if the piece was rotated; Otherwise, false.</returns> 
    /// <remarks>Currently no 180 kicks.</remarks>
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
                if (Matrix.Intersects(Current, X + 1, Y))
                    return false;
                X++;
                return true;
            }
        }
        else
        {
            if (X > Current.MinX)
            {
                if (Matrix.Intersects(Current, X - 1, Y))
                    return false;
                X--;
                return true;
            }
        }

        return false;
    }

    /// <returns>true if the piece was moved; Otherwise, false.</returns> 
    public bool TrySlide(int dx)
    {
        if (Y + Current.Lowest > Highest)
        {
            int old_x = X;
            X += dx;
            X = Math.Clamp(X, Current.MinX, Current.MaxX);
            return X != old_x;
        }

        bool right = dx > 0, moved = false;
        while (dx != 0)
        {
            if (!TrySlide(right)) break;
            moved = true;
            dx -= right ? 1 : -1;
        }

        return moved;
    }

    /// <returns>The number of blocks moved down by.</returns> 
    public int TryDrop(int dy)
    {
        int diff = Y + Current.Lowest - Highest - 1;
        if (diff >= dy)
        {
            Y -= dy;
            return dy;
        }
        int moved = Math.Max(0, diff);
        dy -= moved;
        Y -= moved;
        for (; dy > 0 && !OnGround(); dy--, Y--) moved++;
        return moved;
    }

    public void ResetPiece()
    {
        // Move new piece to top
        X = START_X; Y = START_Y;
        Current = Current.PieceType;
        // Drop immediately if possible
        if (!OnGround()) Y--;
    }

    public int[] Place(out int cleared)
    {
        // Put piece into matrix
        Highest = Math.Max(Highest, Y + Current.Highest);
        Matrix |= Current.GetMask(X, Y);

        // Find cleared lines
        cleared = 0;
        int index = 0;
        int[] clears = new int[4];
        int end = Y + Current.Lowest;
        for (int y = Y + Current.Highest; y >= end; y--)
        {
            // List clears as chunks as they usually aren't split up
            int old_y = y;
            while (Matrix.GetRow(y) == MatrixMask.FULL_LINE && y >= end) y--;
            if (old_y == y) continue;
            
            int dy = old_y - y;
            cleared += dy;
            clears[index++] = y + 1;
            clears[index++] = dy;
        }

        // Clear chunks and move lines down
        if (clears[1] > 0)
        {
            MatrixMask top = (Matrix >> (clears[1] * 10)) & MatrixMask.InverseHeightMasks[clears[0]];
            Matrix = (Matrix & MatrixMask.HeightMasks[clears[0]]) | top;
        }
        if (clears[3] > 0)
        {
            MatrixMask top = (Matrix >> (clears[3] * 10)) & MatrixMask.InverseHeightMasks[clears[2]];
            Matrix = (Matrix & MatrixMask.HeightMasks[clears[2]]) | top;
        }

        Highest -= cleared;

        return clears;
    }

    public void Unplace(int[] clears)
    {
        // Add back cleared lines
        if (clears[3] > 0)
        {
            MatrixMask bottom = (Matrix & MatrixMask.HeightMasks[clears[2]]) |
                     (MatrixMask.InverseHeightMasks[clears[2]] & MatrixMask.HeightMasks[clears[2] + clears[3]]);
            Matrix = ((Matrix & MatrixMask.InverseHeightMasks[clears[2]]) << (clears[3] * 10)) | bottom;
            Highest += clears[3];
        }
        if (clears[1] > 0)
        {
            MatrixMask bottom = (Matrix & MatrixMask.HeightMasks[clears[0]]) |
                     (MatrixMask.InverseHeightMasks[clears[0]] & MatrixMask.HeightMasks[clears[0] + clears[1]]);
            Matrix = ((Matrix & MatrixMask.InverseHeightMasks[clears[0]]) << (clears[1] * 10)) | bottom;
            Highest += clears[1];
        }
        
        // Remove piece
        Matrix &= ~Current.GetMask(X, Y);

        while (Highest >= 0)
        {
            if (Matrix.GetRow(Highest) != 0) break;
            Highest--;
        }
    }

    public void CheckHeight()
    {
        Highest = 0;
        while (Matrix.GetRow(Highest) != 0)
        {
            Highest++;
            if (Highest >= 24) break;
        }
        Highest--;
    }

    public GameBase Clone() => 
        new GameBase(Next.Length, 0)
        {
            Matrix = Matrix,
            Highest = Highest,
            X = X,
            Y = Y,
            Current = Current,
            Hold = Hold,
            Next = (Piece[])Next.Clone()
        };

    public bool PathFind(Piece end_piece, int end_x, int end_y, out List<Moves> moves)
    {
        moves = new List<Moves>();
        // No possible route if piece is out of bounds
        if (end_x < end_piece.MinX || end_x > end_piece.MaxX || end_y < end_piece.MinY || end_y >= 24)
            return false;

        GameBase clone = Clone();
        // Check if hold is needed
        bool hold = false;
        if (clone.Current.PieceType != end_piece.PieceType)
        {
            clone.Current = (clone.Hold == Piece.EMPTY) ? clone.Next[0] : clone.Hold;
            // No possible route if holding doesn't give correct piece type
            if (clone.Current.PieceType != end_piece.PieceType)
                return false;
            hold = true;
        }

        // Can't pathfind to floating piece
        clone.Current = end_piece;
        clone.X = end_x;
        clone.Y = end_y;
        if (!clone.OnGround()) return false;

        // Queue of nodes to try
        Queue<(Piece p, int x, int y, List<Moves> m)> nodes = new Queue<(Piece, int, int, List<Moves>)>();
        // Set of seen nodes
        HashSet<int> seen = new HashSet<int>();
        // Breadth first search
        clone.ResetPiece();
        nodes.Enqueue((clone.Current, clone.X, clone.Y, new List<Moves>()));
        seen.Add((clone.Current, clone.X, clone.Y).GetHashCode());
        PieceMask end_piece_mask = end_piece.GetMask(end_x, end_y);
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
            if (piece.GetMask(clone.X, clone.Y) == end_piece_mask)
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
            int hash = clone.Current | (clone.X << 5) | (clone.Y << 9);
            if (!seen.Contains(hash))
            {
                seen.Add(hash);
                List<Moves> new_m = new List<Moves>(m) { move };
                nodes.Enqueue((clone.Current, clone.X, clone.Y, new_m));
            }
        }
    }
}

// TODO: change callbacks to events or use tasks?
public sealed class Game : GameBase
{
    public const int GAMEWIDTH = 44, GAMEHEIGHT = 24;
    const string BLOCKSOLID = "██", BLOCKGHOST = "▒▒";
    static readonly string[] ClearText = { "SINGLE", "DOUBLE", "TRIPLE", "TETRIS" };
    public static readonly ConsoleColor[] PieceColors =
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

    static readonly ConsoleColor[] GarbageLineColor = new ConsoleColor[10].Select(x => PieceColors[Piece.Garbage]).ToArray();

    public static Game[] Games { get; private set; }
    public static readonly Stopwatch GlobalTime = Stopwatch.StartNew();
    private static bool _IsPaused = false;
    public static bool IsPaused
    {
        get => _IsPaused;
        set
        {
            _IsPaused = value;
            if (_IsPaused)
            {
                GlobalTime.Stop();
                foreach (Game g in Games)
                {
                    g.LockT.Stop();
                }
            }
            else
            {
                GlobalTime.Start();
                foreach (Game g in Games)
                {
                    if (g.OnGround()) g.LockT.Start();
                    Task.Delay(g.GarbageDelay).ContinueWith(t => g.DrawTrashMeter());
                }
            }
        }
    }

    public int[] LinesTrash { get; private set; } = { 0, 0, 1, 2, 4 };
    public int[] TSpinTrash { get; private set; } = { 0, 2, 4, 6 };
    //public int[] ComboTrash { get; private set; } = { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }; // Tetris 99
    public int[] ComboTrash { get; private set; } = { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }; // Jstris
    public int[] PCTrash { get; private set; } = { 0, 10, 10, 10, 10 };

    #region // Fields and Properties
    int XOffset = 0;
    int YOffset = 0;

    public readonly ConsoleColor[][] MatrixColors = new ConsoleColor[24][]; // [y][x]

    private string _Name = "";
    public string Name
    {
        get => _Name;
        set
        {
            _Name = value;
            // Center text
            int space = GAMEWIDTH - value.Length;
            int left_space = space / 2;
            if (space < 0)
                WriteAt(0, -1, ConsoleColor.White, value.Substring(-left_space, 44));
            else
                WriteAt(0, -1, ConsoleColor.White, value.PadLeft(left_space + value.Length).PadRight(GAMEWIDTH));
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

    public double G = 0.05, SoftG = 1;
    double Vel = 0;
    readonly Queue<Moves> MoveQueue = new Queue<Moves>();

    double LastFrameTime;
    public int LockDelay = 1000, EraseDelay = 1000, GarbageDelay = 200; // In miliseconds
    public int AutoLockGrace = 15;
    int MoveCount = 0;
    bool IsLastMoveRotate = false, AlreadyHeld = false;
    readonly Stopwatch LockT = new Stopwatch();
    readonly List<CancellationTokenSource> EraseCancelTokenSrcs = new List<CancellationTokenSource>();

    readonly Random GarbageRand;
    readonly List<(int Lines, long Time)> Garbage = new List<(int, long)>();

    long TargetChangeInteval = 500, LastTargetChangeTime; // In miliseconds
    public TargetModes TargetMode = TargetModes.Random;
    List<Game> Targets = new List<Game>();
    #endregion

    #region // Stats
    public double StartTime { get; private set; }
    public int Sent { get; private set; } = 0;
    public double APL
    {
        get
        {
            if (Sent == 0) return 0;
            return (double)Sent / Lines;
        }
    }

    public int PiecesPlaced { get; private set; } = 0;
    public double PPS
    {
        get
        {
            if (PiecesPlaced == 0) return 0;
            return PiecesPlaced / (GlobalTime.Elapsed.TotalSeconds - StartTime);
        }
    }

    public int KeysPressed { get; private set; } = 0;
    public double KPP
    {
        get
        {
            if (PiecesPlaced == 0) return 0;
            return (double)KeysPressed / PiecesPlaced;
        }
    }
    #endregion

    public Game() : base(6, Guid.NewGuid().GetHashCode())
    {
        for (int i = 0; i < MatrixColors.Length; i++) MatrixColors[i] = new ConsoleColor[10];
        GarbageRand = new Random(Guid.NewGuid().GetHashCode());
    }

    public Game(int next_length, int seed) : base(next_length, seed)
    {
        for (int i = 0; i < MatrixColors.Length; i++) MatrixColors[i] = new ConsoleColor[10];
        GarbageRand = new Random(seed.GetHashCode());
    }

    public void Restart()
    {
        Matrix = new MatrixMask();
        CheckHeight();
        for (int i = 0; i < MatrixColors.Length; i++) MatrixColors[i] = new ConsoleColor[MatrixColors[i].Length];
        BagIndex = Bag.Length;
        for (int i = 0; i < Next.Length; i++) Next[i] = NextPiece();
        Hold = Piece.EMPTY;

        B2B = -1; Combo = -1;

        IsDead = false;
        Score = 0;
        Lines = 0;
        Sent = 0;
        PiecesPlaced = 0;

        Vel = 0;
        IsLastMoveRotate = false;
        AlreadyHeld = false;
        MoveCount = 0;
        MoveQueue.Clear();

        StartTime = GlobalTime.Elapsed.TotalSeconds;
        LastFrameTime = StartTime;
        LastTargetChangeTime = GlobalTime.ElapsedMilliseconds;
        LockT.Reset();
        Garbage.Clear();
        //TargetMode = TargetModes.Random;
        Targets.Clear();

        SpawnNextPiece();
        DrawAll();
    }

    public void Tick()
    {
        if (IsDead || IsPaused) return;

        // Timekeeping
        double deltaT = GlobalTime.Elapsed.TotalSeconds - LastFrameTime;
        LastFrameTime = GlobalTime.Elapsed.TotalSeconds;

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
            LockT.Start();
            // Lock piece
            if ((MoveCount > AutoLockGrace) || (LockT.ElapsedMilliseconds > LockDelay))
                PlacePiece();
        }
        else
        {
            if (MoveCount < AutoLockGrace) LockT.Reset();
            Vel -= Drop((int)Vel, softDrop ? 1 : 0); // Round Vel down
        }

        // Write stats
        WriteAt(0, 20, ConsoleColor.White, $"Sent:{Sent}".PadRight(11));
        WriteAt(0, 21, ConsoleColor.White, $"PPS: {Math.Round(PPS, 3)}".PadRight(11));
        WriteAt(0, 22, ConsoleColor.White, $"APL: {Math.Round(APL, 3)}".PadRight(11));
    }

    public static void SetGames(Game[] games)
    {
        Games = games;
        GlobalTime.Restart();

        // Find width and height (~2:1 ratio)
        int width = (int)Math.Sqrt(Games.Length / 2) * 2, height = width / 2;
        if (width * height < Games.Length) width++;
        if (width * height < Games.Length) height++;

        FConsole.Set(width * (GAMEWIDTH + 1) + 1, height * GAMEHEIGHT + 1);

        foreach (Game g in Games) g.ClearScreen();

        // Set up and re-draw games
        for (int i = 0; i < Games.Length; i++)
        {
            Games[i].XOffset = (i % width) * (GAMEWIDTH + 1) + 1;
            Games[i].YOffset = (i / width) * GAMEHEIGHT + 1;
            if (Games[i].IsDead) Games[i].Restart();
            Games[i].DrawAll();
        }
    }

    #region // Player methods
    public void Play(Moves move)
    {
        if (IsDead || IsPaused) return;

        MoveQueue.Enqueue(move);
        KeysPressed++;
    }

    void Slide(int dx)
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

    int Drop(int dy, int scorePerDrop)
    {
        DrawCurrent(true);
        int moved = TryDrop(dy);
        Score += moved * scorePerDrop;
        if (moved != 0)
        {
            //Playsfx(Sounds.smth);
            IsLastMoveRotate = false;
        }
        DrawCurrent(false);

        return moved;
    }

    void Rotate(int dr)
    {
        DrawCurrent(true);
        if (TryRotate(dr))
        {
            IsLastMoveRotate = true;
            if (MoveCount++ < AutoLockGrace) LockT.Restart();
        }
        DrawCurrent(false);
    }

    void HoldPiece()
    {
        if (AlreadyHeld) return;

        AlreadyHeld = true;
        IsLastMoveRotate = false;

        // Undraw
        DrawCurrent(true);
        DrawPieceAt(Hold, 5, 4, true);

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
        DrawPieceAt(Hold, 5, 4, false);
        DrawCurrent(false);
        LockT.Reset();
    }
    
    void PlacePiece()
    {
        int tspin = TSpinType(IsLastMoveRotate); //0 = no spin, 2 = mini, 3 = t-spin
        // Place piece in MatrixColors
        for (int i = 0; i < 4; i++)
            MatrixColors[Y - Current.Y(i)][X + Current.X(i)] = PieceColors[Current.PieceType];
        // Clear lines
        int[] clears = Place(out int cleared);
        for (int i = 0; i < clears.Length; i += 2)
        {
            if (clears[i + 1] == 0) break;
            for (int j = clears[i]; j < MatrixColors.Length - clears[i + 1]; j++)
            {
                MatrixColors[j] = MatrixColors[j + clears[i + 1]];
            }
        }
        // Add new empty rows
        for (int i = MatrixColors.Length - cleared; i < MatrixColors.Length; i++)
            MatrixColors[i] = new ConsoleColor[10].Select(x => ConsoleColor.Black).ToArray();
        // Line clears
        int score_add = 0;
        score_add += new int[] { 0, 100, 300, 500, 800 }[cleared];
        // T-spins
        if (tspin == 3) score_add += new int[] { 400, 700, 900, 1100 }[cleared];
        if (tspin == 2) score_add += 100;
        // Perfect clear
        bool pc = Matrix.GetRow(0) == 0;
        if (pc) score_add += new int[] { 800, 1200, 1800, 2000 }[cleared - 1];
        // B2B
        bool is_hard_clear = tspin + cleared > 3;
        if (tspin == 0 && cleared != 4 && cleared != 0) B2B = -1; // Reset B2B
        else if (is_hard_clear) B2B++;
        bool b2b_active = is_hard_clear && B2B > 0;
        if (b2b_active) score_add += score_add / 2; //B2B bonus
        // Combo
        Combo = cleared == 0 ? -1 : Combo + 1;
        if (Combo > -1) score_add += 50 * Combo;
        // Score
        Score += score_add * Level;
        // Check if leveled up
        int old_level = Level;
        Lines += cleared;
        if (old_level < Level)
        {

        }

        // Write stats to console
        WriteAt(1, 11, ConsoleColor.White, Level.ToString());
        // Write clear stats and play sound
        if (tspin > 0 || cleared > 0 || Combo > 0 || pc)
        {
            foreach (var ts in EraseCancelTokenSrcs)
            {
                ts.Cancel();
                ts.Dispose();
            }
            EraseCancelTokenSrcs.Clear();

            CancellationTokenSource token_source = new CancellationTokenSource();
            EraseCancelTokenSrcs.Add(token_source);
            Task.Delay(EraseDelay).ContinueWith(t => {
                if (token_source.IsCancellationRequested) return;
                EraseClearStats();
                EraseCancelTokenSrcs.Remove(token_source);
                token_source.Dispose();
            }, token_source.Token);
            EraseClearStats();
        }
        if (b2b_active) WriteAt(4, 14, ConsoleColor.White, "B2B");
        if (tspin == 2) WriteAt(0, 15, ConsoleColor.White, "T-SPIN MINI");
        else if (tspin == 3) WriteAt(2, 15, ConsoleColor.White, "T-SPIN");
        if (cleared > 0) WriteAt(2, 16, ConsoleColor.White, ClearText[cleared - 1]);
        if (Combo > 0) WriteAt(1, 17, ConsoleColor.White, Combo + " COMBO!");
        if (pc) WriteAt(0, 18, ConsoleColor.White, "ALL CLEAR!");

        // Trash sent
        int trash = pc ? PCTrash[cleared] :
                    tspin == 3 ? TSpinTrash[cleared] :
                                 LinesTrash[cleared];
        if (Combo > 0) trash += ComboTrash[Math.Min(Combo, ComboTrash.Length) - 1];
        if (b2b_active) trash++;

        // Stats
        Sent += trash;
        PiecesPlaced++;

        // Garbage
        // Garbage cancelling
        while (Garbage.Count != 0 && trash != 0)
        {
            if (Garbage[0].Lines <= trash)
            {
                trash -= Garbage[0].Lines;
                Garbage.RemoveAt(0);
            }
            else
            {
                Garbage[0] = (Garbage[0].Lines - trash, Garbage[0].Time);
                trash = 0;
            }
        }
        // Dump the trash
        if (cleared == 0)
        {
            while (Garbage.Count > 0)
            {
                if (GlobalTime.ElapsedMilliseconds - Garbage[0].Time <= GarbageDelay) break;

                int lines_to_add = Garbage[0].Lines;
                Garbage.RemoveAt(0);
                int hole = GarbageRand.Next(10);
                int bedrock_height = 0;
                while (Matrix.GetRow(bedrock_height) == MatrixMask.FULL_LINE) bedrock_height++;
                bedrock_height--;
                // Move stuff up
                MatrixMask top = (Matrix & MatrixMask.InverseHeightMasks[bedrock_height + 1]) << (lines_to_add * 10);
                Matrix = top | (Matrix & MatrixMask.HeightMasks[bedrock_height + 1]);
                for (int y = MatrixColors.Length - lines_to_add - 1; y > bedrock_height; y--)
                    MatrixColors[y + lines_to_add] = MatrixColors[y];
                // Add garbage
                for (int y = bedrock_height + 1; y < bedrock_height + lines_to_add + 1; y++)
                {
                    MatrixMask garbage_line = new MatrixMask() { LowLow = ~(1UL << (9 - hole)) << 10 } & MatrixMask.HeightMasks[1];
                    garbage_line <<= y * 10;
                    Matrix |= garbage_line;
                    
                    MatrixColors[y] = new ConsoleColor[10];
                    Buffer.BlockCopy(GarbageLineColor, 0, MatrixColors[y], 0, 10 * sizeof(ConsoleColor));
                    // Add hole
                    MatrixColors[y][hole] = PieceColors[Piece.EMPTY];
                }
            }
        }
        DrawTrashMeter();
        if (trash > 0) SendTrash(trash);
        
        // Redraw matrix
        DrawMatrix();

        CheckHeight();
        SpawnNextPiece();
    }

    void SendTrash(int trash)
    {
        long time = GlobalTime.ElapsedMilliseconds;
        // Select targets
        switch (TargetMode)
        {
            case TargetModes.Random:
                if (time - LastTargetChangeTime > TargetChangeInteval)
                {
                    LastTargetChangeTime = time;
                    Targets.Clear();
                    List<Game> aliveGames = Games.Where(x => !x.IsDead).ToList();
                    if (aliveGames.Count <= 1) break;
                    int i = new Random().Next(aliveGames.Count - 1);
                    if (i >= aliveGames.IndexOf(this)) i = (i + 1) % aliveGames.Count;
                    Targets.Add(aliveGames[i]);
                }
                break;
            case TargetModes.All:
                Targets = Games.Where(x => !x.IsDead).ToList();
                break;
            case TargetModes.Self:
                Targets.Clear();
                Targets.Add(this);
                break;
            case TargetModes.None:
                Targets.Clear();
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
            DrawPieceAt(Next[i], 39, 4 + 3 * i, true);
        // Update current and next
        Current = Next[0];
        for (int i = 1; i < Next.Length; i++) Next[i - 1] = Next[i];
        Next[^1] = NextPiece();
        // Reset piece
        ResetPiece();
        IsLastMoveRotate = false;
        AlreadyHeld = false;
        Vel = 0;
        MoveCount = 0;
        LockT.Reset();
        // Check for block out
        if (Matrix.Intersects(Current, X, Y)) IsDead = true;
        // Check for lock out
        //if (Y + Current.Lowest >= 20) IsDead = true;
        // Draw next
        for (int i = 0; i < Math.Min(6, Next.Length); i++)
            DrawPieceAt(Next[i], 39, 4 + 3 * i, false);
        // Draw current
        DrawCurrent(false);
    }
    #endregion

    #region // Drawing methods
    public void WriteAt(int x, int y, ConsoleColor color, string text) =>
        FConsole.WriteAt(text, x + XOffset, y + YOffset, foreground: color);

    void DrawCurrent(bool black)
    {
        // Ghost
        GameBase clone = this.Clone();
        clone.TryDrop(40);
        for (int i = 0; i < 4; i++)
            if (clone.Y - Current.Y(i) < 20) // If visible
                WriteAt((clone.X + Current.X(i)) * 2 + 12, -clone.Y + Current.Y(i) + 21, black ? ConsoleColor.Black : PieceColors[Current.PieceType], BLOCKGHOST);
        // Piece
        for (int i = 0; i < 4; i++)
            if (Y - Current.Y(i) < 20) // If visible
                WriteAt((X + Current.X(i)) * 2 + 12, -Y + Current.Y(i) + 21, black ? ConsoleColor.Black : PieceColors[Current.PieceType], BLOCKSOLID);
    }

    void DrawPieceAt(Piece piece, int x, int y, bool black)
    {
        for (int i = 0; i < 4; i++)
            WriteAt(piece.X(i) * 2 + x, piece.Y(i) + y, black ? ConsoleColor.Black : PieceColors[piece.PieceType], BLOCKSOLID);
    }

    void DrawMatrix()
    {
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 20; y++)
                WriteAt(x * 2 + 12, 21 - y, MatrixColors[y][x], BLOCKSOLID);
    }

    void DrawTrashMeter()
    {
        int y = 21;
        if (Garbage.Count != 0)
        {
            for (int i = 0; i < Garbage.Count; i++)
            {
                ConsoleColor color = GlobalTime.ElapsedMilliseconds - Garbage[i].Time > GarbageDelay ? ConsoleColor.Red : ConsoleColor.Gray;
                for (int j = y; y > j - Garbage[i].Lines && y > 1; y--)
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
    }

    void ClearScreen()
    {
        // Clear console section
        for (int i = 0; i < GAMEHEIGHT; i++)
            WriteAt(0, i, ConsoleColor.White, "".PadLeft(GAMEWIDTH));
    }

    public void DrawAll()
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
            DrawPieceAt(Next[i], 39, 4 + 3 * i, false);
        // Draw hold
        DrawPieceAt(Hold, 5, 4, false);
        // Draw board
        DrawMatrix();
        // Draw current piece
        DrawCurrent(false);
        // Draw trash meter
        DrawTrashMeter();
    }
    #endregion
}

public static class GameManager
{
    public struct GameSettings
    {
        public static readonly string DefaultPath = AppContext.BaseDirectory + "Settings.json";

        public int[] LinesTrash;
        public int[] TSpinTrash;
        public int[] ComboTrash;
        public int[] PCTrash;

        public double G;
        public double SoftG;

        public int LockDelay, EraseDelay, GarbageDelay; // In miliseconds
        public int AutoLockGrace;
        public int TargetChangeInteval;
    }

    public static GameSettings Settings { get; private set; } = LoadSettings();

    public static GameSettings LoadSettings(string? path = null)
    {
        path ??= GameSettings.DefaultPath;
        string jsonString = File.ReadAllText(path);
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        return JsonSerializer.Deserialize<GameSettings>(jsonString, options);
    }

    public static void SaveSettings(string? path = null)
    {
        path ??= GameSettings.DefaultPath;
        var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        string json = JsonSerializer.Serialize(Settings, options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }
}
