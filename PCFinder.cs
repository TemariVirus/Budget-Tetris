using System.Diagnostics;

namespace Tetris;

class PCFinder
{
    private sealed class DoublyLinkedMatrix
    {
        public int Width, Height;
        public readonly DoublyLinkedMatrixNode[] Headers;
        public readonly List<NodeData> RowData;
        public DoublyLinkedMatrixNode StartHeader;

        private DoublyLinkedMatrix(int width)
        {
            Width = width;
            Height = 0;
            RowData = new List<NodeData>();

            Headers = new DoublyLinkedMatrixNode[width];
            Headers[0] = new DoublyLinkedMatrixNode(this, 0);
            for (int i = 1; i < width; i++)
            {
                Headers[i] = new DoublyLinkedMatrixNode(this, i);
                Headers[i - 1].Right = Headers[i];
                Headers[i].Left = Headers[i - 1];
            }
            Headers[^1].Right = Headers[0];
            Headers[0].Left = Headers[^1];
            StartHeader = Headers[0];
        }

        public static bool TryGetMatrix(MatrixMask holes, List<Piece> pieces, out DoublyLinkedMatrix matrix)
        {
            // Format: holes that it fills, then piece used, then 1st 2 piece intact & on ground, then whether or not this piece is not used (extra)
            int pieces_used = holes.PopCount() / 4;
            bool extra = pieces_used < pieces.Count;
            if (extra) pieces_used++;
            matrix = new DoublyLinkedMatrix(holes.PopCount() + pieces_used + 1 + (extra ? 1 : 0));
            DoublyLinkedMatrixNode[] lasts = new DoublyLinkedMatrixNode[matrix.Width];

            if (pieces_used > pieces.Count) return false;

            // Get columns
            int height;
            uint[] matrix_rows = holes.GetRows();
            Dictionary<int, int> columns = new Dictionary<int, int>();
            for (height = 0; height < matrix_rows.Length; height++)
            {
                if (matrix_rows[height] == 0) break;

                for (int x = 0; x < 10; x++)
                    if ((matrix_rows[height] >> (9 - x) & 1) == 1)
                        columns.Add(height * 10 + x, columns.Count);
            }

            // Add rows of each piece
            MatrixMask matrix_mask = ~holes;
            for (int i = 0; i < pieces_used; i++)
            {
                // Loop through each rotation
                for (int r = 0; r < 4; r++)
                {
                    Piece piece = pieces[i].PieceType | (r * Piece.ROTATION_CW);
                    int[][] ori_rows = new int[piece.Height][].Select((x, i) =>
                    {
                        List<int> row = new List<int>(4);

                        // Skip to current y
                        int j = 3;
                        for (; j > 0 && i > 0; j--)
                            if (piece.Y(j) != piece.Y(j - 1))
                                i--;

                        // Add all x coords for that y level
                        int y = piece.Y(j);
                        for (; j >= 0 && piece.Y(j) == y; j--)
                            row.Add(piece.X(j));

                        row.Sort();
                        return row.ToArray();
                    }).ToArray();

                    // Loop through all x
                    for (int x = piece.MinX; x <= piece.MaxX; x++)
                    {
                        int[][] rows = ori_rows.Select((arr) => arr.Select((val) => val + x).ToArray()).ToArray();
                        int[] ys = new int[piece.Height].Select((x, y) => y).ToArray();
                        // Loop through all permutation of rows (bc line clears)
                        do
                        {
                            // Check if piece fits
                            bool fits = true;
                            for (int j = 0; j < ys.Length && fits; j++)
                            {
                                for (int k = 0; k < rows[j].Length && fits; k++)
                                {
                                    int pos = ys[j] * 10 + rows[j][k];
                                    fits = columns.ContainsKey(pos);
                                }
                            }

                            // Add row to matrix
                            if (fits)
                            {
                                // For 1st 2 pieces, check if intact and on ground
                                bool restricted = i < 2 && IntactAndOnGround(piece, x, ys);
                                // Add data associated with row
                                matrix.RowData.Add(new NodeData(piece, i, x, ys));

                                AddRow(matrix, i, rows, ys, restricted);
                            }
                        } while (NextLexicographicPermutation(ys));
                    }

                    // O pieces can't be rotated
                    if (piece.PieceType == Piece.O) break;
                    // I, S and Z pieces are the same (ignoring location) when rotated 180
                    if ((piece.PieceType == Piece.I ||
                         piece.PieceType == Piece.S ||
                         piece.PieceType == Piece.Z)
                         && r >= 1) break;
                }

                // Add extra row for if it is unused
                if (extra)
                {
                    DoublyLinkedMatrixNode piece_node = new DoublyLinkedMatrixNode(matrix, columns.Count + i);
                    ConnectVertically(matrix, piece_node);
                    DoublyLinkedMatrixNode unused_node = new DoublyLinkedMatrixNode(matrix, matrix.Width - 1);
                    ConnectVertically(matrix, unused_node);
                    piece_node.Left = unused_node;
                    piece_node.Right = unused_node;
                    unused_node.Left = piece_node;
                    unused_node.Right = piece_node;

                    matrix.Height++;
                }
            }

            // Connect column ends to make rings
            for (int i = 0; i < matrix.Width; i++)
            {
                // Unsolvable if a column(s) has no 1s
                if (matrix.Headers[i].Down == null)
                    return false;

                DoublyLinkedMatrixNode first = matrix.Headers[i].Down;
                first.Up = lasts[i];
                lasts[i].Down = first;
            }

            return true;


            bool NextLexicographicPermutation(in int[] pos)
            {
                // Find first bit that has a 0 in front
                int i = 0;
                for (; i < pos.Length - 1; i++)
                    if (pos[i] + 1 != pos[i + 1])
                        break;

                // Move bit in front and move all bits behind back to the start
                pos[i]++;
                for (int j = 0; j < i; j++)
                    pos[j] = j;

                return pos[i] < height;
            }

            void ConnectVertically(DoublyLinkedMatrix _matrix, DoublyLinkedMatrixNode node)
            {
                // Add to header if column is empty
                if (lasts[node.HeaderIndex] == null)
                {
                    node.Header.Down = node;
                }
                else
                {
                    lasts[node.HeaderIndex].Down = node;
                    node.Up = lasts[node.HeaderIndex];
                }

                lasts[node.HeaderIndex] = node;
                node.Header.OneCount++;
            }

            void AddRow(DoublyLinkedMatrix _matrix, int piece_index, int[][] rows, int[] ys, bool restricted)
            {
                DoublyLinkedMatrixNode previous = null;
                for (int i = 0; i < 2; i++)
                {
                    DoublyLinkedMatrixNode first = null;
                    // Connect nodes for coords
                    for (int j = 0; j < ys.Length; j++)
                    {
                        foreach (int x in rows[j])
                        {
                            int pos = ys[j] * 10 + x;
                            int index = columns[pos];
                            DoublyLinkedMatrixNode current = new DoublyLinkedMatrixNode(_matrix, index, _matrix.RowData.Count - 1);

                            //if (previous == null)
                            //{
                            //    ConnectVertically(_matrix, current);
                            //    first = current;
                            //    previous = current;
                            //}
                            //else
                            //    Connect(current);
                            if (first == null)
                            {
                                first = current;
                                previous = current;
                            }
                            Connect(current);
                        }
                    }
                    // Connect node for piece id
                    DoublyLinkedMatrixNode piece_node = new DoublyLinkedMatrixNode(_matrix, columns.Count + piece_index, _matrix.RowData.Count - 1);
                    Connect(piece_node);
                    // Connect nodes for placement restriction
                    if (i == 0 && restricted)
                        Connect(new DoublyLinkedMatrixNode(_matrix, columns.Count + pieces_used, _matrix.RowData.Count - 1));
                    // Connect the ends to make it a ring
                    first.Left = previous;
                    previous.Right = first;
                    _matrix.Height++;

                    if (!restricted || piece_index == 0) break;
                }


                void Connect(DoublyLinkedMatrixNode node)
                {
                    ConnectVertically(_matrix, node);
                    node.Left = previous;
                    previous.Right = node;
                    previous = node;
                }
            }

            bool IntactAndOnGround(Piece piece, int x, int[] ys)
            {
                // Chcek if intact
                for (int i = 1; i < ys.Length; i++)
                    if (ys[i - 1] != ys[i] - 1)
                        return false;

                // Check if on ground
                if (ys[0] != 0)
                    if (!matrix_mask.Intersects(piece.GetMask(x, ys[0] + piece.MinY - 1)))
                        return false;

                return true;
            }
        }

