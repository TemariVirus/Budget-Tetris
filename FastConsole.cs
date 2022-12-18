using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace FastConsole
{
    static class FConsole
    {
        static SafeFileHandle ConoutHandle;

        private static Stopwatch Time = Stopwatch.StartNew();
        public static Thread RenderThread { get; private set; }
        public static Thread InputThread { get; private set; }
        static Action RenderCallback;
        static Action InputLoopCallback;

        public static Thread SwitchFocusThread { get; private set; }

        // Throws an exception if it failed to grab the CONOUT$ file handle
        // Otherwise, starts the console update loop on a separate thread
        // The callback is called at the end of each frame
        public static void Initialise(Action renderCallback = null, Action inputCallback = null)
        {
            ConoutHandle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

            if (ConoutHandle.IsInvalid) throw new System.ComponentModel.Win32Exception();
            else
            {
                // Rendering
                Width = (short)Console.BufferWidth;
                Height = (short)Console.BufferHeight;
                Console.CursorVisible = CursorVisible;
                ConsoleBuffer = new int[Width * Height];
                Console.OutputEncoding = Encoding.Unicode;
                RenderThread = new Thread(RenderLoop);
                RenderCallback = renderCallback;
                RenderThread.Priority = ThreadPriority.Lowest;
                RenderThread.Start();

                // Check for focus switching
                SwitchFocusThread = new Thread(() =>
                {
                    SwitchFocusDelegate = new WinEventDelegate((_, _1, _2, _3, _4, _5, _6) => IsFocused = WindowIsFocused());
                    IntPtr m_hhook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, SwitchFocusDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
                    Dispatcher.Run();
                });
                SwitchFocusThread.Priority = ThreadPriority.Lowest;
                SwitchFocusThread.Start();
                IsFocused = WindowIsFocused();

                // Input
                InputThread = new Thread(InputLoop);
                InputThread.SetApartmentState(ApartmentState.STA);
                InputLoopCallback = inputCallback;
                InputThread.Priority = ThreadPriority.Lowest;
                InputThread.Start();
            }
        }

        #region // Font
        private const int FixedWidthTrueType = 54;
        private const int StandardOutputHandle = -11;

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool SetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool GetCurrentConsoleFontEx(IntPtr hConsoleOutput, bool MaximumWindow, ref FontInfo ConsoleCurrentFontEx);

        private static readonly IntPtr ConsoleOutputHandle = GetStdHandle(StandardOutputHandle);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct FontInfo
        {
            internal int cbSize;
            internal int FontIndex;
            internal short FontWidth;
            public short FontSize;
            public int FontFamily;
            public int FontWeight;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.wc, SizeConst = 32)]
            public string FontName;
        }

        public static FontInfo[] SetFont(string font, short fontSize = 0)
        {
            FontInfo before = new FontInfo
            {
                cbSize = Marshal.SizeOf<FontInfo>()
            };

            if (GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref before))
            {

                FontInfo set = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>(),
                    FontIndex = 0,
                    FontFamily = FixedWidthTrueType,
                    FontName = font,
                    FontWeight = 400,
                    FontSize = fontSize > 0 ? fontSize : before.FontSize
                };

                // Get some settings from current font.
                if (!SetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref set))
                {
                    var ex = Marshal.GetLastWin32Error();
                    Console.WriteLine("Error setting font: " + ex);
                    throw new System.ComponentModel.Win32Exception(ex);
                }

                FontInfo after = new FontInfo
                {
                    cbSize = Marshal.SizeOf<FontInfo>()
                };
                GetCurrentConsoleFontEx(ConsoleOutputHandle, false, ref after);

                return new[] { before, set, after };
            }
            else
            {
                var er = Marshal.GetLastWin32Error();
                Console.WriteLine("Get error " + er);
                throw new System.ComponentModel.Win32Exception(er);
            }
        }
        #endregion

        #region // Rendering
        public static int Width { get; private set; }
        public static int Height { get; private set; }
        private static int XOffset, YOffset;
        private static bool _CursorVisible = true;
        public static bool CursorVisible
        {
            get => _CursorVisible;
            set
            {
                _CursorVisible = value;
                Console.CursorVisible = value;
            }
        }
        public static int CursorLeft = 0, CursorTop = 0;
        public static double Framerate = 30;

        static int[] ConsoleBuffer;

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern SafeFileHandle CreateFile(
            string fileName,
            [MarshalAs(UnmanagedType.U4)] uint fileAccess,
            [MarshalAs(UnmanagedType.U4)] uint fileShare,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] int flags,
            IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteConsoleOutputW(
            SafeFileHandle hConsoleOutput,
            int[] lpBuffer,
            Coord dwBufferSize,
            Coord dwBufferCoord,
            ref Rect lpWriteRegion);

        [StructLayout(LayoutKind.Sequential)]
        struct Coord
        {
            public static readonly Coord Zero = new Coord(0, 0);
            public short X;
            public short Y;

            public Coord(int _x, int _y)
            {
                X = (short)_x;
                Y = (short)_y;
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        struct Rect
        {
            public readonly short Left;
            public readonly short Top;
            public readonly short Right;
            public readonly short Bottom;

            public Rect(int left, int top, int right, int height)
            {
                Left = (short)left;
                Top = (short)top;
                Right = (short)right;
                Bottom = (short)height;
            }
        }

        private static void RenderLoop()
        {
            Queue<long> draw_times = new Queue<long>(16);
            long prevT = 0;
            while (true)
            {
                // Handle window being resized
                try
                {
                    Console.BufferWidth = Math.Max(Width, Console.WindowWidth);
                    Console.BufferHeight = Math.Max(Height, Console.WindowHeight);
                }
                catch { }
                int old_xoffset = XOffset, old_yoffset = YOffset;
                XOffset = (Console.WindowWidth - Width) / 2;
                YOffset = (Console.WindowHeight - Height) / 2;
                if (old_xoffset != XOffset || old_yoffset != YOffset)
                {
                    // Fill the whole buffer with black
                    int width = Console.BufferWidth, height = Console.BufferHeight;
                    int[] the_void = new int[width * height];
                    Rect void_rect = new Rect(0, 0, width, height);
                    WriteConsoleOutputW(ConoutHandle, the_void, new Coord((short)width, (short)height), Coord.Zero, ref void_rect);
                    // Reset cursor visibility
                    Console.CursorVisible = CursorVisible;
                }

                // Write fps
                long newT = Time.ElapsedTicks;
                draw_times.Enqueue(newT);
                long oldT;
                if (draw_times.Count > 16) oldT = draw_times.Dequeue();
                else oldT = draw_times.Peek();
                double fps = Math.Round((double)draw_times.Count / (newT - oldT) * Stopwatch.Frequency, 2);
                WriteAt($"{fps}{(fps == (int)fps ? "." : "")}".PadRight(5, '0') + "fps", 1, 0);
                ForceRender();

                // Callback
                RenderCallback?.Invoke();

                // Wait until next render
                while (Time.ElapsedTicks - prevT < Stopwatch.Frequency / Framerate) Thread.Sleep(0);
                prevT = Time.ElapsedTicks;
            }
        }

        public static void SetRenderCallback(Action callback)
        {
            RenderCallback = callback;
        }

        public static void SetWindow(int width, int height)
        {
            Console.WindowWidth = width;
            Console.WindowHeight = height;
        }

        public static void SetBuffer(int width, int height)
        {
            if (width != Width) Console.BufferWidth = width;
            if (height != Height) Console.BufferHeight = height;
            if (width != Width || height != Height) ResizeBuffer(width, height);
        }

        public static void Set(int width, int height) => Set(width, height, width, height);

        public static void Set(int window_width, int window_height, int buffer_width, int buffer_height)
        {
            try
            {
                if (window_width <= Console.BufferWidth) Console.WindowWidth = window_width;
            }
            catch { }
            if (buffer_width != Width) Console.BufferWidth = buffer_width;
            try
            {
                Console.WindowWidth = window_width;
            }
            catch { }
            try
            {
                if (window_height <= Console.BufferHeight) Console.WindowHeight = window_height;
            }
            catch { }
            if (buffer_height != Height) Console.BufferHeight = buffer_height;
            try
            {
                Console.WindowHeight = window_height;
            }
            catch { }

            if (buffer_width != Width || buffer_height != Height)
                ResizeBuffer(buffer_width, buffer_height);
        }

        static void ResizeBuffer(int width, int height)
        {
            int old_width = Width, old_height = Height;
            Width = (short)width;
            Height = (short)height;
            // Create a new screen buffer and copy characters from the old one
            int[] new_buff = new int[Width * Height];
            int min_height = Math.Min(Height, old_height), min_width = Math.Min(Width, old_width);
            for (int i = 0; i < min_height; i++)
                Buffer.BlockCopy(ConsoleBuffer, sizeof(int) * i * old_width, new_buff, sizeof(int) * i * Width, sizeof(int) * min_width);
            ConsoleBuffer = new_buff;
        }

        public static void Write(string text)
        {
            WriteAt(text, CursorLeft, CursorTop);
        }

        public static void Write(object obj)
        {
            WriteAt(obj.ToString(), CursorLeft, CursorTop);
        }

        public static void WriteLine()
        {
            Write("\n");
        }

        public static void WriteLine(string text)
        {
            Write(text + '\n');
        }

        public static void WriteLine(object obj)
        {
            Write(obj.ToString() + '\n');
        }

        public static void WriteAt(string text, int x, int y, ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black)
        {
            int pos = y * Width + x;
            for (int i = 0; i < text.Length && pos < Height * Width; i++, pos++)
            {
                // might need to do checks for tab, return and newline?
                if (text[i] == '\t')
                {
                    int space_end = pos + 8 - (pos % Width % 8);
                    for (; pos < space_end && pos < Height * Width; pos++)
                        ConsoleBuffer[pos] = ' ' | ((int)foreground << 16) | ((int)background << 20);
                }
                else if (text[i] == '\r')
                {
                    pos -= pos % Width;
                }
                else if (text[i] == '\n')
                {
                    pos -= pos % Width;
                    pos += Width - 1;
                }
                else
                {
                    ConsoleBuffer[pos] = text[i] | ((int)foreground << 16) | ((int)background << 20);
                }
            }
            int cursor_pos = Math.Min(pos, ConsoleBuffer.Length - 1);
            CursorLeft = cursor_pos % Width;
            CursorTop = cursor_pos / Width;
        }

        public static void WriteAt(object obj, int x, int y, ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black)
        {
            WriteAt(obj.ToString(), x, y, foreground, background);
        }

        public static void Clear()
        {
            ConsoleBuffer = new int[Width * Height];
        }

        public static void ForceRender()
        {
            Rect rect = new Rect(XOffset, YOffset, XOffset + Width, YOffset + Height);
            WriteConsoleOutputW(ConoutHandle, ConsoleBuffer, new Coord(Width, Height), Coord.Zero, ref rect);
        }
        #endregion

        #region // User Input
        private class KeyListener
        {
            public readonly Key Key;
            public readonly bool OnPress;
            public readonly int Delay, RepeatDelay;
            public readonly Action Action;

            public bool LastIsPressed;
            public long LastStateChangeTime;

            public bool Remove = false;

            public KeyListener(Key key, bool onPress, int delayInMilliseconds, int repeatDelayInMilliseconds, Action action)
            {
                Key = key;
                OnPress = onPress;
                Delay = delayInMilliseconds;
                RepeatDelay = repeatDelayInMilliseconds;
                Action = action;
                Thread getIsDown = new Thread(() => LastIsPressed = Keyboard.IsKeyDown(key));
                getIsDown.SetApartmentState(ApartmentState.STA);
                getIsDown.Start();
                getIsDown.Join();
                LastStateChangeTime = Time.ElapsedMilliseconds;
            }

            public void Check()
            {
                bool isPressed = Keyboard.IsKeyDown(Key);
                long currentTime = Time.ElapsedMilliseconds;

                // If key changed state
                if (LastIsPressed != isPressed)
                {
                    LastIsPressed = isPressed;
                    LastStateChangeTime = currentTime;

                    if (isPressed != OnPress) return;

                    if (Delay <= 0)
                        Action?.Invoke();
                    if (Delay == 0)
                        LastStateChangeTime += RepeatDelay; // - Delay, but Delay == 0
                }
                else if (Delay >= 0)
                {
                    if (isPressed != OnPress) return;
                    if (currentTime - LastStateChangeTime <= Delay) return;

                    Action?.Invoke();
                    LastStateChangeTime = currentTime + RepeatDelay - Delay;
                    if (RepeatDelay < 0) LastStateChangeTime = long.MinValue;
                }
            }
        }

        private static readonly List<KeyListener> KeyListeners = new List<KeyListener>();

        private static bool IsFocused;

        private static WinEventDelegate SwitchFocusDelegate;
        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_SYSTEM_FOREGROUND = 3;

        [DllImport("user32.dll")]
        static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static void InputLoop()
        {
            while (true)
            {
                long time = Time.ElapsedTicks;
                // Remove listeners that are to be removed
                for (int i = 0; i < KeyListeners.Count; i++)
                    if (KeyListeners[i] != null)
                        if (KeyListeners[i].Remove)
                            KeyListeners.RemoveAt(i--);

                if (IsFocused)
                {
                    // Check for key events
                    for (int i = 0; i < KeyListeners.Count; i++)
                        KeyListeners[i]?.Check();

                    // Callback function
                    InputLoopCallback?.Invoke();
                }

                // Try to get ~2000 loops per second, since the smallest precision is a millisecond
                while (Time.ElapsedTicks - time < Stopwatch.Frequency / 2100) Thread.Sleep(0);
            }
        }

        public static void SetInputLoopCallback(Action callback)
        {
            InputLoopCallback = callback;
        }

        private static void AddListener(Key key, bool onPress, int delayInMilliseconds, int repeatDelayInMilliseconds, Action action) =>
            KeyListeners.Add(new KeyListener(key, onPress, delayInMilliseconds, repeatDelayInMilliseconds, action));

        public static void AddOnPressListener(Key key, Action action) =>
            AddListener(key, true, -1, 0, action);

        public static void AddOnReleaseListener(Key key, Action action) =>
            AddListener(key, false, -1, 0, action);

        public static void AddOnHoldListener(Key key, Action action, int delayInMilliseconds, int repeatDelayInMilliseconds) =>
            AddListener(key, true, delayInMilliseconds, repeatDelayInMilliseconds, action);

        private static void RemoveListeners(Key key, bool onPress, bool onHold)
        {
            for (int i = 0; i < KeyListeners.Count; i++)
            {
                if (KeyListeners[i].Remove) continue;

                if (KeyListeners[i].Key == key && KeyListeners[i].OnPress == onPress)
                {
                    //if (onHold)
                    //{
                    //    if (KeyListeners[i].Delay >= 0)
                    //        KeyListeners[i].Remove = true;
                    //}
                    //else if (KeyListeners[i].Delay < 0)
                    //    KeyListeners[i].Remove = true;

                    // Same as above
                    KeyListeners[i].Remove = onHold ^ (KeyListeners[i].Delay < 0);
                }
            }
        }

        public static void RemoveAllListeners()
        {
            for (int i = 0; i < KeyListeners.Count; i++)
                KeyListeners[i].Remove = true;
        }

        public static void RemoveAllListeners(Key key)
        {
            for (int i = 0; i < KeyListeners.Count; i++)
                if (KeyListeners[i].Key == key)
                    KeyListeners[i].Remove = true;
        }

        public static void RemoveOnPressListeners(Key key) =>
            RemoveListeners(key, true, false);

        public static void RemoveOnReleaseListeners(Key key) =>
            RemoveListeners(key, false, false);

        public static void RemoveOnHoldListeners(Key key) =>
            RemoveListeners(key, true, true);

        public static bool WindowIsFocused()
        {
            IntPtr handle = GetForegroundWindow();
            StringBuilder sb = new StringBuilder(256);

            if (GetWindowText(handle, sb, 256) > 0)
                return sb.ToString() == Console.Title;

            return false;
        }
        #endregion
    }
}
