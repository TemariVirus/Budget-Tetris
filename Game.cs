using FastConsole;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Tetris
{
    public enum Moves
    {
        None,
        Hold,
        Left,
        Right,
        DASLeft,
        DASRight,
        DASLeftAll,
        DASRightAll,
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
        AllButSelf,
        Self,
        None
    }

    [DataContract]
    public struct GameSettings
    {
        public static readonly string DefaultPath = AppContext.BaseDirectory + "Settings.json";

        public static readonly GameSettings Default = new GameSettings()
        {
            BGMVolume = 0.1f,
            SFXVolume = 0.1f,

            LinesTrash = new int[] { 0, 0, 1, 2, 4 },
            TSpinTrash = new int[] { 0, 2, 4, 6 },
            //ComboTrash = new int[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }, // Tetris 99
            ComboTrash = new int[] { 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }, // Jstris
            PCTrash = new int[] { 0, 10, 10, 10, 10 },
            G = 0.05,
            SoftG = 1,

            LookAheads = 6,

            DASDelay = 133,
            DASInterval = 66,
            LockDelay = 500,
            EraseDelay = 1000,
            GarbageDelay = 500,

            AutoLockGrace = 15,
            TargetChangeInteval = 500,
        };

        [DataMember]
        public float BGMVolume { get; private set; }
        [DataMember]
        public float SFXVolume { get; private set; }

        [DataMember]
        public int[] LinesTrash { get; private set; }
        [DataMember]
        public int[] TSpinTrash { get; private set; }
        [DataMember]
        public int[] ComboTrash { get; private set; }
        [DataMember]
        public int[] PCTrash { get; private set; }

        [DataMember]
        public double G { get; private set; }
        [DataMember]
        public double SoftG { get; private set; }

        [DataMember]
        public int LookAheads { get; private set; }

        // In miliseconds
        [DataMember]
        public int DASDelay { get; private set; }
        [DataMember]
        public int DASInterval { get; private set; }
        [DataMember]
        public int LockDelay { get; private set; }
        [DataMember]
        public int EraseDelay { get; private set; }
        [DataMember]
        public int GarbageDelay { get; private set; }

        [DataMember]
        public int AutoLockGrace { get; private set; }
        [DataMember]
        public int TargetChangeInteval { get; private set; }


        public static GameSettings LoadSettings(string path = null)
        {
            if (path == null)
                path = DefaultPath;
            if (!File.Exists(path))
            {
                if (path == DefaultPath)
                    SaveSettings(Default, DefaultPath);

                return Default;
            }

            var mem_stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path)));
            var serializer = new DataContractJsonSerializer(typeof(GameSettings));
            GameSettings settings;
            try
            {
                settings = (GameSettings)serializer.ReadObject(mem_stream);
            }
            catch
            {
                settings = Default;
            }
            mem_stream.Dispose();
            return settings;
        }

        public static void SaveSettings(GameSettings settings, string path = null)
        {
            if (path == null)
                path = DefaultPath;

            var mem_stream = new MemoryStream();
            var serializer = new DataContractJsonSerializer(typeof(GameSettings));
            serializer.WriteObject(mem_stream, settings);
            mem_stream.Position = 0;
            var sr = new StreamReader(mem_stream);

            File.WriteAllText(path, sr.ReadToEnd());

            sr.Dispose();
            mem_stream.Dispose();
        }
    }

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
            new int[][] { new int[] {  0,  0,  0,  0 }, new int[] { 0, 0, 0, 0 }, new int[] {  0,  0, 0,  0 }, new int[] {  0,  0,  0,  0 } }, // Empty
            new int[][] { new int[] {  0,  0, -1,  1 }, new int[] { 0, 0, 1, 0 }, new int[] {  0, -1, 1,  0 }, new int[] {  0,  0, -1,  0 } }, // T
            new int[][] { new int[] {  0, -1,  1,  2 }, new int[] { 1, 1, 1, 1 }, new int[] { -1,  0, 1,  2 }, new int[] {  0,  0,  0,  0 } }, // I
            new int[][] { new int[] {  1, -1,  1,  0 }, new int[] { 0, 0, 0, 1 }, new int[] {  0, -1, 1, -1 }, new int[] {  0, -1,  0,  0 } }, // L
            new int[][] { new int[] { -1, -1,  1,  0 }, new int[] { 0, 1, 0, 0 }, new int[] {  0, -1, 1,  1 }, new int[] {  0,  0,  0, -1 } }, // J
            new int[][] { new int[] {  0,  1,  0, -1 }, new int[] { 0, 0, 1, 1 }, new int[] {  0,  1, 0, -1 }, new int[] { -1,  0, -1,  0 } }, // S
            new int[][] { new int[] { -1,  0,  0,  1 }, new int[] { 1, 1, 0, 0 }, new int[] {  0, -1, 0,  1 }, new int[] {  0,  0, -1, -1 } }, // Z
            new int[][] { new int[] {  0,  1,  0,  1 }, new int[] { 0, 1, 0, 1 }, new int[] {  0,  1, 0,  1 }, new int[] {  0,  1,  0,  1 } }  // O
        };
        // Innermost arrays must be sorted in ascending order
        private static readonly int[][][] PieceY = {
            new int[][] { new int[] {  0,  0, 0, 0 }, new int[] {  0,  0, 0, 0 }, new int[] {  0,  0, 0, 0 }, new int[] {  0,  0, 0, 0 } }, // Empty
            new int[][] { new int[] { -1,  0, 0, 0 }, new int[] { -1,  0, 0, 1 }, new int[] {  0,  0, 0, 1 }, new int[] { -1,  0, 0, 1 } }, // T
            new int[][] { new int[] {  0,  0, 0, 0 }, new int[] { -1,  0, 1, 2 }, new int[] {  1,  1, 1, 1 }, new int[] { -1,  0, 1, 2 } }, // I
            new int[][] { new int[] { -1,  0, 0, 0 }, new int[] { -1,  0, 1, 1 }, new int[] {  0,  0, 0, 1 }, new int[] { -1, -1, 0, 1 } }, // L
            new int[][] { new int[] { -1,  0, 0, 0 }, new int[] { -1, -1, 0, 1 }, new int[] {  0,  0, 0, 1 }, new int[] { -1,  0, 1, 1 } }, // J
            new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1,  0, 0, 1 }, new int[] {  0,  0, 1, 1 }, new int[] { -1,  0, 0, 1 } }, // S
            new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1,  0, 0, 1 }, new int[] {  0,  0, 1, 1 }, new int[] { -1,  0, 0, 1 } }, // Z
            new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 } }  // O
        };

        private static readonly int[] KicksX = { 0, -1, -1, 0, -1 }, IKicksX = { 0, -2, 1, -2, 1 };
        private static readonly int[] KicksY = { 0, 0, 1, -2, -2 }, IKicksY = { 0, 0, 0, -1, 2 };

        private static readonly Piece[] Pieces = GetPieces();

        public readonly int Id;
        public readonly int PieceType, R;
        public readonly int Highest, Lowest, Height;
        public readonly int MinX, MaxX, MinY, MaxY;
        private readonly PieceMask[][] _Masks;
        private readonly int[] _X, _Y;
        private readonly int[] _KicksCWX, _KicksCWY;
        //private readonly int[] _Kicks180X, _Kicks180Y;
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
            unsafe
            {
                MaxY = (int)Math.Ceiling(sizeof(MatrixMask) * 8D / 10D) - MinY;
            }
            Highest = -_Y.Min();
            Lowest = -_Y.Max();
            Height = Highest - Lowest + 1;

            GetKicksTable(true, out _KicksCWX, out _KicksCWY);
            GetKicksTable(false, out _KicksCCWX, out _KicksCCWY);

            _Masks = new PieceMask[MaxX - MinX + 1][];
            for (int i = 0; i < _Masks.Length; i++)
            {
                _Masks[i] = new PieceMask[MaxY - MinY + 1];
                for (int j = 0; j < _Masks[i].Length; j++)
                    _Masks[i][j] = GetMask(i + MinX, j + MinY);
            }
        }

        private static Piece[] GetPieces()
        {
            int piece_count = (PIECE_BITS | ROTATION_BITS) + 1;
            Piece[] pieces = new Piece[piece_count];
            for (int i = 0; i < piece_count; i++)
                pieces[i] = new Piece(i);

            return pieces;
        }

        // Helper function to convert from old format
        private void GetKicksTable(bool clockwise, out int[] kicksx, out int[] kicksy)
        {
            bool vertial = (R & ROTATION_CW) == ROTATION_CW;
            int xmul = (!clockwise ^ (R > ROTATION_CW) ^ (vertial && clockwise)) ? -1 : 1;
            int ymul = vertial ? -1 : 1;
            if (PieceType == I) ymul *= ((R > ROTATION_CW) ^ clockwise) ? -1 : 1;
            int[] testorder = PieceType == I && (vertial ^ !clockwise) ? new int[] { 0, 2, 1, 4, 3 } : new int[] { 0, 1, 2, 3, 4 };
            kicksx = new int[5];
            kicksy = new int[5];
            for (int i = 0; i < 5; i++)
            {
                kicksx[i] = (PieceType == I ? IKicksX[testorder[i]] : KicksX[testorder[i]]) * xmul;
                kicksy[i] = (PieceType == I ? IKicksY[testorder[i]] : KicksY[testorder[i]]) * ymul;
            }
        }

        // Helper function to convert from old format
        private PieceMask GetMask(int x, int y)
        {
            int start = new int[4].Select((_, i) => XYToPos(x + _X[i], y - _Y[i]))
                                  .Min();
            start &= ~31;

            // Center at (0, 0)
            ulong mask = 0;
            for (int i = 0; i < 4; i++)
                mask |= 1UL << (XYToPos(x + _X[i], y - _Y[i]) - start);

            return new PieceMask(mask, Math.Min(Math.Max(start / 32, 0), 7));
        }

        static int XYToPos(int x, int y) => (9 - x) + (10 * y);

        public PieceMask Masks(int x, int y)
        {
            if (y < MinY || y > MaxY) return new PieceMask();

            //x -= MinX;
            //if (x < 0 || x >= MaxX - MinX + 1) return new PieceMask();

            return _Masks[x - MinX][y - MinY];
        }
        public int X(int i) => _X[i];
        public int Y(int i) => _Y[i];
        public int KicksCWX(int i) => _KicksCWX[i];
        public int KicksCWY(int i) => _KicksCWY[i];
        //public int Kicks180X(int i) => _Kicks180X[i];
        //public int Kicks180Y(int i) => _Kicks180Y[i];
        public int KicksCCWX(int i) => _KicksCCWX[i];
        public int KicksCCWY(int i) => _KicksCCWY[i];

        public static implicit operator Piece(int i) => Pieces[i & (PIECE_BITS | ROTATION_BITS)];
        public static implicit operator int(Piece i) => i.Id;

        public override string ToString() =>
            new string[] { "", "CW ", "180 ", "CCW " }[R >> 3] +
            new string[] { "Empty", "T", "I", "L", "J", "S", "Z", "O" }[PieceType];
    }

    public class GameBase
    {
        public const int START_X = 4, START_Y = 20;

        // Try out array of heights as well
        public int Highest { get; private set; } = 0;
        public MatrixMask Matrix = new MatrixMask();
        internal int X { get; set; }
        internal int Y { get; set; }

        public Piece Current { get; set; } = Piece.EMPTY;
        public Piece Hold { get; set; } = Piece.EMPTY;
        public Piece[] Next { get; protected set; } = Array.Empty<Piece>();

        protected Random PieceRand = new Random();
        protected int BagIndex;
        protected Piece[] Bag = new Piece[] { Piece.T, Piece.I, Piece.L, Piece.J, Piece.S, Piece.Z, Piece.O };

        public GameBase(int next_length = 6, int? seed = null)
        {
            if (next_length < 1) throw new ArgumentOutOfRangeException(nameof(next_length));

            BagIndex = Bag.Length;
            Next = new Piece[next_length];
            PieceRand = new Random(seed ?? Guid.NewGuid().GetHashCode());

            for (int i = 0; i < Next.Length; i++) Next[i] = NextPiece();
        }

        protected Piece NextPiece()
        {
            if (BagIndex == Bag.Length)
            {
                // Re-shuffle bag
                // Each permutation has an equal chance of appearing
                for (int i = 0; i < Bag.Length; i++)
                {
                    int swapIndex = PieceRand.Next(Bag.Length - i) + i;
                    (Bag[i], Bag[swapIndex]) = (Bag[swapIndex], Bag[i]);
                }
                BagIndex = 0;
            }

            return Bag[BagIndex++];
        }

        public bool Fits(Piece piece, int x, int y)
        {
            if (x < piece.MinX || x > piece.MaxX || y < piece.MinY) return false;
            // if (Y + piece.Lowest > Highest) return true;

            return !Matrix.Intersects(piece.Masks(x, y));
        }

        public bool OnGround()
        {
            // if (Y + Current.Lowest > Highest) return false; // Already checked in most cases, wouldn't speed things up
            if (Y <= Current.MinY) return true;

            return Matrix.Intersects(Current.Masks(X, Y - 1));
        }

        public int TSpinType(bool rotatedLast)
        {
            // 19 = 10 + T.MaxX
            const int ALL_CORNERS = (0b101 << 20) | 0b101;
            const int NO_BR = (0b101 << 20) | 0b100;
            const int NO_BL = (0b101 << 20) | 0b001;
            const int NO_TL = (0b001 << 20) | 0b101;
            const int NO_TR = (0b100 << 20) | 0b101;

            if (!rotatedLast || (Current.PieceType != Piece.T)) return 0;

            // Allign the matrix so that the t-spin is at bottom left
            int pos = 10 * Y - X - 2;
            ulong corners = (Matrix >> pos).LowLow & ALL_CORNERS;

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
                    if (Matrix.Intersects(Current.Masks(X + 1, Y)))
                        return false;
                    X++;
                    return true;
                }
            }
            else if (X > Current.MinX)
            {
                if (Matrix.Intersects(Current.Masks(X - 1, Y)))
                    return false;
                X--;
                return true;
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
                X = Math.Min(Math.Max(X, Current.MinX), Current.MaxX);
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
#if DEBUG
        if (dy < 0)
            throw new ArgumentOutOfRangeException(nameof(dy), "dy must be non-negative.");
#endif
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
            Matrix |= Current.Masks(X, Y);

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
            Matrix &= ~Current.Masks(X, Y);

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

        public bool PathFind(Piece end_piece, int end_x, int end_y, out List<Moves> path_moves)
        {
            path_moves = new List<Moves>();
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
            Queue<(Piece, int, int, List<Moves>)> nodes = new Queue<(Piece, int, int, List<Moves>)>();
            // Set of seen nodes
            HashSet<int> seen = new HashSet<int>();
            // Breadth first search
            clone.ResetPiece();
            nodes.Enqueue((clone.Current, clone.X, clone.Y, new List<Moves>()));
            seen.Add((clone.Current, clone.X, clone.Y).GetHashCode());
            PieceMask end_piece_mask = end_piece.Masks(end_x, end_y);
            while (nodes.Count != 0)
            {
                (Piece piece, int x, int y, List<Moves> moves) = nodes.Dequeue();
                clone.Current = piece;
                clone.Y = y;
                // Try all different moves
                // Slide left/right
                for (int i = 0; i < 2; i++)
                {
                    clone.X = x;
                    bool right = i == 1;
                    if (clone.TrySlide(right))
                        UpdateWith(moves, right ? Moves.Right : Moves.Left);
                }
                // DAS left/right
                for (int i = 0; i < 2; i++)
                {
                    clone.X = x;
                    bool right = i == 1;
                    if (clone.TrySlide(right ? 10 : -10))
                        UpdateWith(moves, right ? Moves.DASRightAll : Moves.DASLeftAll);
                }
                // Rotate CW/CCW/180
                for (int i = 1; i <= 3; i++)
                {
                    clone.Current = piece;
                    clone.X = x;
                    clone.Y = y;
                    if (clone.TryRotate(i))
                        UpdateWith(moves, i == 1 ? Moves.RotateCW :
                                      i == 2 ? Moves.Rotate180 :
                                               Moves.RotateCCW);
                }
                // Hard/soft drop
                clone.Current = piece;
                clone.X = x;
                clone.Y = y;
                clone.TryDrop(40);
                if (piece.Masks(clone.X, clone.Y) == end_piece_mask)
                {
                    path_moves = moves;
                    path_moves.Add(Moves.HardDrop);
                    if (hold) path_moves.Insert(0, Moves.Hold);
                    return true;
                }
                UpdateWith(moves, Moves.SoftDrop);
            }

            return false;


            void UpdateWith(List<Moves> moves, Moves move)
            {
                int hash = clone.Current | (clone.X << 5) | (clone.Y << 9);
                if (!seen.Contains(hash))
                {
                    seen.Add(hash);
                    List<Moves> new_m = new List<Moves>(moves) { move };
                    nodes.Enqueue((clone.Current, clone.X, clone.Y, new_m));
                }
            }
        }
    }

    public sealed class Game : GameBase
    {
        public static GameSettings Settings { get; private set; } = GameSettings.LoadSettings();

        public static Game[] Games { get; private set; } = Array.Empty<Game>();
        public static readonly Stopwatch GlobalStopwatch = Stopwatch.StartNew();
        public double StartSeconds { get; private set; }
        public static double CurrentSeconds { get => GlobalStopwatch.Elapsed.TotalSeconds; }
        public static long CurrentMillis { get => GlobalStopwatch.ElapsedMilliseconds; }

        const string BLOCKSOLID = "██", BLOCKGHOST = "▒▒";

        public static int GameWidth { get; private set; } = 44;
        public static int GameHeight { get; private set; } = 24;

        private static readonly string[] ClearText = { "SINGLE", "DOUBLE", "TRIPLE", "TETRIS" };
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

        private static readonly ConsoleColor[] GarbageLineColor = new ConsoleColor[10].Select(x => PieceColors[Piece.Garbage]).ToArray();

        #region // Fields and Properties
        public readonly ConsoleColor[][] MatrixColors = new ConsoleColor[25][]; // [y][x]

        public int XOffset { get; private set; } = 0;
        public int YOffset { get; private set; } = 0;

        private string _Name = "";
        public string Name
        {
            get => _Name;
            set
            {
                _Name = value;
                DrawName(value);
            }
        }
        public bool IsBot { get; internal set; } = false;
        private bool _IsDead = true;
        public bool IsDead
        {
            get => _IsDead;
            private set
            {
                _IsDead = value;
                if (_IsDead)
                {
                    // Death animation

                    EraseClearStats();
                }
            }
        }

        private static bool _IsPaused = false;
        public static bool IsPaused
        {
            get => _IsPaused;
            set
            {
                _IsPaused = value;
                if (_IsPaused)
                {
                    GlobalStopwatch.Stop();
                }
                else
                {
                    GlobalStopwatch.Start();
                    foreach (Game g in Games)
                        Task.Delay(Settings.GarbageDelay).ContinueWith(t => g.DrawTrashMeter());
                }
            }
        }
        public static bool IsMuted { get; set; } = false;

        public int B2B { get; internal set; } = -1;
        public int Combo { get; internal set; } = -1;

        public double SoftG { get; set; } = Settings.SoftG;
        private double Vel = 0;
        private readonly Queue<Moves> MoveQueue = new Queue<Moves>();

        private double LastFrameTime;
        public int LockDelay { get; set; } = Settings.LockDelay;
        public int AutoLockGrace { get; set; } = Settings.AutoLockGrace;
        private int MoveCount = 0;
        private bool IsLastMoveRotate = false, AlreadyHeld = false;
        private long LastMoveMillis = -1;
        private readonly List<CancellationTokenSource> EraseCancelTokenSrcs = new List<CancellationTokenSource>();

        private readonly Random GarbageRand;
        private readonly List<(int Lines, long Time)> Garbage = new List<(int, long)>();

        private long LastTargetChangeTime;
        public TargetModes TargetMode { get; set; } = TargetModes.Random;
        private List<Game> Targets = new List<Game>();
        #endregion

        #region // Stats
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
                WriteAt(25, 0, ConsoleColor.White, _Lines.ToString().PadRight(GameWidth - 25));
                WriteAt(1, 11, ConsoleColor.White, Level.ToString());
            }
        }
        public int GarbageCleared { get; private set; } = 0;
        public int Sent { get; private set; } = 0;
        public int Level { get => Lines / 10 + 1; }
        public int PiecesPlaced { get; private set; } = 0;
        public int KeysPressed { get; private set; } = 0;
        // Attack per line
        public double APL
        {
            get => (Sent == 0) ? 0 : (double)Sent / Lines;
        }
        // Attack per piece
        public double APP
        {
            get => (Sent == 0) ? 0 : (double)Sent / PiecesPlaced;
        }
        // Attack per minute
        public double APM
        {
            get => (Sent == 0) ? 0 : 60D * Sent / (CurrentSeconds - StartSeconds);
        }
        // Pieces per second
        public double PPS
        {
            get => (PiecesPlaced == 0) ? 0 : (double)PiecesPlaced / (CurrentSeconds - StartSeconds);
        }
        // Keys per piece
        public double KPP
        {
            get => (PiecesPlaced == 0) ? 0 : (double)KeysPressed / PiecesPlaced;
        }
        // VS score
        public double VS
        {
            get => (CurrentSeconds - StartSeconds == 0) ? 0 : 100D * (Sent + GarbageCleared) / (CurrentSeconds - StartSeconds);
        }
        #endregion

        public Game(int next_length = 6, int? seed = null) : base(next_length, seed ?? Guid.NewGuid().GetHashCode())
        {
            seed = seed ?? Guid.NewGuid().GetHashCode();
            for (int i = 0; i < MatrixColors.Length; i++) MatrixColors[i] = new ConsoleColor[10];
            GarbageRand = new Random(seed.GetHashCode());
        }

        public static void InitWindow(int size = 16)
        {
            Sound.SFXVolume = Settings.SFXVolume;
            Sound.BGMVolume = Settings.BGMVolume;
            // Set up console
            Console.Title = "Budget Tetris";
            FConsole.Framerate = 30;
            FConsole.CursorVisible = false;
            FConsole.SetFont("Consolas", (short)size);
            FConsole.Initialise(() =>
            {
                if (IsPaused || Games == null) return;
                foreach (Game g in Games)
                {
                    g.Tick();
                }
            });

            GameSettings.SaveSettings(Settings);

            FConsole.AddOnPressListener(Key.R, () =>
            {
                IsPaused = true;
                int seed = Guid.NewGuid().GetHashCode();
                foreach (Game game in Games)
                {
                    game.PieceRand = new Random(seed);
                    game.Restart();
                    game.ClearScreen();
                    game.DrawAll();
                }
                IsPaused = false;
            });
            FConsole.AddOnPressListener(Key.Escape, () => IsPaused = !IsPaused);
            FConsole.AddOnPressListener(Key.M, () => Sound.IsMuted = !Sound.IsMuted);
        }

        public static void SetGames(Game[] games)
        {
            Games = games;
            GlobalStopwatch.Restart();
            GameHeight = games.Any(x => x.IsBot) ? 27 : 24;
            double game_aspect = (GameWidth * 0.5D) / GameHeight;
            double target_aspect = 16D / 9D;
            // Find width and height (appox. 16:9 aspect ratio) in terms of GameWidth and GameHeight
            double width_double = Math.Sqrt(games.Length * target_aspect / game_aspect);
            double height_double = games.Length / width_double;
            int width = (int)Math.Round(width_double);
            int height = (int)Math.Round(height_double);
            if (width * height < Games.Length)
            {
                if (width_double - width > height_double - height)
                    width++;
                else
                    height++;
            }

            FConsole.Clear();
            FConsole.Set(width * (GameWidth + 1) + 1, height * GameHeight + 1);

            // Set up and re-draw games
            for (int i = 0; i < Games.Length; i++)
            {
                Games[i].XOffset = (i % width) * (GameWidth + 1) + 1;
                Games[i].YOffset = (i / width) * GameHeight + 1;
                Games[i].Restart();
                Games[i].ClearScreen();
                Games[i].DrawAll();
            }
        }

        public void SetupPlayerInput()
        {
            FConsole.AddOnPressListener(Key.Left, () => Play(Moves.Left));
            FConsole.AddOnPressListener(Key.Right, () => Play(Moves.Right));
            if (Settings.DASDelay >= 0)
            {
                int das_l_priority = 0, das_r_priority = 0;
                if (Settings.DASInterval <= 16)
                {
                    FConsole.AddOnHoldListener(Key.Left, () =>
                    {
                        if (das_r_priority < 2)
                        {
                            Play(Moves.DASLeftAll);
                            das_l_priority = das_r_priority + 1;
                        }
                        else
                            das_l_priority = 1;
                    }, Settings.DASDelay, 16);
                    FConsole.AddOnHoldListener(Key.Right, () =>
                    {
                        if (das_l_priority < 2)
                        {
                            Play(Moves.DASRightAll);
                            das_r_priority = das_l_priority + 1;
                        }
                        else
                            das_r_priority = 1;
                    }, Settings.DASDelay, 16);
                }
                else
                {
                    FConsole.AddOnHoldListener(Key.Left, () =>
                    {
                        if (das_r_priority < 2)
                        {
                            Play(Moves.DASLeft);
                            das_l_priority = das_r_priority + 1;
                        }
                        else
                            das_l_priority = 1;
                    }, Settings.DASDelay, Settings.DASInterval);
                    FConsole.AddOnHoldListener(Key.Right, () =>
                    {
                        if (das_l_priority < 2)
                        {
                            Play(Moves.DASRight);
                            das_r_priority = das_l_priority + 1;
                        }
                        else
                            das_r_priority = 1;
                    }, Settings.DASDelay, Settings.DASInterval);
                }
                FConsole.AddOnReleaseListener(Key.Left, () => das_l_priority = 0);
                FConsole.AddOnReleaseListener(Key.Right, () => das_r_priority = 0);
            }

            FConsole.AddOnPressListener(Key.Up, () => Play(Moves.RotateCW));
            FConsole.AddOnPressListener(Key.X, () => Play(Moves.RotateCW));
            FConsole.AddOnPressListener(Key.Z, () => Play(Moves.RotateCCW));
            FConsole.AddOnPressListener(Key.A, () => Play(Moves.Rotate180));

            FConsole.AddOnHoldListener(Key.Down, () => Play(Moves.SoftDrop), 0, 16);
            FConsole.AddOnPressListener(Key.Space, () => Play(Moves.HardDrop));

            FConsole.AddOnPressListener(Key.C, () => Play(Moves.Hold));
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
            GarbageCleared = 0;
            Sent = 0;
            KeysPressed = 0;
            PiecesPlaced = 0;

            Vel = 0;
            IsLastMoveRotate = false;
            AlreadyHeld = false;
            MoveCount = 0;
            MoveQueue.Clear();

            StartSeconds = CurrentSeconds;
            LastFrameTime = StartSeconds;
            LastTargetChangeTime = CurrentMillis;
            LastMoveMillis = -1;
            Garbage.Clear();
            //TargetMode = TargetModes.Random;
            Targets.Clear();

            SpawnNextPiece();
        }

        public void Tick()
        {
            if (IsDead || IsPaused) return;

            // Timekeeping
            double deltaT = CurrentSeconds - LastFrameTime;
            LastFrameTime = CurrentSeconds;

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
                        Slide(-1, false);
                        break;
                    case Moves.Right:
                        Slide(1, false);
                        break;
                    case Moves.DASLeft:
                        Slide(-1, true);
                        break;
                    case Moves.DASRight:
                        Slide(1, true);
                        break;
                    case Moves.DASLeftAll:
                        Slide(-10, true);
                        break;
                    case Moves.DASRightAll:
                        Slide(10, true);
                        break;
                    case Moves.SoftDrop:
                        softDrop = true;
                        int dropped = Drop((int)SoftG, 1);
                        Vel += SoftG - dropped;
                        break;
                    case Moves.HardDrop:
                        Score += 2 * TryDrop(Y - Current.MinY);
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
            Vel += Settings.G * deltaT * FConsole.Framerate;
            if (OnGround())
            {
                Vel = 0;
                // Take note of the time it touched the ground
                if (LastMoveMillis < 0)
                    LastMoveMillis = CurrentMillis;
                // Lock piece
                if ((MoveCount > AutoLockGrace) || (CurrentMillis - LastMoveMillis > LockDelay))
                    PlacePiece();
            }
            else
            {
                if (MoveCount < AutoLockGrace) LastMoveMillis = -1;
                if (Vel >= 1)
                    Vel -= Drop((int)Vel, softDrop ? 1 : 0); // Round Vel down
            }

            DrawAll();

            // Write stats
            WriteAt(0, 20, ConsoleColor.White, $"PPS: {Math.Round(PPS, 3)}".PadRight(11));
            WriteAt(0, 21, ConsoleColor.White, $"APP: {Math.Round(APP, 3)}".PadRight(11));
            WriteAt(0, 22, ConsoleColor.White, $"VS: {Math.Round(VS, 2)}".PadRight(11));
        }

        #region // Player methods
        public void Play(Moves move)
        {
            if (IsDead || IsPaused) return;

            MoveQueue.Enqueue(move);
            if (move != Moves.None &&
                move != Moves.DASLeft &&
                move != Moves.DASRight)
                KeysPressed++;
        }

        public void Slide(int dx, bool das)
        {
            DrawCurrent(true);
            if (TrySlide(dx))
            {
                if (!IsMuted)
                    Sound.Slide.Play();
                IsLastMoveRotate = false;
                if (MoveCount < AutoLockGrace) LastMoveMillis = OnGround() ? CurrentMillis : -1;
                if (!das) MoveCount++;
            }
            DrawCurrent(false);
        }

        public int Drop(int dy, int scorePerDrop)
        {
            DrawCurrent(true);
            int moved = TryDrop(dy);
            Score += moved * scorePerDrop;
            if (moved > 0)
            {
                //Sound.SoftDrop.Play();
                IsLastMoveRotate = false;
            }
            DrawCurrent(false);

            return moved;
        }

        public void Rotate(int dr)
        {
            if (Current.PieceType == Piece.O) return;

            DrawCurrent(true);
            if (TryRotate(dr))
            {
                if (!IsMuted)
                    Sound.Rotate.Play();
                IsLastMoveRotate = true;
                if (MoveCount++ < AutoLockGrace) LastMoveMillis = OnGround() ? CurrentMillis : -1;
            }
            DrawCurrent(false);
        }

        public void HoldPiece()
        {
            if (AlreadyHeld) return;

            if (!IsMuted) Sound.Hold.Play();

            // Undraw
            DrawCurrent(true);
            DrawPieceAt(Hold, 3, 3, true);

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
            DrawPieceAt(Hold, 3, 3, false);
            DrawCurrent(false);
            AlreadyHeld = true;
            IsLastMoveRotate = false;
        }

        void PlacePiece()
        {
            int tspin = TSpinType(IsLastMoveRotate); //0 = no spin, 2 = mini, 3 = t-spin
                                                     // Place piece in MatrixColors
            for (int i = 0; i < 4; i++)
                if (Y - Current.Y(i) < MatrixColors.Length)
                    MatrixColors[Y - Current.Y(i)][X + Current.X(i)] = PieceColors[Current.PieceType];
            // Clear lines
            int[] clears = Place(out int cleared);
            for (int i = 0; i < clears.Length; i += 2)
            {
                if (clears[i + 1] == 0) break;
                for (int j = clears[i]; j < MatrixColors.Length - clears[i + 1]; j++)
                {
                    if (MatrixColors[j].Any(x => x == PieceColors[Piece.Garbage]))
                        GarbageCleared++;
                    MatrixColors[j] = MatrixColors[j + clears[i + 1]];
                }
            }
            // Add new empty rows
            for (int i = MatrixColors.Length - cleared; i < MatrixColors.Length; i++)
                MatrixColors[i] = new ConsoleColor[10].Select(x => PieceColors[Piece.EMPTY]).ToArray();
            // Line clears
            int score_add = 0;
            score_add += new int[] { 0, 100, 300, 500, 800 }[cleared];
            // T-spins
            if (tspin == 3) score_add += new int[] { 400, 700, 900, 1100 }[cleared];
            if (tspin == 2) score_add += 100;
            // Perfect clear
            bool pc = cleared > 0 && Matrix.GetRow(0) == 0;
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
            // Lines
            Lines += cleared;

            // Write stats to console
            // Write clear stats
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
                Task.Delay(Settings.EraseDelay).ContinueWith(t =>
                {
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
            // Play sound
            if (pc) Sound.PC.Play();
            else if (tspin > 0) Sound.TSpin.Play();
            else if (cleared > 0) Sound.ClearSounds[cleared - 1].Play();

            // Trash sent
            int trash = pc ? Settings.PCTrash[cleared] :
                        tspin == 3 ? Settings.TSpinTrash[cleared] :
                                     Settings.LinesTrash[cleared];
            if (Combo > 0) trash += Settings.ComboTrash[Math.Min(Combo, Settings.ComboTrash.Length) - 1];
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
                int garbage_dumped = 0;
                while (Garbage.Count > 0)
                {
                    if (CurrentMillis - Garbage[0].Time <= Settings.GarbageDelay) break;

                    int lines_to_add = Garbage[0].Lines;
                    garbage_dumped += lines_to_add;
                    Garbage.RemoveAt(0);
                    int hole = GarbageRand.Next(10);
                    int bedrock_height = 0;
                    while (Matrix.GetRow(bedrock_height) == MatrixMask.FULL_LINE) bedrock_height++;
                    // Move stuff up
                    MatrixMask top = (Matrix & MatrixMask.InverseHeightMasks[bedrock_height]) << (lines_to_add * 10);
                    Matrix = top | MatrixMask.HeightMasks[bedrock_height];
                    for (int y = MatrixColors.Length - lines_to_add - 1; y > bedrock_height - 1; y--)
                        MatrixColors[y + lines_to_add] = MatrixColors[y];
                    // Add garbage
                    for (int y = bedrock_height; y < Math.Min(MatrixColors.Length, bedrock_height + lines_to_add); y++)
                    {
                        MatrixMask garbage_line = new MatrixMask(LowLow: ~(1UL << (9 - hole))) & MatrixMask.HeightMasks[1];
                        garbage_line <<= y * 10;
                        Matrix |= garbage_line;

                        MatrixColors[y] = new ConsoleColor[10];
                        GarbageLineColor.CopyTo(MatrixColors[y], 0);
                        // Add hole
                        MatrixColors[y][hole] = PieceColors[Piece.EMPTY];
                    }
                }

                if (garbage_dumped > 0)
                    (garbage_dumped >= 10 ? Sound.GarbageLarge : Sound.GarbageSmall).Play();
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
            long time = CurrentMillis;
            // Select targets
            switch (TargetMode)
            {
                case TargetModes.Random:
                    if (time - LastTargetChangeTime > Settings.TargetChangeInteval)
                    {
                        LastTargetChangeTime = time;
                        Targets.Clear();
                        List<Game> aliveGames = Games.Where(g => !g.IsDead).ToList();
                        if (aliveGames.Count <= 1) break;
                        int i = new Random().Next(aliveGames.Count - 1);
                        if (i >= aliveGames.IndexOf(this)) i = (i + 1) % aliveGames.Count;
                        Targets.Add(aliveGames[i]);
                    }
                    break;
                case TargetModes.All:
                    Targets = Games.Where(g => !g.IsDead).ToList();
                    break;
                case TargetModes.AllButSelf:
                    Targets = Games.Where(g => !g.IsDead && g != this).ToList();
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
                Task.Delay(Settings.GarbageDelay).ContinueWith(t => victim.DrawTrashMeter());
            }
        }

        void SpawnNextPiece()
        {
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
            LastMoveMillis = -1;
            // Check for block out
            if (!Fits(Current, X, Y)) IsDead = true;
            // Check for lock out
            //if (Y + Current.Lowest >= 20) IsDead = true;
            // Draw next
            DrawNext();
            // Draw current
            DrawCurrent(false);
        }
        #endregion

        #region // Drawing methods
        public void WriteAt(int x, int y, ConsoleColor color, string text) =>
            FConsole.WriteAt(text, x + XOffset, y + YOffset, foreground: color);

        void DrawName(string name)
        {
            // Center text
            int space = GameWidth - name.Length;
            int left_space = space / 2;
            if (space < 0)
                WriteAt(0, -1, ConsoleColor.White, name.Substring(-left_space, GameWidth));
            else
                WriteAt(0, -1, ConsoleColor.White, name.PadLeft(left_space + name.Length).PadRight(GameWidth));
        }

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

        void DrawNext()
        {
            if (Settings.LookAheads <= 0) return;

            // Outline
            WriteAt(34, 1, ConsoleColor.White, "╔══NEXT══╗");
            int i = 2;
            for (; i < Math.Max(2 + 3, 1 + Math.Min(7, Next.Length) * 3); i++)
                WriteAt(34, i, ConsoleColor.White, "║        ║");
            WriteAt(34, i, ConsoleColor.White, "╚════════╝");
            // Pieces
            i = 0;
            for (; i < Math.Min(7, Next.Length); i++)
                DrawPieceAt(Next[i], 37, 3 + 3 * i, false);
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
                    ConsoleColor color = CurrentMillis - Garbage[i].Time > Settings.GarbageDelay ? ConsoleColor.Red : ConsoleColor.Gray;
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
            for (int i = 14; i < 19; i++)
                WriteAt(0, i, ConsoleColor.Black, "".PadLeft(11));
        }

        void ClearScreen()
        {
            // Clear console section
            for (int i = 0; i < GameHeight; i++)
                WriteAt(0, i, ConsoleColor.White, "".PadLeft(GameWidth));
        }

        public void DrawAll()
        {
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
            DrawNext();
            // Draw hold
            DrawPieceAt(Hold, 3, 3, false);
            // Draw board
            DrawMatrix();
            // Draw current piece
            DrawCurrent(false);
            // Draw trash meter
            DrawTrashMeter();
            // Write name
            DrawName(Name);
        }
        #endregion
    }
}