        public DoublyLinkedMatrixNode FindMinColumn()
        {
            DoublyLinkedMatrixNode min_header = StartHeader;
            for (DoublyLinkedMatrixNode current = StartHeader.Right; current != StartHeader; current = current.Right)
            {
                if (current.OneCount < min_header.OneCount)
                {
                    min_header = current;
                    if (current.OneCount == 0) break;
                }
            }

            return min_header;
        }
    }

    private sealed class DoublyLinkedMatrixNode : IEquatable<DoublyLinkedMatrixNode>
    {
        public static int IdCounter = 0;
        public readonly int Id = IdCounter++;

        public readonly DoublyLinkedMatrix Matrix;
        public readonly int HeaderIndex;
        public readonly DoublyLinkedMatrixNode Header;
        public int OneCount;

        public readonly int DataIndex;
        public readonly NodeData Data;

        public DoublyLinkedMatrixNode Up;
        public DoublyLinkedMatrixNode Down;
        public DoublyLinkedMatrixNode Right;
        public DoublyLinkedMatrixNode Left;

        public DoublyLinkedMatrixNode(DoublyLinkedMatrix matrix, int header_index, int data_index = -1)
        {
            Matrix = matrix;
            HeaderIndex = header_index;
            Header = matrix.Headers[header_index];
            OneCount = 0;
            DataIndex = data_index;
            if (data_index != -1) Data = matrix.RowData[data_index];
        }

