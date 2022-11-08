using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Tetris;

[StructLayout(LayoutKind.Sequential)]
public readonly struct MatrixMask
{
    public const ulong FULL_LINE = (1 << 10) - 1;

    public static readonly MatrixMask[] HeightMasks = new MatrixMask[26].Select((_, i) =>
    {
        MatrixMask mask = ~new MatrixMask();    // Set it to all 1s
        mask <<= i * 10;                        // Make the first i rows 0
        return ~mask;                           // Invert it
    }).ToArray();
    public static readonly MatrixMask[] InverseHeightMasks = HeightMasks.Select((m) => ~m & HeightMasks[^1]).ToArray();

    public ulong LowLow { get; init; }
    public ulong LowHigh { get; init; }
    public ulong HighLow { get; init; }
    public ulong HighHigh { get; init; }

    private unsafe ulong this[int i]
    {
        get
        {
            #if DEBUG
            if (i < 0 || i > 7)
                throw new IndexOutOfRangeException();
            #endif

            fixed (void* ptr = &this)
                return *(ulong*)((int*)ptr + i);
        }
        set
        {
            #if DEBUG
            if (i < 0 || i > 7)
                throw new IndexOutOfRangeException();
            #endif

            fixed (void* ptr = &this)
                *(ulong*)((int*)ptr + i) = value;
        }
    }

    #region // Logical operators
    public static MatrixMask operator ~(MatrixMask value) =>
        new MatrixMask()
        {
            LowLow = ~value.LowLow,
            LowHigh = ~value.LowHigh,
            HighLow = ~value.HighLow,
            HighHigh = ~value.HighHigh
        };
    public static MatrixMask operator &(MatrixMask matrix, PieceMask piece)
    {
        MatrixMask mask = matrix;
        mask[piece.Offset] &= piece.Mask;
        return mask;
    }
    public static MatrixMask operator &(MatrixMask left, MatrixMask right) =>
        new MatrixMask()
        {
            LowLow = left.LowLow & right.LowLow,
            LowHigh = left.LowHigh & right.LowHigh,
            HighLow = left.HighLow & right.HighLow,
            HighHigh = left.HighHigh & right.HighHigh
        };
    public static MatrixMask operator |(MatrixMask matrix, PieceMask piece)
    {
        MatrixMask mask = matrix;
        mask[piece.Offset] |= piece.Mask;
        return mask;
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
        MatrixMask mask = matrix;
        mask[piece.Offset] ^= piece.Mask;
        return mask;
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
        BitOperations.PopCount(LowLow);

    public ulong GetRow(int row) =>
        (row switch
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

    public bool Intersects(PieceMask piece) =>
        (this[piece.Offset] & piece.Mask) != 0;

    public static explicit operator MatrixMask(PieceMask piece)
    {
        MatrixMask mask = new MatrixMask();
        mask[piece.Offset] = piece.Mask;
        return mask;
    }

#nullable enable
    public override bool Equals(object? obj) =>
        obj is MatrixMask value && this == value;

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder(275);
        for (int i = 249; i >= 0; i--)
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
    public ulong Mask { get; init; }
    public int Offset { get; init; }

    public static PieceMask operator ~(PieceMask value) =>
        new PieceMask()
        {
            Mask = ~value.Mask,
            Offset = value.Offset
        };

    public static bool operator ==(PieceMask left, PieceMask right) =>
        left.Mask == right.Mask &&
        left.Offset == right.Offset;
    public static bool operator !=(PieceMask left, PieceMask right) =>
        left.Mask != right.Mask ||
        left.Offset != right.Offset;

    public override bool Equals(object? obj) =>
        obj is PieceMask value && this == value;

    public override int GetHashCode() =>
       (Offset.GetHashCode() * 397) ^
       ((Mask.GetHashCode() >> 7) + 43);

    public override string ToString()
    {
        MatrixMask mask = new MatrixMask() { LowLow = Mask };
        mask <<= 64 * Offset;
        return mask.ToString();
    }
}
