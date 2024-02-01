/*
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Tetris
{
    class Debug
    {
        static long evaltime = 0;
        static List<int> innodes = new(), outnodes = new();

        static void Main2(string[] args)
        {
            SetWindow(45, 23, 45, 23);
            Console.Title = "Budget Tetris AI";
            Console.CursorVisible = false;
            Console.OutputEncoding = Encoding.Unicode;
            Stopwatch time = new();

            //draw start screen
            StartScreen();

            //game
            StartGame();

            void StartScreen()
            {
                time.Start();
                while (time.ElapsedMilliseconds < 30) ;
                time.Stop();
                Console.BackgroundColor = ConsoleColor.Blue;
                WriteAt(20, 1, ConsoleColor.DarkRed, "T");
                WriteAt(21, 1, ConsoleColor.DarkYellow, "E");
                WriteAt(22, 1, ConsoleColor.Yellow, "T");
                WriteAt(23, 1, ConsoleColor.Green, "R");
                WriteAt(24, 1, ConsoleColor.Cyan, "I");
                WriteAt(25, 1, ConsoleColor.Magenta, "S");
                WriteAt(22, 2, ConsoleColor.Blue, "  ");

                Console.BackgroundColor = ConsoleColor.Black;
                WriteAt(2, 5, ConsoleColor.White, "FOR BEST EXPERIENCE SET FONT TO CONSOLAS");
                WriteAt(9, 6, ConsoleColor.White, "(RIGHT CLICK ON THIS WINDOW");
                WriteAt(11, 7, ConsoleColor.White, "AND CLICK 'PROPERTIES')");

                WriteAt(1, 9, ConsoleColor.White, "CONTROLS:");
                WriteAt(3, 10, ConsoleColor.White, "MOVE - ARROW KEYS/NUMPAD 4 and 6");
                WriteAt(3, 11, ConsoleColor.White, "SOFT DROP - DOWN ARROW/NUMPAD 2");
                WriteAt(3, 12, ConsoleColor.White, "HARD DROP - SPACEBAR/NUMPAD 8");
                WriteAt(3, 13, ConsoleColor.White, "ROTATE CLOCKWISE - X/UP ARROW/NUMPAD 1, 5 AND    9");
                WriteAt(3, 15, ConsoleColor.White, "ROTATE ANTI-CLOCKWISE - Z/NUMPAD 3 AND 7");
                WriteAt(3, 16, ConsoleColor.White, "HOLD - C");
                WriteAt(3, 17, ConsoleColor.White, "PAUSE - ESC/F1");

                WriteAt(12, 21, ConsoleColor.White, "PRESS ANY KEY TO START");
                bool show = true;
                time.Start();
                while (!Console.KeyAvailable)
                {
                    if (time.ElapsedTicks > Stopwatch.Frequency)
                    {
                        time.Restart();
                        show = !show;
                        if (show) WriteAt(12, 21, ConsoleColor.White, "PRESS ANY KEY TO START");
                        else WriteAt(12, 21, ConsoleColor.White, "                      ");
                    }
                }
                Console.ReadKey(true);

                Console.BackgroundColor = ConsoleColor.Black;
                Console.Clear();
            }

            void WriteAt(int x, int y, ConsoleColor color, string text)
            {
                Console.CursorLeft = x;
                Console.CursorTop = y;
                Console.ForegroundColor = color;
                Console.Write(text);
            }

            void StartGame()
            {
                //NN net = LoadNNs(@"C:\Users\Kiyonn\source\repos\Tetris AI\survivalist.txt")[0];
                NN net = LoadNNs(@"C:\Users\Kiyonn\source\repos\Tetris AI\deepsearch.txt")[0];

                bool draw = true, visualise = false, tsdonly = false;
                int visspeed = 40;

                //draw screen onto console
                #region
                Console.CursorVisible = false;
                if (draw)
                {
                    WriteAt(18, 0, ConsoleColor.White, "LINES - 0    ");

                    WriteAt(1, 1, ConsoleColor.White, "╔══HOLD══╗");
                    for (int i = 2; i < 5; i++) WriteAt(1, i, ConsoleColor.White, "║        ║");
                    WriteAt(1, 5, ConsoleColor.White, "╚════════╝");

                    WriteAt(12, 1, ConsoleColor.White, "╔════════════════════╗");
                    for (int i = 2; i < 22; i++) WriteAt(12, i, ConsoleColor.White, "║                    ║");
                    WriteAt(12, 22, ConsoleColor.White, "╚════════════════════╝");

                    WriteAt(35, 1, ConsoleColor.White, "╔══NEXT══╗");
                    for (int i = 2; i < 19; i++) WriteAt(35, i, ConsoleColor.White, "║        ║");
                    WriteAt(35, 19, ConsoleColor.White, "╚════════╝");

                    WriteAt(1, 7, ConsoleColor.White, "╔════════╗");
                    for (int i = 8; i < 12; i++) WriteAt(1, i, ConsoleColor.White, "║        ║");
                    WriteAt(1, 12, ConsoleColor.White, "╚════════╝");
                    WriteAt(2, 8, ConsoleColor.White, "SCORE");
                    WriteAt(2, 9, ConsoleColor.White, "0");
                    WriteAt(2, 10, ConsoleColor.White, "LEVEL");
                    WriteAt(2, 11, ConsoleColor.White, "1");
                }
                #endregion

                //initialisation
                #region
                Random rand = new();
                string[] cleartxt = { "SINGLE", "DOUBLE", "TRIPLE", "TETRIS" };
                ConsoleColor[] piececolors = { ConsoleColor.Black, ConsoleColor.Magenta, ConsoleColor.Cyan, ConsoleColor.DarkYellow, ConsoleColor.DarkBlue, ConsoleColor.Green, ConsoleColor.DarkRed, ConsoleColor.Yellow, ConsoleColor.Gray };
                int[] kicksx = { 0, -1, -1, 0, -1 }, ikicksx = { 0, -2, 1, -2, 1 };
                int[] kicksy = { 0, 0, -1, 2, 2 }, ikicksy = { 0, 0, 0, 1, -2 };
                //[piece][rot][layer]
                int[][][] piecesx = { new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 } }, //empty
                new int[][] { new int[] { 0, 0, -1, 1}, new int[] { 0, 0, 1, 0 }, new int[] { 0, -1, 1, 0 }, new int[] { 0, 0, -1, 0 } }, //T
                new int[][] { new int[] { 0, -1, 1, 2 }, new int[] { 1, 1, 1, 1 }, new int[] { -1, 0, 1, 2 }, new int[] { 0, 0, 0, 0 } }, //I
                new int[][] { new int[] { 1, -1, 1, 0 }, new int[] { 0, 0, 0, 1 }, new int[] { 0, -1, 1, -1 }, new int[] { 0, -1, 0, 0 } }, //L
                new int[][] { new int[] { -1, -1, 1, 0 }, new int[] { 0, 1, 0, 0 }, new int[] { 0, -1, 1, 1 }, new int[] { 0, 0, 0, -1 } }, //J
                new int[][] { new int[] { 0, 1, 0, -1 }, new int[] { 0, 0, 1, 1 }, new int[] { 0, 1, 0, -1 }, new int[] { -1, 0, -1, 0 } }, //S
                new int[][] { new int[] { -1, 0, 0, 1 }, new int[] { 1, 1, 0, 0 }, new int[] { 0, -1, 0, 1 }, new int[] { 0, 0, -1, -1 } }, //Z
                new int[][] { new int[] { 0, 1, 0, 1 }, new int[] { 0, 1, 0, 1 }, new int[] { 0, 1, 0, 1 }, new int[] { 0, 1, 0, 1 } } }; //O
                int[][][] piecesy = { new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 }, new int[] { 0, 0, 0, 0 } }, //empty
                new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, 0, 0, 1 } }, //T
                new int[][] { new int[] { 0, 0, 0, 0 }, new int[] { -1, 0, 1, 2 }, new int[] { 1, 1, 1, 1 }, new int[] { -1, 0, 1, 2 } }, //I
                new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, 0, 1, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, -1, 0, 1 } }, //L
                new int[][] { new int[] { -1, 0, 0, 0 }, new int[] { -1, -1, 0, 1 }, new int[] { 0, 0, 0, 1 }, new int[] { -1, 0, 1, 1 } }, //J
                new int[][] { new int[] { -1 ,-1, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 1, 1 }, new int[] { -1, 0, 0, 1 } }, //S
                new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1, 0, 0, 1 }, new int[] { 0, 0, 1, 1 }, new int[] { -1, 0, 0, 1 } }, //Z
                new int[][] { new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 }, new int[] { -1, -1, 0, 0 } } }; //O
                //pieceleft[piece, rotation]
                int[][] pieceleft = { new int[] { 0, 0, 0, 0 }, new int[] { -1, 0, -1, -1 }, new int[] { -1, 1, -1, 0 }, new int[] { -1, 0, -1, -1 }, new int[] { -1, 0, -1, -1 }, new int[] { -1, 0, -1, -1 }, new int[] { -1, 0, -1, -1 }, new int[] { 0 } };
                int[][] pieceright = { new int[] { 0, 0, 0, 0 }, new int[] { 1, 1, 1, 0 }, new int[] { 2, 1, 2, 0 }, new int[] { 1, 1, 1, 0 }, new int[] { 1, 1, 1, 0 }, new int[] { 1, 1, 1, 0 }, new int[] { 1, 1, 1, 0 }, new int[] { 1 } };

                int[][] screen = new int[40][]; //{ y, x }
                for (int i = 0; i < 40; i++) screen[i] = new int[10];
                long score = 0;
                int level = 1, lines = 0, bagi = 7;
                int[] bag = new int[] { 1, 2, 3, 4, 5, 6, 7 };

                int[] origin = new int[2]; //{ x, y }
                int rotation = 0, hold = 0, current = 0; // 0 = none, 1 = T, 2 = I, 3 = L, 4 = J, 5 = S, 6 = Z, 7 = O
                int[] next = new int[6];
                for (int i = 0; i < next.Length; i++) next[i] = NextPiece();

                int combo = -1, locktimer = 20, erasetimer = 0, movecount = 0, xmul, ymul;
                double vel = 0, G = 1.3 * Math.Log(level) + 3, bestvalue = 0;
                bool lastrotate = false, holdsawpped = false, prevB2B = false, softdrop = false, pc = false;

                ConsoleKey keysave = ConsoleKey.NoName;

                long[][] hashtable = new long[10][];
                for (int i = 0; i < 10; i++)
                {
                    hashtable[i] = new long[40];
                    for (int j = 0; j < 40; j++) hashtable[i][j] = ((long)rand.Next() << 34) + ((long)rand.Next() << 17) + (long)rand.Next();
                }
                long[] piecehashtable = new long[8];
                for (int i = 0; i < 8; i++) piecehashtable[i] = ((long)rand.Next() << 34) + ((long)rand.Next() << 17) + (long)rand.Next();
                long[] holdhashtable = new long[8];
                for (int i = 0; i < 8; i++) holdhashtable[i] = ((long)rand.Next() << 34) + ((long)rand.Next() << 17) + (long)rand.Next();
                long[][] nexthashtable = new long[next.Length][];
                for (int i = 0; i < next.Length; i++)
                {
                    nexthashtable[i] = new long[8];
                    for (int j = 0; j < 8; j++) nexthashtable[i][j] = ((long)rand.Next() << 34) + ((long)rand.Next() << 17) + (long)rand.Next();
                }
                #endregion
                PlaceNextPiece();

                float[] weights = { 0.25f, -10.553231f, -0.8175709f, -0.18374358f }; //{ b2b bonus, sqaured height, caves, pillars }

                //find best move
                G = 0.5;
                double thinktimeinseconds = 0.2, movetresh = -0.03, movemul = 1.01, movetar = 5.9;
                Stopwatch timer = new(), movet = new();
                bool overtime = false, covered = false;
                Dictionary<long, double> hashvals = new();
                Dictionary<long, double> hashevals = new();
                long count = 0, checktime = 0, piececount = 0, totaldepth = 0, thinkt = (long)(thinktimeinseconds * Stopwatch.Frequency / 2);
                int maxdepth = 0, movescool = 1, movedelay = 1, movereact = 1, fillhole = 9, sent = 0;
                timer.Start();
                List<ConsoleKey> moves = StartSearch(false);
                double value = bestvalue;
                List<ConsoleKey> swapmoves = StartSearch(true);
                timer.Stop();
                if (bestvalue >= value)
                {
                    if (bestvalue != value || moves.Count > swapmoves.Count + 1)
                    {
                        moves = swapmoves;
                        moves.Insert(0, ConsoleKey.C);
                    }
                }

                //game loop
                long framecount = 0;
                int trashcool = 20, trashaddcool = 160, trashcounter = 0, hole = rand.Next(10), holecool = 5 + rand.Next(5);
                List<int> garbage2 = new();
                int[] trash = garbage2.ToArray();
                while (true)
                {
                    time.Restart();
                    if (hashvals.Count > 200000) hashvals.Clear();
                    if (hashevals.Count > 2000000) hashevals.Clear();
                    framecount++;
                    softdrop = false;
                    int y;

                    //handle input
                    movescool--;
                    if (movescool == 0)
                    {
                        if (moves.Count != 0)
                        {
                            keysave = moves[0];
                            movescool = movedelay;
                        }
                    }
                    if (keysave != ConsoleKey.NoName)
                    {
                        bool validmove = true;
                        switch (keysave)
                        {
                            //hold
                            case ConsoleKey.C:
                                {
                                    if (!holdsawpped)
                                    {
                                        lastrotate = false;
                                        holdsawpped = true;
                                        int oldhold = hold;
                                        hold = current;
                                        movecount = 0;
                                        locktimer = 20;
                                        //undraw piece
                                        //if (draw) for (int i = 0; i < 4; i++) if (origin[1] + piecesy[current][rotation][i] > 19) WriteAt((origin[0] + piecesx[current][rotation][i]) * 2 + 13, origin[1] + piecesy[current][rotation][i] - 18, piececolors[0], "██");

                                        if (oldhold == 0) { if (PlaceNextPiece()) break; }
                                        else
                                        {
                                            current = oldhold;
                                            //undraw oldhold in hold
                                            if (draw) for (int i = 0; i < 4; i++) WriteAt(piecesx[oldhold][0][i] * 2 + 4, piecesy[oldhold][0][i] + 3, piececolors[0], "██");
                                            //move new piece to top
                                            origin = new int[] { 4, 18 };
                                            rotation = 0;
                                            vel = 0;
                                            //immediately drop if possible
                                            if (!OnGround(screen, origin, current, rotation)) origin[1]++;
                                            //check for block out
                                            bool lose = false;
                                            for (int i = 0; i < 4; i++) if (screen[origin[1] + piecesy[current][rotation][i]][origin[0] + piecesx[current][rotation][i]] != 0) lose = true;
                                            if (lose) break;
                                        }
                                        //draw new hold in hold
                                        if (draw) for (int i = 0; i < 4; i++) WriteAt(piecesx[hold][0][i] * 2 + 4, piecesy[hold][0][i] + 3, piececolors[hold], "██");
                                    }
                                    break;
                                }
                            //move left
                            case ConsoleKey.NumPad4: goto case ConsoleKey.LeftArrow;
                            case ConsoleKey.LeftArrow:
                                {
                                    for (int i = 0; i < 4; i++)
                                        if (origin[0] + piecesx[current][rotation][i] == 0) validmove = false;
                                        else validmove &= screen[origin[1] + piecesy[current][rotation][i]][origin[0] + piecesx[current][rotation][i] - 1] == 0;
                                    if (validmove)
                                    {
                                        RedrawPiece(-1, 0, 0);
                                        if (movecount < 15) locktimer = 20;
                                        movecount++;
                                    }
                                    break;
                                }
                            //move right
                            case ConsoleKey.NumPad6: goto case ConsoleKey.RightArrow;
                            case ConsoleKey.RightArrow:
                                {
                                    for (int i = 0; i < 4; i++)
                                        if (origin[0] + piecesx[current][rotation][i] == 9) validmove = false;
                                        else validmove &= screen[origin[1] + piecesy[current][rotation][i]][origin[0] + piecesx[current][rotation][i] + 1] == 0;
                                    if (validmove)
                                    {
                                        RedrawPiece(1, 0, 0);
                                        if (movecount < 15) locktimer = 20;
                                        movecount++;
                                    }
                                    break;
                                }
                            //soft drop
                            case ConsoleKey.NumPad2: goto case ConsoleKey.DownArrow;
                            case ConsoleKey.DownArrow: softdrop = true; break;
                            //hard drop
                            case ConsoleKey.NumPad8: goto case ConsoleKey.Spacebar;
                            case ConsoleKey.Spacebar:
                                {
                                    int movedown = 0;
                                    for (; !OnGround(screen, origin, current, rotation); origin[1]++, movedown++) score += 2; //add 2 score per cell
                                    origin[1] -= movedown;
                                    locktimer = 0; //lock piece
                                    if (movedown != 0)
                                    {
                                        RedrawPiece(0, movedown, 0);
                                        if (draw) WriteAt(2, 9, ConsoleColor.White, score.ToString());
                                    }
                                    break;
                                }
                            //rotate anti-clockwise
                            case ConsoleKey.NumPad3: goto case ConsoleKey.Z;
                            case ConsoleKey.NumPad7: goto case ConsoleKey.Z;
                            case ConsoleKey.Z:
                                {
                                    if (current != 7)
                                    {
                                        int test = RotateTest(screen, origin, current, rotation, false);
                                        if (movecount >= 15 && OnGround(screen, origin, current, rotation)) ;
                                        else if (test != -1)
                                        {
                                            if (current == 2) RedrawPiece(ikicksx[test] * xmul, ikicksy[test] * ymul, -1);
                                            else RedrawPiece(kicksx[test] * xmul, kicksy[test] * ymul, -1);
                                            if (movecount < 15) locktimer = 20;
                                            movecount++;
                                        }
                                    }
                                    break;
                                }
                            //rotate clockwise
                            case ConsoleKey.NumPad1: goto case ConsoleKey.UpArrow;
                            case ConsoleKey.NumPad5: goto case ConsoleKey.UpArrow;
                            case ConsoleKey.NumPad9: goto case ConsoleKey.UpArrow;
                            case ConsoleKey.X: goto case ConsoleKey.UpArrow;
                            case ConsoleKey.UpArrow:
                                {
                                    if (current != 7)
                                    {
                                        int test = RotateTest(screen, origin, current, rotation, true);
                                        if (movecount >= 15 && OnGround(screen, origin, current, rotation)) ;
                                        else if (test != -1)
                                        {
                                            if (current == 2) RedrawPiece(ikicksx[test] * xmul, ikicksy[test] * ymul, 1);
                                            else RedrawPiece(kicksx[test] * xmul, kicksy[test] * ymul, 1);
                                            if (movecount < 15) locktimer = 20;
                                            movecount++;
                                        }
                                    }
                                    break;
                                }
                        }
                        keysave = ConsoleKey.NoName;
                        if (moves.Count != 0) if (moves[0] != ConsoleKey.DownArrow) moves.RemoveAt(0);
                    }

                    //erase stuff on the bottom left
                    if (erasetimer-- == 0) EraseClearStats();

                    //handle locking and gravity
                    vel += softdrop ? Math.Max(G, 1) : G;
                    if (OnGround(screen, origin, current, rotation))
                    {
                        vel = 0;
                        locktimer--;
                        if (movecount > 15) locktimer = -1;
                        if (moves.Count != 0) if (moves[0] == ConsoleKey.DownArrow) moves.RemoveAt(0);
                    }
                    else
                    {
                        int down = 0;
                        for (; !OnGround(screen, origin, current, rotation) && vel > 0; vel--, down++)
                        {
                            origin[1]++;
                            if (softdrop) score++; //add 1 score per soft drop
                        }
                        if (down != 0)
                        {
                            origin[1] -= down;
                            RedrawPiece(0, down, 0);
                            if (draw) if (softdrop) WriteAt(2, 9, ConsoleColor.White, score.ToString());
                        }
                    }
                    if (locktimer < 0)
                    {
                        locktimer = 20;
                        movecount = 0;
                        holdsawpped = false;
                        //check for lock out
                        bool lose = true;
                        for (int i = 0; i < 4; i++) if (origin[1] + piecesy[current][rotation][i] > 19) lose = false;
                        if (lose) break;
                        //clear any lines that should be cleared and play vfx
                        ClearLines();
                        //bring next piece onto screen, check for block out
                        if (PlaceNextPiece()) break;
                        timer.Start();
                        moves = StartSearch(false);
                        value = bestvalue;
                        swapmoves = StartSearch(true);
                        timer.Stop();
                        if (bestvalue >= value)
                        {
                            if (bestvalue != value || moves.Count > swapmoves.Count + 1)
                            {
                                moves = swapmoves;
                                moves.Insert(0, ConsoleKey.C);
                            }
                        }
                        movescool = movereact;
                    }

                    //trash
                    trashaddcool--;
                    if (trashaddcool == 0)
                    {
                        garbage2.Add(Math.Min(5, rand.Next(level / 3 + 1)));
                        trashaddcool = 160;
                    }
                    y = 21;
                    if (garbage2.Count != 0)
                    {
                        trashcool--;
                        trash = garbage2.ToArray();
                        trash[0] -= trashcounter;
                        for (; y > 21 - trash[0]; y--) WriteAt(34, y, ConsoleColor.Red, "█");
                        for (int i = 1; i < trash.Length; i++) for (int j = y; y > j - trash[i]; y--) WriteAt(34, y, ConsoleColor.Gray, "█");
                    }
                    else trashcool = 20;
                    for (; y > 1; y--) WriteAt(34, y, ConsoleColor.Black, "█");

                    //logging
                    timer.Stop();
                    if (!visualise)
                    {
                        WriteAt(0, 0, ConsoleColor.White, "total time: " + (float)timer.ElapsedMilliseconds / piececount * 2);
                        WriteAt(0, 1, ConsoleColor.White, "nodes: " + (float)count / piececount);
                        WriteAt(0, 2, ConsoleColor.White, "depth: " + (float)totaldepth / piececount);
                        WriteAt(0, 3, ConsoleColor.White, "check time: " + (float)checktime / piececount / 10000);
                        WriteAt(0, 4, ConsoleColor.White, "eval time: " + (float)evaltime / piececount / 10000);
                        WriteAt(0, 5, ConsoleColor.White, "tresh: " + movetresh);
                        WriteAt(0, 6, ConsoleColor.White, "sent: " + sent);
                    }
                }

                EraseClearStats();
                Console.ReadKey();

                return;

                void RedrawPiece(int dx, int dy, int dr)
                {
                    //undraw piece
                    //if (draw) for (int i = 0; i < 4; i++) if (origin[1] + piecesy[current][rotation][i] > 19) WriteAt((origin[0] + piecesx[current][rotation][i]) * 2 + 13, origin[1] + piecesy[current][rotation][i] - 18, piececolors[0], "██");
                    //move or rotate piece
                    origin[0] += dx;
                    origin[1] += dy;
                    rotation = (rotation + dr + 4) % 4;
                    if (dr == 0) lastrotate = false;
                    else lastrotate = true;
                    //redraw piece
                    //if (draw) for (int i = 0; i < 4; i++) if (origin[1] + piecesy[current][rotation][i] > 19) WriteAt((origin[0] + piecesx[current][rotation][i]) * 2 + 13, origin[1] + piecesy[current][rotation][i] - 18, piececolors[current], "██");
                }

                bool OnGround(int[][] matrix, int[] pos, int piece, int rot)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int y = pos[1] + piecesy[piece][rot % 4][i] + 1;
                        if (y == 40) return true;
                        if (matrix[y][pos[0] + piecesx[piece][rot % 4][i]] != 0) return true;
                    }
                    return false;
                }

                int RotateTest(int[][] matrix, int[] pos, int piece, int rot, bool clockwise)
                {
                    rot = (rot + 4) % 4;
                    xmul = !clockwise ^ rot > 1 ^ (rot % 2 == 1 && clockwise) ? -1 : 1;
                    ymul = rot % 2 == 1 ? -1 : 1;
                    int testrotation = clockwise ? (rot + 1) % 4 : (rot + 3) % 4;
                    if (piece == 2) ymul *= rot > 1 ^ !clockwise ? -1 : 1;
                    int[] testorder = piece == 2 && (rot % 2 == 1 ^ !clockwise) ? new int[] { 0, 2, 1, 4, 3 } : new int[] { 0, 1, 2, 3, 4 };
                    foreach (int test in testorder)
                    {
                        bool pass = true;
                        for (int i = 0; i < 4; i++)
                        {
                            int x, y;
                            if (piece == 2)
                            {
                                x = pos[0] + piecesx[piece][testrotation][i] + (ikicksx[test] * xmul);
                                y = pos[1] + piecesy[piece][testrotation][i] + (ikicksy[test] * ymul);
                            }
                            else
                            {
                                x = pos[0] + piecesx[piece][testrotation][i] + (kicksx[test] * xmul);
                                y = pos[1] + piecesy[piece][testrotation][i] + (kicksy[test] * ymul);
                            }
                            if (x < 0 || x > 9 || y > 39) pass = false;
                            else if (matrix[y][x] != 0) pass = false;
                        }
                        if (pass) return test;
                    }

                    return -1;
                }

                bool PlaceNextPiece()
                {
                    lastrotate = false;
                    //undraw pieces in next
                    if (draw) for (int i = 0; i < 6; i++) for (int j = 0; j < 4; j++) WriteAt(38 + piecesx[next[i]][0][j] * 2, 3 + (3 * i) + piecesy[next[i]][0][j], piececolors[0], "██");
                    //update pieces in current and next
                    current = next[0];
                    for (int i = 0; i < next.Length - 1; i++) next[i] = next[i + 1];
                    next[next.Length - 1] = NextPiece();
                    //redraw next
                    if (draw) for (int i = 0; i < 6; i++) for (int j = 0; j < 4; j++) WriteAt(38 + piecesx[next[i]][0][j] * 2, 3 + (3 * i) + piecesy[next[i]][0][j], piececolors[next[i]], "██");
                    //move new piece to top
                    origin = new int[] { 4, 18 };
                    rotation = 0;
                    //immediately drop if possible
                    if (!OnGround(screen, origin, current, rotation)) origin[1]++;
                    vel = 0;
                    //check for block out
                    for (int i = 0; i < 4; i++) if (screen[origin[1] + piecesy[current][rotation][i]][origin[0] + piecesx[current][rotation][i]] != 0) return true;
                    //draw piece
                    origin[0]--;
                    origin[1] -= 4;
                    RedrawPiece(1, 4, 0);
                    return false;
                }

                int NextPiece()
                {
                    if (bagi == 7)
                    {
                        //refil bag
                        for (int i = 0; i < 7; i++)
                        {
                            int swapi = rand.Next(7 - i) + i, swap = bag[swapi];
                            bag[swapi] = bag[i];
                            bag[i] = swap;
                        }
                        bagi = 0;
                    }
                    return bag[bagi++];
                }

                void EraseClearStats()
                {
                    if (draw)
                    {
                        WriteAt(5, 14, ConsoleColor.Black, "███");
                        WriteAt(1, 15, ConsoleColor.Black, "███████████");
                        WriteAt(3, 16, ConsoleColor.Black, "██████");
                        WriteAt(2, 17, ConsoleColor.Black, "██████████");
                        WriteAt(1, 18, ConsoleColor.Black, "███████████");
                    }
                }

                void ClearLines()
                {
                    //draw piece
                    if (draw) for (int i = 0; i < 4; i++) if (origin[1] + piecesy[current][rotation][i] > 19) WriteAt((origin[0] + piecesx[current][rotation][i]) * 2 + 13, origin[1] + piecesy[current][rotation][i] - 18, piececolors[current], "██");
                    //update stats
                    int garbage;
                    int[] info = Score(screen, prevB2B, origin, current, rotation, combo, lastrotate, out garbage); //{ scoreadd, B2B, T-spin, combo, clears } //first bit of B2B = B2B chain status
                    int cleared = info.Length - 4;
                    prevB2B = (info[1] & 1) == 1;
                    score += info[0] * level;
                    if (pc)
                    {
                        score += level * new int[] { 800, 1200, 1800, 2000 }[cleared - 1];
                        if ((info[1] & 2) == 2) score += 1200;
                    }
                    lines += cleared;
                    level = lines / 10 + 1;
                    combo = info[3];
                    sent += garbage;
                    //G = (float)Math.Pow(-0.007 * level + 0.807, 1 - level) / 40;
                    //erase if there is stuff to write
                    if (info[2] > 1 || cleared > 0) EraseClearStats();
                    if (draw)
                    {
                        //write to console
                        WriteAt(2, 9, ConsoleColor.White, score.ToString());
                        WriteAt(2, 11, ConsoleColor.White, level.ToString());
                        WriteAt(26, 0, ConsoleColor.White, lines.ToString());
                        //write the kind of clear onto conosle
                        if ((info[1] & 2) != 0) WriteAt(5, 14, ConsoleColor.White, "B2B");
                        if (info[2] == 2) WriteAt(1, 15, ConsoleColor.White, "T-SPIN MINI");
                        else if (info[2] == 3) WriteAt(3, 15, ConsoleColor.White, "T-SPIN");
                        if (cleared > 0) WriteAt(3, 16, ConsoleColor.White, cleartxt[cleared - 1]);
                        if (combo > 0) WriteAt(2, 17, ConsoleColor.White, combo + " COMBO!");
                        erasetimer = 14;

                        //garbage cancelling
                        if (garbage != 0)
                        {
                            if (garbage2.Count != 0)
                            {
                                int trash;
                                trashcounter += garbage;
                                trash = garbage2[0];
                                while (trashcounter >= trash && garbage2.Count != 0)
                                {
                                    trash = garbage2[0];
                                    garbage2.RemoveAt(0);
                                    trashcounter -= trash;
                                    subhole(trash);
                                    if (garbage2.Count != 0) trash = garbage2[0];
                                }
                                if (garbage2.Count == 0)
                                {
                                    trashcool = 20;
                                    trashcounter = 0;
                                }
                            }
                            else
                            {
                                trashcool = 20;
                                trashcounter = 0;
                            }
                        }
                        //dump the trash
                        if (trashcool <= 0 && cleared == 0)
                        {
                            trashcool = 20;
                            int addlines;
                            addlines = garbage2[0];
                            garbage2.RemoveAt(0);

                            subhole(addlines);
                            addlines -= trashcounter;
                            for (int x = 0; x < 10; x++) for (int y = 19; y < 40; y++) screen[y - addlines][x] = screen[y][x];
                            for (int x = 0; x < 10; x++) for (int y = 39; y > 39 - addlines; y--) screen[y][x] = x == hole ? 0 : 8;
                            trashcounter = 0;
                            cleared = 1;
                        }
                        //redraw screen
                        if (cleared != 0) for (int x = 0; x < 10; x++) for (int y = 39; y > 19; y--) WriteAt(13 + x * 2, y - 18, piececolors[screen[y][x]], "██");
                    }
                }

                int[] Score(int[][] matrix, bool _b2b, int[] pos, int piece, int rot, int comb, bool lastrot, out int trash)
                {
                    //{ scoreadd, B2B, T-spin, combo, clears } //first bit of B2B = B2B chain status
                    rot = (rot + 8) % 4;
                    //check for t-spins
                    int tspin = TSpin(matrix, pos, piece, rot, lastrot); //0 = no spin, 2 = mini, 3 = t-spin
                    //write piece onto screen
                    for (int i = 0; i < 4; i++) matrix[pos[1] + piecesy[piece][rot][i]][pos[0] + piecesx[piece][rot][i]] = piece;
                    //find cleared lines
                    int cleared = 0, scoreadd = 0;
                    int[] clears = new int[4];
                    for (int i = 0; i < 4 && cleared != 4; i++)
                    {
                        if (i > 0) if (piecesy[piece][rot][i] == piecesy[piece][rot][i - 1]) continue;

                        int y = pos[1] + piecesy[piece][rot][i];
                        bool clear = true;
                        for (int x = 0; x < 10 && clear; x++) if (matrix[y][x] == 0) clear = false;
                        if (clear) clears[cleared++] = y;
                    }
                    //clear lines
                    if (cleared != 0)
                    {
                        int movedown = 1;
                        for (int y = clears[cleared - 1] - 1; y > 13; y--)
                        {
                            if (movedown == cleared) matrix[y + movedown] = matrix[y];
                            else if (clears[cleared - movedown - 1] == y) movedown++;
                            else matrix[y + movedown] = matrix[y];
                        }
                        //add new empty rows
                        for (; movedown > 0; movedown--) matrix[13 + movedown] = new int[10];
                    }
                    //combo
                    comb = cleared == 0 ? -1 : comb + 1;
                    //t-spins
                    if (tspin == 3) scoreadd += new int[] { 400, 700, 900, 1100 }[cleared];
                    if (tspin == 2) scoreadd += 100;
                    //line clears
                    scoreadd += new int[] { 0, 100, 300, 500, 800 }[cleared];
                    //perfect clear
                    pc = true;
                    for (int x = 0; x < 10; x++) if (matrix[39][x] != 0) pc = false;
                    //compute score
                    int b2b = _b2b ? 1 : 0;
                    if (tspin + cleared > 3)
                    {
                        if (_b2b)
                        {
                            scoreadd += scoreadd / 2; //B2B bonus
                            b2b |= 2;
                        }
                        b2b |= 1;
                    }
                    if (comb != -1) scoreadd += 50 * comb;
                    if (tspin == 0 && cleared != 4 && cleared != 0) b2b = 0;

                    int[] info = new int[4 + cleared];
                    info[0] = scoreadd;
                    info[1] = b2b;
                    info[2] = tspin;
                    info[3] = comb;
                    for (int i = 4; i < cleared + 4; i++) info[i] = clears[i - 4];

                    trash = new int[] { 0, 0, 1, 2, 4 }[cleared];
                    if (info[2] == 3) trash += new int[] { 0, 2, 3, 4 }[cleared];
                    if ((info[1] & 2) == 2) trash++;
                    if (pc) trash += 4;
                    if (comb > 0) trash += new int[] { 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }[Math.Min(comb - 1, 9)];
                    if (trash > 20) trash = 20;

                    return info;
                }

                int TSpin(int[][] matrix, int[] pos, int piece, int rot, bool lastrot)
                {
                    rot = (rot + 4) % 4;
                    if (lastrot && piece == 1)
                    {
                        bool[] corners = new bool[4]; //{ top left, top right, bottom left, bottom right }
                        if (pos[0] - 1 < 0)
                        {
                            corners[0] = true;
                            corners[2] = true;
                        }
                        else
                        {
                            corners[0] = matrix[pos[1] - 1][pos[0] - 1] != 0;
                            if (pos[1] + 1 > 39) corners[2] = true;
                            else corners[2] = matrix[pos[1] + 1][pos[0] - 1] != 0;
                        }
                        if (pos[0] + 1 > 9)
                        {
                            corners[1] = true;
                            corners[3] = true;
                        }
                        else
                        {
                            corners[1] = matrix[pos[1] - 1][pos[0] + 1] != 0;
                            if (pos[1] + 1 > 39) corners[3] = true;
                            else corners[3] = matrix[pos[1] + 1][pos[0] + 1] != 0;
                        }

                        //3 corner rule
                        int count = 0;
                        foreach (bool corner in corners) if (corner) count++;
                        if (count > 2)
                        {
                            //check if mini
                            switch (rot)
                            {
                                case 0:
                                    {
                                        if (matrix[pos[1] - 1][pos[0] - 1] == 0 || matrix[pos[1] - 1][pos[0] + 1] == 0) return 2;
                                        break;
                                    }
                                case 1:
                                    {
                                        if (matrix[pos[1] + 1][pos[0] + 1] == 0 || matrix[pos[1] - 1][pos[0] + 1] == 0) return 2;
                                        break;
                                    }
                                case 2:
                                    {
                                        if (matrix[pos[1] + 1][pos[0] - 1] == 0 || matrix[pos[1] + 1][pos[0] + 1] == 0) return 2;
                                        break;
                                    }
                                case 3:
                                    {
                                        if (matrix[pos[1] - 1][pos[0] - 1] == 0 || matrix[pos[1] + 1][pos[0] - 1] == 0) return 2;
                                        break;
                                    }
                            }
                            return 3;
                        }
                    }
                    return 0;
                }

                List<ConsoleKey> StartSearch(bool swap)
                {
                    piececount++;
                    movet.Restart();
                    List<ConsoleKey> bestmoves = new();
                    bestvalue = double.MinValue;
                    int piece = swap ? hold : current, _hold = swap ? piece : hold, nexti = 0;
                    if (swap && hold == 0)
                    {
                        piece = next[0];
                        nexti++;
                    }
                    //find fillhole
                    int highesttrash = 39;
                    covered = false;
                    for (; screen[highesttrash][0] == 8 || screen[highesttrash][1] == 8; highesttrash--) ;
                    highesttrash++;
                    if (highesttrash != 40)
                    {
                        for (int x = 0; x < 10; x++)
                        {
                            if (screen[highesttrash][x] == 0)
                            {
                                for (int y = highesttrash - 1; y > 16 && !covered; y--) if (screen[y][x] != 0) covered = true;
                                fillhole = x;
                            }
                        }
                    }
                    //find stateval
                    long statehash = 0;
                    double stateval;
                    for (int x = 0; x < 10; x++) for (int y = 16; y < 40; y++) if (screen[y][x] != 0) statehash ^= hashtable[x][y];
                    if (hashevals.ContainsKey(statehash)) stateval = hashevals[statehash];
                    else
                    {
                        List<double> feat = ExtrFeat(screen, 0);
                        double val = net.FeedFoward(feat)[0], newval = net.FeedFoward(feat)[0];
                        for (; newval != val; newval = net.FeedFoward(feat)[0]) val = newval;
                        stateval = newval;
                        hashevals.Add(statehash, stateval);
                    }
                    //keep searching until time's up or no next pieces left
                    overtime = false;
                    for (int depth = 1; !overtime && depth != next.Length + 1; depth++)
                    {
                        maxdepth = depth;
                        for (int rot = 0; rot < 4; rot++)
                        {
                            if (overtime) break;

                            List<ConsoleKey> tempmoves = new();
                            if (rot == 3) tempmoves.Add(ConsoleKey.Z);
                            else for (int i = 0; i < rot; i++) tempmoves.Add(ConsoleKey.UpArrow);

                            int left = pieceleft[piece][rot], right = pieceright[piece][rot];
                            for (int i = 0; i < 5 + left; i++) tempmoves.Add(ConsoleKey.LeftArrow);

                            for (int orix = -left; orix < 10 - right; orix++)
                            {
                                if (overtime) break;

                                if (tempmoves.Count == 0) tempmoves.Add(ConsoleKey.RightArrow);
                                else if (tempmoves[tempmoves.Count - 1] == ConsoleKey.LeftArrow) tempmoves.RemoveAt(tempmoves.Count - 1);
                                else tempmoves.Add(ConsoleKey.RightArrow);
                                if (!CanMove(tempmoves.ToArray(), screen, piece)) continue;

                                int oriy = 18;
                                //hard drop
                                while (!OnGround(screen, new int[] { orix, oriy }, piece, rot)) oriy++;
                                if (visualise)
                                {
                                    WriteAt(0, 0, ConsoleColor.White, "depth: " + depth);
                                    WriteAt(0, 1, ConsoleColor.White, "nodes: " + (float)count / piececount);
                                    WriteAt(0, 2, ConsoleColor.White, "trans: " + hashvals.Count);
                                    for (int x = 0; x < 10; x++) for (int y = 39; y > 19; y--) WriteAt(13 + x * 2, y - 18, piececolors[screen[y][x]], "██");
                                    for (int i = 0; i < 4; i++) if (oriy + piecesy[piece][rot][i] > 19) WriteAt((orix + piecesx[piece][rot][i]) * 2 + 13, oriy + piecesy[piece][rot][i] - 18, piececolors[piece], "▒▒");
                                    timer.Restart();
                                    while (timer.ElapsedMilliseconds < visspeed) ;
                                }
                                //update screen
                                int[][] vscreen = Clone(screen);
                                int garbage;
                                int[] info = Score(vscreen, prevB2B, new int[] { orix, oriy }, piece, rot, combo, false, out garbage);
                                //check if better
                                double newvalue;
                                if (!tsdonly || info[0] == 0 || (info.Length - 4 == 2 && info[2] == 3))
                                {
                                    if (Check(vscreen, piece, rot, orix, oriy, info))
                                    {
                                        if (visualise) for (int x = 0; x < 10; x++) for (int y = 39; y > 19; y--) WriteAt(13 + x * 2, y - 18, piececolors[vscreen[y][x]], "██");
                                        newvalue = Search(vscreen, depth - 1, nexti + 1, next[nexti], _hold, false, info[3], (info[1] & 1) == 1, garbage, stateval);
                                        if (newvalue >= bestvalue)
                                        {
                                            if (newvalue != bestvalue || bestmoves.Count >= tempmoves.Count)
                                            {
                                                bestvalue = newvalue;
                                                bestmoves = new List<ConsoleKey>(tempmoves);
                                            }
                                        }
                                    }
                                }
                                //try spin clockwise
                                tempmoves.Add(ConsoleKey.DownArrow);
                                if (piece != 7)
                                {
                                    int x = orix, y = oriy, rotate;
                                    int test = RotateTest(screen, new int[] { x, y }, piece, rot, true);
                                    for (rotate = 1; test != -1 && rotate < 3; test = RotateTest(screen, new int[] { x, y }, piece, rot + rotate, true), rotate++)
                                    {
                                        tempmoves.Add(ConsoleKey.UpArrow);
                                        //apply kick
                                        if (piece == 2)
                                        {
                                            x += ikicksx[test] * xmul;
                                            y += ikicksy[test] * ymul;
                                        }
                                        else
                                        {
                                            x += kicksx[test] * xmul;
                                            y += kicksy[test] * ymul;
                                        }
                                        if (!OnGround(screen, new int[] { x, y }, piece, rot + rotate)) break;
                                        if (visualise)
                                        {
                                            WriteAt(0, 0, ConsoleColor.White, "depth: " + depth);
                                            WriteAt(0, 1, ConsoleColor.White, "nodes: " + (float)count / piececount);
                                            WriteAt(0, 2, ConsoleColor.White, "trans: " + hashvals.Count);
                                            for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[screen[j][i]], "██");
                                            for (int i = 0; i < 4; i++) if (y + piecesy[piece][rot][i] > 19) WriteAt((x + piecesx[piece][rot][i]) * 2 + 13, y + piecesy[piece][rot][i] - 18, piececolors[piece], "▒▒");
                                            timer.Restart();
                                            while (timer.ElapsedMilliseconds < visspeed) ;
                                        }
                                        //update screen
                                        vscreen = Clone(screen);
                                        info = Score(vscreen, prevB2B, new int[] { x, y }, piece, rot + rotate, combo, true, out garbage);
                                        //check if better
                                        if (visualise) for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[vscreen[j][i]], "██");
                                        if (!tsdonly || info[0] == 0 || (info.Length - 4 == 2 && info[2] == 3))
                                        {
                                            if (Check(vscreen, piece, (rot + rotate) % 4, x, y, info))
                                            {
                                                newvalue = Search(vscreen, depth - 1, nexti + 1, next[nexti], _hold, false, info[3], (info[1] & 1) == 1, garbage, stateval);
                                                if (newvalue >= bestvalue)
                                                {
                                                    if (newvalue != bestvalue || bestmoves.Count > tempmoves.Count)
                                                    {
                                                        bestvalue = newvalue;
                                                        bestmoves = new List<ConsoleKey>(tempmoves);
                                                    }
                                                }
                                            }
                                        }
                                        if (piece == 2 || piece == 3 || piece == 4) break;
                                    }
                                    while (tempmoves[tempmoves.Count - 1] == ConsoleKey.UpArrow) tempmoves.RemoveAt(tempmoves.Count - 1);
                                }
                                //try spin anti-clockwise
                                if (piece != 7)
                                {
                                    int x = orix, y = oriy, rotate;
                                    int test = RotateTest(screen, new int[] { x, y }, piece, rot, false);
                                    for (rotate = -1; test != -1 && rotate > -3; test = RotateTest(screen, new int[] { x, y }, piece, rot + rotate, false), rotate--)
                                    {
                                        tempmoves.Add(ConsoleKey.Z);
                                        //apply kick
                                        if (piece == 2)
                                        {
                                            x += ikicksx[test] * xmul;
                                            y += ikicksy[test] * ymul;
                                        }
                                        else
                                        {
                                            x += kicksx[test] * xmul;
                                            y += kicksy[test] * ymul;
                                        }
                                        if (!OnGround(screen, new int[] { x, y }, piece, rot + rotate + 4)) break;
                                        if (visualise)
                                        {
                                            WriteAt(0, 0, ConsoleColor.White, "depth: " + depth);
                                            WriteAt(0, 1, ConsoleColor.White, "nodes: " + (float)count / piececount);
                                            WriteAt(0, 2, ConsoleColor.White, "trans: " + hashvals.Count);
                                            for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[screen[j][i]], "██");
                                            for (int i = 0; i < 4; i++) if (y + piecesy[piece][rot][i] > 19) WriteAt((x + piecesx[piece][rot][i]) * 2 + 13, y + piecesy[piece][rot][i] - 18, piececolors[piece], "▒▒");
                                            timer.Restart();
                                            while (timer.ElapsedMilliseconds < visspeed) ;
                                        }
                                        //lock piece
                                        vscreen = Clone(screen);
                                        info = Score(vscreen, prevB2B, new int[] { x, y }, piece, rot + rotate, combo, true, out garbage);
                                        //check if better
                                        if (visualise) for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[vscreen[j][i]], "██");
                                        if (!tsdonly || info[0] == 0 || (info.Length - 4 == 2 && info[2] == 3))
                                        {
                                            if (Check(vscreen, piece, (rot + rotate + 4) % 4, x, y, info))
                                            {
                                                newvalue = Search(vscreen, depth - 1, nexti + 1, next[nexti], _hold, false, info[3], (info[1] & 1) == 1, garbage, stateval);
                                                if (newvalue >= bestvalue)
                                                {
                                                    if (newvalue != bestvalue || bestmoves.Count > tempmoves.Count)
                                                    {
                                                        bestvalue = newvalue;
                                                        bestmoves = new List<ConsoleKey>(tempmoves);
                                                    }
                                                }
                                            }
                                        }
                                        if (piece == 2 || piece == 3 || piece == 4) break;
                                    }
                                    while (tempmoves[tempmoves.Count - 1] == ConsoleKey.Z) tempmoves.RemoveAt(tempmoves.Count - 1);
                                }

                                tempmoves.Remove(ConsoleKey.DownArrow);
                            }
                            //o pieces can't be rotated
                            if (piece == 7) break;
                        }
                    }
                    totaldepth += maxdepth;
                    //addjust movetresh
                    movetresh *= Math.Pow(movemul, maxdepth - movetar);
                    movetresh = Math.Min(movetresh, -0.02);

                    bestmoves.Add(ConsoleKey.Spacebar);
                    return bestmoves;
                }

                double Search(int[][] matrix, int depth, int nexti, int piece, int _hold, bool swapped, int comb, bool b2b, double trash, double prevstate)
                {
                    if (movet.ElapsedTicks > thinkt && !visualise && thinkt >= 0)
                    {
                        overtime = true;
                        return double.MinValue;
                    }

                    count++;

                    if (depth == 0 || nexti == next.Length)
                    {
                        long hash = 0;
                        for (int x = 0; x < 10; x++) for (int y = 16; y < 40; y++) if (matrix[y][x] != 0) hash ^= hashtable[x][y];
                        if (hashevals.ContainsKey(hash)) return hashevals[hash];
                        List<double> feat = ExtrFeat(matrix, trash);
                        double val = net.FeedFoward(feat)[0], newval = net.FeedFoward(feat)[0];
                        if (visualise)
                        {
                            WriteAt(0, 0, ConsoleColor.White, "depth: " + depth);
                            WriteAt(0, 3, ConsoleColor.White, "count: " + count);
                            hashevals.Add(hash, newval);
                            return newval;
                        }
                        for (; newval != val; newval = net.FeedFoward(feat)[0]) val = newval;
                        hashevals.Add(hash, newval);

                        return newval;
                    }
                    else
                    {
                        double value = double.MinValue;
                        //have we seen this situation before?
                        long hash = HashBoard(matrix, piece, _hold, nexti, depth);
                        if (hashvals.ContainsKey(hash)) return hashvals[hash];
                        //find stateval
                        long statehash = 0;
                        double stateval;
                        for (int x = 0; x < 10; x++) for (int y = 16; y < 40; y++) if (matrix[y][x] != 0) statehash ^= hashtable[x][y];
                        if (hashevals.ContainsKey(statehash)) stateval = hashevals[statehash];
                        else
                        {
                            List<double> feat = ExtrFeat(matrix, trash);
                            double val = net.FeedFoward(feat)[0], newval = net.FeedFoward(feat)[0];
                            for (; newval != val; newval = net.FeedFoward(feat)[0]) val = newval;
                            stateval = newval;
                            hashevals.Add(statehash, stateval);
                        }
                        //was the last move shit?
                        double discount = Math.Pow(0.95, maxdepth - depth);
                        double discount2 = Math.Pow(1 / (1 + Math.Exp(-2.5 * stateval)), maxdepth - depth);
                        if (stateval == 0) return stateval;
                        if (stateval - prevstate < movetresh * discount2) return stateval;
                        //check value for swap
                        if (!swapped)
                        {
                            if (hold == 0) value = Math.Max(value, Search(matrix, depth, nexti + 1, next[nexti], piece, true, comb, b2b, trash, stateval));
                            else value = Math.Max(value, Search(matrix, depth, nexti, _hold, piece, true, comb, b2b, trash, stateval));
                            if (hashvals.ContainsKey(hash)) return hashvals[hash];
                        }
                        //check all landing spots
                        for (int rot = 0; rot < 4; rot++)
                        {
                            int left = pieceleft[piece][rot], right = pieceright[piece][rot];

                            for (int orix = -left; orix < 10 - right; orix++)
                            {
                                if (overtime) break;

                                int oriy = 18;
                                //hard drop
                                while (!OnGround(matrix, new int[] { orix, oriy }, piece, rot)) oriy++;
                                if (visualise)
                                {
                                    WriteAt(0, 1, ConsoleColor.White, "nodes: " + (float)count / piececount);
                                    WriteAt(0, 2, ConsoleColor.White, "trans: " + hashvals.Count);
                                    for (int x = 0; x < 10; x++) for (int y = 39; y > 19; y--) WriteAt(13 + x * 2, y - 18, piececolors[matrix[y][x]], "██");
                                    for (int i = 0; i < 4; i++) if (oriy + piecesy[piece][rot][i] > 19) WriteAt((orix + piecesx[piece][rot][i]) * 2 + 13, oriy + piecesy[piece][rot][i] - 18, piececolors[piece], "▒▒");
                                    timer.Restart();
                                    while (timer.ElapsedMilliseconds < visspeed) ;
                                }
                                //update matrix
                                int garbage;
                                int[] info = Score(matrix, b2b, new int[] { orix, oriy }, piece, rot, comb, false, out garbage);
                                //check if better
                                if (visualise) for (int x = 0; x < 10; x++) for (int y = 39; y > 19; y--) WriteAt(13 + x * 2, y - 18, piececolors[matrix[x][y]], "██");
                                if (!tsdonly || info[0] == 0 || (info.Length - 4 == 2 && info[2] == 3)) if (Check(matrix, piece, rot, orix, oriy, info)) value = Math.Max(value, Search(matrix, depth - 1, nexti + 1, next[nexti], _hold, false, info[3], (info[1] & 1) == 1, Math.FusedMultiplyAdd(discount, garbage, trash), stateval));
                                //revert matrix
                                Revert(matrix, piece, rot, new int[] { orix, oriy }, info);
                                //try spin clockwise
                                if (piece != 7)
                                {
                                    int x = orix, y = oriy;
                                    int test = RotateTest(matrix, new int[] { x, y }, piece, rot, true);
                                    for (int rotate = 1; test != -1 && rotate < 3; test = RotateTest(matrix, new int[] { x, y }, piece, rot + rotate, true), rotate++)
                                    {
                                        //apply kick
                                        if (piece == 2)
                                        {
                                            x += ikicksx[test] * xmul;
                                            y += ikicksy[test] * ymul;
                                        }
                                        else
                                        {
                                            x += kicksx[test] * xmul;
                                            y += kicksy[test] * ymul;
                                        }
                                        if (!OnGround(matrix, new int[] { x, y }, piece, rot + rotate)) break;
                                        if (visualise)
                                        {
                                            WriteAt(0, 1, ConsoleColor.White, "nodes: " + (float)count / piececount);
                                            WriteAt(0, 2, ConsoleColor.White, "trans: " + hashvals.Count);
                                            for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[matrix[j][i]], "██");
                                            for (int i = 0; i < 4; i++) if (y + piecesy[piece][rot][i] > 19) WriteAt((x + piecesx[piece][rot][i]) * 2 + 13, y + piecesy[piece][rot][i] - 18, piececolors[piece], "▒▒");
                                            timer.Restart();
                                            while (timer.ElapsedMilliseconds < visspeed) ;
                                        }
                                        //update matrix
                                        info = Score(matrix, b2b, new int[] { x, y }, piece, rot + rotate, comb, true, out garbage);
                                        if (visualise) for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[matrix[j][i]], "██");
                                        //check if better
                                        if (!tsdonly || info[0] == 0 || (info.Length - 4 == 2 && info[2] == 3)) if (Check(matrix, piece, (rot + rotate) % 4, x, y, info)) value = Math.Max(value, Search(matrix, depth - 1, nexti + 1, next[nexti], _hold, false, info[3], (info[1] & 1) == 1, Math.FusedMultiplyAdd(discount, garbage, trash), stateval));
                                        //Revert matrix
                                        Revert(matrix, piece, (rot + rotate) % 4, new int[] { x, y }, info);
                                        if (piece == 2 || piece == 3 || piece == 4) break;
                                    }
                                }
                                //try spin anti-clockwise
                                if (piece != 7)
                                {
                                    int x = orix, y = oriy;
                                    int test = RotateTest(matrix, new int[] { x, y }, piece, rot, false);

                                    for (int rotate = -1; test != -1 && rotate > -3; test = RotateTest(matrix, new int[] { x, y }, piece, (rot + rotate + 4) % 4, false), rotate--)
                                    {
                                        //apply kick
                                        if (piece == 2)
                                        {
                                            x += ikicksx[test] * xmul;
                                            y += ikicksy[test] * ymul;
                                        }
                                        else
                                        {
                                            x += kicksx[test] * xmul;
                                            y += kicksy[test] * ymul;
                                        }
                                        if (!OnGround(matrix, new int[] { x, y }, piece, rot + rotate + 4)) break;
                                        if (visualise)
                                        {
                                            WriteAt(0, 1, ConsoleColor.White, "nodes: " + (float)count / piececount);
                                            WriteAt(0, 2, ConsoleColor.White, "trans: " + hashvals.Count);
                                            for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[matrix[j][i]], "██");
                                            for (int i = 0; i < 4; i++) if (y + piecesy[piece][rot][i] > 19) WriteAt((x + piecesx[piece][rot][i]) * 2 + 13, y + piecesy[piece][rot][i] - 18, piececolors[piece], "▒▒");
                                            timer.Restart();
                                            while (timer.ElapsedMilliseconds < visspeed) ;
                                        }
                                        //update matrix
                                        info = Score(matrix, b2b, new int[] { x, y }, piece, rot + rotate + 4, comb, true, out garbage);
                                        if (visualise) for (int i = 0; i < 10; i++) for (int j = 39; j > 19; j--) WriteAt(13 + i * 2, j - 18, piececolors[matrix[j][i]], "██");
                                        //check if better
                                        if (!tsdonly || info[0] == 0 || (info.Length - 4 == 2 && info[2] == 3)) if (Check(matrix, piece, (rot + rotate + 4) % 4, x, y, info)) value = Math.Max(value, Search(matrix, depth - 1, nexti + 1, next[nexti], _hold, false, info[3], (info[1] & 1) == 1, Math.FusedMultiplyAdd(discount, garbage, trash), stateval));
                                        //revert matrix
                                        Revert(matrix, piece, (rot + rotate + 4) % 4, new int[] { x, y }, info);
                                        if (piece == 2 || piece == 3 || piece == 4) break;
                                    }
                                }
                            }
                            //o pieces can't be rotated
                            if (piece == 7) break;
                        }

                        hashvals.Add(hash, value);
                        return value;
                    }
                }

                void Revert(int[][] matrix, int piece, int rot, int[] pos, int[] info)
                {
                    //add cleared lines
                    if (info.Length != 4)
                    {
                        int moveup = info.Length - 4;
                        for (int y = 13; moveup != 0; y++)
                        {
                            if (y == info[info.Length - moveup])
                            {
                                moveup--;
                                matrix[y] = new int[10];
                                for (int x = 0; x < 10; x++) matrix[y][x] = 1;
                            }
                            else matrix[y] = matrix[y + moveup];
                        }
                    }
                    //remove piece
                    for (int i = 0; i < 4; i++) matrix[pos[1] + piecesy[piece][rot][i]][pos[0] + piecesx[piece][rot][i]] = 0;
                }

                List<double> ExtrFeat(int[][] matrix, double trash)
                {
                    Stopwatch t = new();
                    t.Start();

                    //find heightest block in each column
                    int[] heights = new int[10];
                    for (int x = 0; x < 10; x++)
                    {
                        int height = 23;
                        for (int y = 17; matrix[y][x] == 0; y++)
                        {
                            height--;
                            if (y == 39) break;
                        }
                        heights[x] = height;
                    }
                    //sum of heights squared
                    double h = 0;
                    foreach (int height in heights) h += height * height;
                    h /= 128;
                    //"caves"
                    double caves = 0;
                    for (int y = 39 - heights[0]; y < 40; y++) if (matrix[y][0] == 0) if (y < 39 - heights[1]) caves += heights[0] + y - 39;
                    for (int x = 1; x < 9; x++) for (int y = 39 - heights[x]; y < 40; y++) if (matrix[y][x] == 0) if (y <= Math.Min(39 - heights[x - 1], 39 - heights[x + 1])) caves += heights[x] + y - 39;
                    for (int y = 39 - heights[9]; y < 40; y++) if (matrix[y][9] == 0) if (y <= 39 - heights[8]) caves += heights[9] + y - 39;
                    caves /= 8;
                    //pillars
                    double pillars = 0;
                    for (int x = 0; x < 10; x++)
                    {
                        if (x == fillhole) continue;
                        float diff;
                        if (x != 0 && x != 9) diff = Math.Min(heights[x - 1], heights[x + 1]) - heights[x];
                        else diff = x == 0 ? heights[1] - heights[0] : heights[8] - heights[9];
                        if (diff > 2) pillars += diff * diff;
                    }
                    pillars /= 16;
                    //row trasitions
                    double rowtrans = 0;
                    for (int y = 19; y < 40; y++)
                    {
                        bool empty = matrix[y][0] == 0;
                        for (int x = 1; x < 10; x++)
                        {
                            if (empty ^ matrix[y][x] == 0)
                            {
                                rowtrans++;
                                empty = !empty;
                            }
                        }
                    }
                    rowtrans /= 8;
                    //column trasitions
                    double coltrans = 0;
                    for (int x = 0; x < 10; x++)
                    {
                        bool empty = matrix[19][x] == 0;
                        for (int y = 20; y < 40; y++)
                        {
                            if (empty ^ matrix[y][x] == 0)
                            {
                                coltrans++;
                                empty = !empty;
                            }
                        }
                    }
                    rowtrans /= 8;

                    t.Stop();
                    evaltime += t.ElapsedTicks;

                    return new List<double>() { Math.Sqrt(h), caves, pillars, rowtrans, coltrans, trash };
                }

                bool CanMove(ConsoleKey[] moves, int[][] matrix, int piece)
                {
                    int x = 4, y = 19, rot = 0;
                    double velo = G * (movereact - 1);
                    foreach (ConsoleKey move in moves)
                    {
                        switch (move)
                        {
                            case ConsoleKey.LeftArrow:
                                {
                                    for (int i = 0; i < 4; i++) if (matrix[y + piecesy[piece][rot][i]][x + piecesx[piece][rot][i] - 1] != 0) return false;
                                    x--;
                                    break;
                                }
                            case ConsoleKey.RightArrow:
                                {
                                    for (int i = 0; i < 4; i++) if (matrix[y + piecesy[piece][rot][i]][x + piecesx[piece][rot][i] + 1] != 0) return false;
                                    x++;
                                    break;
                                }
                            case ConsoleKey.Z:
                                {
                                    if (RotateTest(screen, new int[] { x, y }, piece, rot, false) != 0) return false;
                                    rot = 3;
                                    break;
                                }
                            case ConsoleKey.UpArrow:
                                {
                                    if (RotateTest(screen, new int[] { x, y }, piece, rot, true) != 0) return false;
                                    rot++;
                                    break;
                                }
                        }
                        //gravity
                        for (velo += G * movedelay; !OnGround(matrix, new int[] { x, y }, piece, rot) && velo > 0; velo--) y++;
                        if (OnGround(matrix, new int[] { x, y }, piece, rot)) velo = 0;
                    }

                    return true;
                }

                int[][] Clone(int[][] array)
                {
                    int[][] clone = new int[40][];
                    for (int i = 0; i < 40; i++)
                    {
                        clone[i] = new int[10];
                        Buffer.BlockCopy(array[i], 0, clone[i], 0, 40);
                    }

                    return clone;
                }

                long HashBoard(int[][] matrix, int piece, int hold, int nexti, int depth)
                {
                    long hash = piecehashtable[piece] ^ holdhashtable[hold];
                    for (int i = 0; nexti + i < next.Length && i < depth; i++) hash ^= nexthashtable[i][next[nexti + i]];
                    for (int x = 0; x < 10; x++) for (int y = 16; y < 40; y++) if (matrix[y][x] != 0) hash ^= hashtable[x][y];
                    return hash;
                }

                bool Check(int[][] matrix, int piece, int rot, int x, int y, int[] clears)
                {
                    Stopwatch t = new();
                    t.Start();

                    int[] heights = new int[10];
                    for (int x2 = 0; x2 < 10; x2++)
                    {
                        int y2 = 17;
                        for (; y2 < 40; y2++) if (matrix[y2][x2] != 0) break;
                        heights[x2] = y2;
                    }

                    //bool[][] visited = new bool[10][];
                    for (int i = 0; i < 4; i++)
                    {
                        //skip if x is same
                        bool skip = false;
                        //for (int j = 0; j < i; j++) if (piecesx[piece][rot][j] == piecesx[piece][rot][i]) skip = true;
                        //find proper y value
                        int oriy = y + piecesy[piece][rot][i];
                        for (int c = 4; c < clears.Length && !skip; c++)
                        {
                            //no need to check if the piece was cleared
                            if (clears[c] == oriy) skip = true;
                            else if (clears[c] > oriy) oriy--;
                        }
                        if (skip) continue;
                        //check for holes underneath
                        int orix = x + piecesx[piece][rot][i];
                        if (oriy != 39)
                        {
                            if (matrix[oriy + 1][orix] == 0)
                            {
                                //for (int j = 0; j < 10; j++) visited[j] = new bool[40];
                                //bool closedoff = ClosedOff(orix, oriy + 1, 2);
                                bool closedoff = false;
                                int left = orix, right = orix;
                                //left
                                if (orix != 0)
                                {
                                    if (matrix[oriy + 1][left - 1] == 0 && heights[left - 1] <= oriy)
                                    {
                                        left--;
                                        if (orix != 1)
                                        {
                                            if (matrix[oriy + 1][left - 1] == 0 && heights[left - 1] <= oriy)
                                            {
                                                left--;
                                                if (orix != 2) if (matrix[oriy + 1][left - 1] == 0 && heights[left - 1] <= oriy) left--;
                                            }
                                        }
                                    }
                                }
                                if (orix - left == 3)
                                {
                                    t.Stop();
                                    checktime += t.ElapsedTicks;
                                    return false;
                                }
                                //right
                                if (orix != 9)
                                {
                                    if (matrix[oriy + 1][right + 1] == 0 && heights[right + 1] <= oriy)
                                    {
                                        right++;
                                        if (orix != 8)
                                        {
                                            if (matrix[oriy + 1][right + 1] == 0 && heights[right + 1] <= oriy)
                                            {
                                                right++;
                                                if (orix != 7) if (matrix[oriy + 1][right + 1] == 0 && heights[right + 1] <= oriy) right++;
                                            }
                                        }
                                    }
                                }
                                closedoff = (heights[left] <= oriy && heights[right] <= oriy) || (right - left > 3);

                                if (closedoff)
                                {
                                    t.Stop();
                                    checktime += t.ElapsedTicks;
                                    return false;
                                }
                            }
                        }

                        //check for holes at the sides
                        //left
                        if (orix != 0)
                        {
                            if (matrix[oriy][orix - 1] == 0)
                            {
                                bool closedoff = false;
                                int left = orix - 1, right = orix - 1;
                                //left
                                if (orix != 1)
                                {
                                    if (matrix[oriy][left - 1] == 0 && heights[left - 1] < oriy)
                                    {
                                        left--;
                                        if (orix != 2)
                                        {
                                            if (matrix[oriy][left - 1] == 0 && heights[left - 1] < oriy)
                                            {
                                                left--;
                                                if (orix != 3) if (matrix[oriy][left - 1] == 0 && heights[left - 1] < oriy) left--;
                                            }
                                        }
                                    }
                                }
                                closedoff = (heights[left] <= oriy && heights[right] <= oriy) || (right - left > 3);

                                if (closedoff)
                                {
                                    t.Stop();
                                    checktime += t.ElapsedTicks;
                                    return false;
                                }
                            }
                        }
                        //right
                        if (orix != 9)
                        {
                            if (matrix[oriy][orix + 1] == 0)
                            {
                                bool closedoff = false;
                                int left = orix + 1, right = orix + 1;
                                //right
                                if (orix != 8)
                                {
                                    if (matrix[oriy][right + 1] == 0 && heights[right + 1] < oriy)
                                    {
                                        right++;
                                        if (orix != 7)
                                        {
                                            if (matrix[oriy][right + 1] == 0 && heights[right + 1] < oriy)
                                            {
                                                right++;
                                                if (orix != 6) if (matrix[oriy][right + 1] == 0 && heights[right + 1] < oriy) right++;
                                            }
                                        }
                                    }
                                }
                                closedoff = (heights[left] <= oriy && heights[right] <= oriy) || (right - left > 3);

                                if (closedoff)
                                {
                                    t.Stop();
                                    checktime += t.ElapsedTicks;
                                    return false;
                                }
                            }
                        }
                    }

                    t.Stop();
                    checktime += t.ElapsedTicks;
                    return true;

                    /*bool ClosedOff(int x, int y, int depth)
                    {
                        if (visited[x][y]) return true;

                        visited[x][y] = true;
                        if (depth == 0) return matrix[y][x] != 0;
                        if (matrix[y][x] != 0) return true;

                        if (x != 0) if (!ClosedOff(x - 1, y, depth - 1)) return false;
                        if (x != 9) if (!ClosedOff(x + 1, y, depth - 1)) return false;
                        if (y != 0) if (!ClosedOff(x, y - 1, depth - 1)) return false;
                        //if (y != 39) if (!ClosedOff(x, y + 1, depth - 1)) return false;

                        return true;
                    }
                }

                int subhole(int subtract)
                {
                    holecool -= subtract;
                    if (holecool < 0)
                    {
                        hole = rand.Next(10);
                        holecool = 5 + rand.Next(5);
                    }
                    return hole;
                }
            }
        }

        class NN
        {
            public int inputs, outputs;
            public double fitness = 0;
            public List<Node> nodes = new();
            public List<int> connectionids = new();
            public Dictionary<int, Connection> connections = new();

            public NN(int _inputs, int _outputs, List<Connection> _connections)
            {
                inputs = _inputs + 1;
                outputs = _outputs;
                foreach (Connection c in _connections)
                {
                    //add connection to connection tracking lists
                    Connection newc = c.Clone();
                    connectionids.Add(newc.id);
                    connections.Add(newc.id, newc);
                    //add nodes as nescessary
                    while (nodes.Count <= newc.input || nodes.Count <= newc.output) nodes.Add(new(nodes, nodes.Count));
                    //add connection to coresponding nodes
                    nodes[c.input].outputs.Add(newc);
                    nodes[c.output].inputs.Add(newc);
                }

            }

            public List<double> FeedFoward(List<double> input)
            {
                Stopwatch t = new();
                t.Start();

                //bias node
                input.Add(1);
                //set input nodes
                for (int i = 0; i < inputs; i++) nodes[i].value = nodes[i].UpdateValue() + input[i];
                //update all nodes (except output)
                for (int i = inputs + outputs; i < nodes.Count; i++) nodes[i].UpdateValue();
                //update ouput nodes and get output
                List<double> output = new();
                for (int i = inputs; i < inputs + outputs; i++) output.Add(nodes[i].UpdateValue());

                t.Stop();
                evaltime += t.ElapsedTicks;

                return output;
            }
        }

        class Node
        {
            public List<Node> network;
            public double value = 0;
            public int id;
            public List<Connection> inputs = new(), outputs = new();

            public Node(List<Node> _network, int _id, List<Connection> _inputs = null, List<Connection> _outputs = null)
            {
                network = _network;
                id = _id;
                if (_inputs != null) inputs = _inputs;
                if (_outputs != null) outputs = _outputs;
            }

            public double UpdateValue()
            {
                //sum activations * weights
                value = 0;
                foreach (Connection c in inputs) if (c.enabled) value = Math.FusedMultiplyAdd(network[c.input].value, c.weight, value);
                //ReLU
                value = Math.Max(0, value);
                return value;
            }
        }

        class Connection
        {
            public bool enabled = true;
            public int input, output, id;
            public double weight;

            public Connection(int _input, int _output, double _weight)
            {
                input = _input;
                output = _output;
                weight = _weight;
                int checkid = Exists(input, output);
                if (checkid == -1)
                {
                    id = innodes.Count;
                    innodes.Add(input);
                    outnodes.Add(output);
                }
                else id = checkid;
            }

            public Connection Clone()
            {
                Connection clone = new(input, output, weight);
                if (!enabled) clone.enabled = false;
                return clone;
            }
        }

        static int Exists(int input, int output)
        {
            for (int i = 0; i < innodes.Count; i++) if (innodes[i] == input && outnodes[i] == output) return i;
            return -1;
        }
        static NN[] LoadNNs(string path)
        {
            List<string> lines = new(File.ReadAllLines(path, Encoding.UTF8));
            List<List<string>> pars = new();
            int oldi = 1;
            //split up NNs
            for (int i = 2; i < lines.Count; i++)
            {
                if (lines[i].Length == 0)
                {
                    pars.Add(lines.GetRange(oldi, i - oldi));
                    i += 2;
                    oldi = i;
                }
            }

            //create NNS
            NN[] NNs = new NN[pars.Count];
            for (int i = 0; i < NNs.Length; i++)
            {
                //no. of inputs and outputs
                string[] inout = pars[i][0].Split(' ');
                //connections
                List<Connection> cons = new();
                for (int j = 1; j < pars[i].Count; j++)
                {
                    string[] con = pars[i][j].Split(' ');
                    Connection newcon = new(Convert.ToInt32(con[0]), Convert.ToInt32(con[1]), Convert.ToDouble(con[2]));
                    if (con.Length == 4) newcon.enabled = false;
                    cons.Add(newcon);
                }
                //create NN

                NNs[i] = new(Convert.ToInt32(inout[0]), Convert.ToInt32(inout[1]), cons);
            }
            return NNs;
        }
        static void SetWindow(int wwidth, int wheight, int bwidth, int bheight)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Clear();
            Console.WindowHeight = wheight;
            Console.WindowWidth = wwidth;
            Console.BufferHeight = bheight;
            Console.BufferWidth = bwidth;
        }
    }
}
*/