        public void AddRow()
        {
            Up.Down = this;
            Down.Up = this;
            Header.OneCount++;
            for (DoublyLinkedMatrixNode current = Left; current != this; current = current.Left)
            {
                current.Up.Down = current;
                current.Down.Up = current;
                current.Header.OneCount++;
            }
            Matrix.Height++;
        }

        public void RemoveRow()
        {
            Up.Down = Down;
            Down.Up = Up;
            if (Header.Down == this) Header.Down = Down;
            Header.OneCount--;
            for (DoublyLinkedMatrixNode current = Left; current != this; current = current.Left)
            {
                current.Up.Down = current.Down;
                current.Down.Up = current.Up;
                if (current.Header.Down == current) current.Header.Down = current.Down;
                current.Header.OneCount--;
            }
            Matrix.Height--;
        }

        public void AddColumn()
        {
            Header.Left.Right = Header;
            Header.Right.Left = Header;
            Matrix.Width++;
        }

        public void RemoveColumn()
        {
            Header.Left.Right = Header.Right;
            Header.Right.Left = Header.Left;
            Matrix.Width--;
            if (Header == Matrix.StartHeader)
                Matrix.StartHeader = Header.Left;
        }

        public override int GetHashCode() => Id;

        public bool Equals(DoublyLinkedMatrixNode node) => Id == node.Id;

        public override bool Equals(object obj) =>
            obj is DoublyLinkedMatrixNode node && Id == node.Id;
    }

    private sealed class NodeData : IEquatable<NodeData>
    {
        public static int IdCounter = 0;
        public readonly int Id = IdCounter++;

        public readonly Piece @Piece;
        public readonly int PieceIndex;
        public readonly int X;
        public readonly int[] Ys;

        public NodeData(Piece piece, int piece_index, int x, int[] ys)
        {
            Piece = piece;
            PieceIndex = piece_index;
            X = x;
            Ys = (int[])ys.Clone();
        }

        public override int GetHashCode() => Id;

        public bool Equals(NodeData data) => Id == data.Id;

        public override bool Equals(object obj) =>
            obj is NodeData data && Id == data.Id;
    }


    public bool ShowMode = false, Wait = false, GoNext = false;
    long NodeCount, PCCount;
    GameBase PathFind;
    Stopwatch sw = new Stopwatch();

    public bool TryFindPC(GameBase game, out List<(Piece piece, int x, int y)> placements)
    {
        Game.Games[0].DrawAll();
        NodeCount = 0;
        PCCount = 0;
        DoublyLinkedMatrixNode.IdCounter = 0;
        NodeData.IdCounter = 0;
        PathFind = game.Clone();
        placements = new List<(Piece piece, int x, int y)>();
        sw.Restart();
        new Thread(() =>
        {
            while (true)
            {
                Game.Games[0].WriteAt(0, 25, ConsoleColor.White, "Nodes/s: " + NodeCount / sw.Elapsed.TotalSeconds);
                Thread.Sleep(1000);
            }
        }).Start();

        // Order of pieces: Current, Hold (if exists), Next (in order)
        List<Piece> pieces = new List<Piece>() { game.Current };
        if (game.Hold != Piece.EMPTY) pieces.Add(game.Hold);
        pieces.AddRange(game.Next);
        MatrixMask holes = ~game.Matrix & MatrixMask.HeightMasks[game.Highest + 1];
        // Can't pc if parity is odd
        if (holes.PopCount() % 2 != 0) return false;
        // Need an extra line if parity is not multiple of 4
        if (holes.PopCount() % 4 == 2) holes = AddLines(holes, 1);
        // Add 2 extra lines if matrix is empty
        if (holes.PopCount() == 0) holes = AddLines(holes, 2);

        // Repeatedly try to find higher and higher PCs
        for (DoublyLinkedMatrix matrix; holes.PopCount() / 4 <= pieces.Count; holes = AddLines(holes, 2))
        {
            if (!DoublyLinkedMatrix.TryGetMatrix(holes, pieces, out matrix)) continue;
            if (SolverHead(matrix, out DoublyLinkedMatrixNode[] sol)) return true;
        }

        return false;
    }

    private static MatrixMask AddLines(MatrixMask matrix, int lines)
    {
        int highest = 0;
        while (matrix.GetRow(highest) != 0)
        {
            highest++;
            if (highest >= 24) break;
        }

        return matrix | (MatrixMask.InverseHeightMasks[highest] & MatrixMask.HeightMasks[highest + lines]);
    }

    private bool SolverHead(DoublyLinkedMatrix matrix, out DoublyLinkedMatrixNode[] rows)
    {
        while (Console.KeyAvailable) Console.ReadKey(true);
        Stack<DoublyLinkedMatrixNode> solution = new Stack<DoublyLinkedMatrixNode>();
        bool found = Solve(matrix);
        rows = solution.ToArray();
        return found;


        // Knuth's algorithm X
        bool Solve(DoublyLinkedMatrix matrix)
        {
            if (matrix.Width == 0)
            {
                // Check if pieces can be placed as in solution
                return CheckPlacement(GetPlacementsData(solution));
            }
            if (matrix.Height == 0) return false;

            // Select the column with the least number of 1s
            DoublyLinkedMatrixNode header = matrix.FindMinColumn();

            // Cannot be exactly covered if column has no 1s
            if (header.OneCount == 0) return false;

            // Keep track of removed rows (to be added back in reverse order)
            Stack<(DoublyLinkedMatrixNode Node, bool IsRow)> removed = new Stack<(DoublyLinkedMatrixNode, bool)>();
            DoublyLinkedMatrixNode current_row = header.Down;
            for (int i = 0; i < header.OneCount; i++, current_row = current_row.Down)
            {
                NodeCount++;
                // Add this row to partial solution
                solution.Push(current_row);
                if (ShowMode && current_row.DataIndex != -1)
                {
                    DrawPiece(current_row.Data, false);
                    if (Wait) WaitNext();
                }

                // For each column where this row has a 1,
                DoublyLinkedMatrixNode col = current_row;
                do
                {
                    // Remove all the other rows with a 1 in this column
                    for (DoublyLinkedMatrixNode row = col.Up; row != col; row = row.Up)
                    {
                        row.RemoveRow();
                        removed.Push((row, true));
                    }
                    // And remove this column
                    col.RemoveColumn();
                    removed.Push((col, false));

                    col = col.Right;
                } while (col != current_row);
                // Then remove this row
                current_row.RemoveRow();
                removed.Push((current_row, true));

                // Recurse
                if (Solve(matrix))
                    return true;

                // Add back rows and columns
                while (removed.Count > 0)
                {
                    var (node, is_row) = removed.Pop();
                    if (is_row) node.AddRow();
                    else node.AddColumn();
                }
                // Remove row from partial solution
                solution.Pop();
                if (ShowMode && current_row.DataIndex != -1)
                    DrawPiece(current_row.Data, true);
            }

            return false;
        }
    }

    private bool CheckPlacement(HashSet<NodeData> solution)
    {
        PCCount++;
        if (!ShowMode)
            foreach (var placement in solution)
                DrawPiece(placement, false);
        if (Wait)
        {
            Game.Games[0].WriteAt(0, 25, ConsoleColor.White, "PC found");
            WaitNext();
            Game.Games[0].WriteAt(0, 25, ConsoleColor.White, "        ");
        }

        int next_index = -1;
        Piece old_current = PathFind.Current, old_hold = PathFind.Hold;
        bool can_pathfind = CanPathFind();
        PathFind.Current = old_current;
        PathFind.Hold = old_hold;

        return can_pathfind;


        bool CanPathFind()
        {
            if (solution.Count == 0) return true;
            //if (next_index >= PathFind.Next.Length) return false;

            next_index++;
            NodeData[] nodes_current = solution.Where(x => x.Piece.PieceType == PathFind.Current.PieceType).ToArray();
            if (CheckNodes(nodes_current)) return true;

            bool hold_empty = PathFind.Hold == Piece.EMPTY;
            Piece hold = hold_empty ? PathFind.Next[next_index++] : PathFind.Hold;
            NodeData[] nodes_hold = solution.Where(x => x.Piece.PieceType == hold.PieceType).ToArray();
            // Update hold
            PathFind.Hold = PathFind.Current;
            if (CheckNodes(nodes_hold)) return true;
            // Un-update Hold
            if (hold_empty)
            {
                next_index--;
                PathFind.Hold = Piece.EMPTY;
            }
            else
                PathFind.Hold = hold;

            next_index--;
            return false;
        }

        bool CheckNodes(NodeData[] nodes)
        {
            foreach (NodeData placement in nodes)
            {
                // Make sure piece is intact
                bool intact = true;
                for (int j = 1; j < placement.Ys.Length && intact; j++)
                    if (placement.Ys[j - 1] != placement.Ys[j] - 1)
                        intact = false;
                if (!intact) continue;

                int y = placement.Ys[0] + placement.Piece.MinY;
                PathFind.Current = placement.Piece;
                if (PathFind.PathFind(placement.Piece, placement.X, y, out _))
                {
                    // Place piece
                    PathFind.X = placement.X;
                    PathFind.Y = y;
                    int[] clears = PathFind.Place(out _);
                    solution.Remove(placement);
                    // Move Ys
                    for (int j = clears.Length - 2; j >= 0; j -= 2)
                        if (clears[j + 1] != 0)
                            foreach (NodeData data in solution)
                            {
                                for (int k = 0; k < data.Ys.Length; k++)
                                    if (data.Ys[k] > clears[j])
                                        data.Ys[k] -= clears[j];
                            }
                    // Update Current
                    PathFind.Current = PathFind.Next[next_index];
                    // Check if rest of pieces can be placed; if not, backtrack
                    if (CanPathFind()) return true;
                    // Unmove Ys
                    for (int j = 0; j < clears.Length; j += 2)
                        if (clears[j + 1] != 0)
                            foreach (NodeData data in solution)
                            {
                                for (int k = 0; k < data.Ys.Length; k++)
                                    if (data.Ys[k] >= clears[j])
                                        data.Ys[k] += clears[j];
                            }
                        else break;
                    // Unplace piece
                    solution.Add(placement);
                    PathFind.Current = placement.Piece;
                    PathFind.X = placement.X;
                    PathFind.Y = y;
                    PathFind.Unplace(clears);
                }
            }

            return false;
        }
    }

    static private HashSet<NodeData> GetPlacementsData(Stack<DoublyLinkedMatrixNode> solution)
    {
        HashSet<NodeData> placements = new HashSet<NodeData>(solution.Count);
        foreach (var row in solution)
            if (row.DataIndex != -1)
                placements.Add(row.Data);
        //placements.Add(row.Data.Clone());

        return placements;
    }

    void WaitNext()
    {
        while (!GoNext) Thread.Sleep(10);
        GoNext = false;
    }

    void DrawPiece(NodeData data, bool black)
    {
        ConsoleColor color = black ? ConsoleColor.Black : Game.PieceColors[data.Piece.PieceType];
        int y_index = 0;
        for (int i = 3; i >= 0; i--)
        {
            Game.Games[0].WriteAt((data.Piece.X(i) + data.X) * 2 + 12, 21 - data.Ys[y_index], color, "▒▒");
            if (i > 0)
            {
                if (data.Piece.Y(i) != data.Piece.Y(i - 1))
                    y_index++;
            }
        }
        Game.Games[0].WriteAt(0, 23, ConsoleColor.White, "Nodes searched: " + NodeCount + "           ");
        Game.Games[0].WriteAt(0, 24, ConsoleColor.White, "PCs checked: " + PCCount + "           ");
    }
}
