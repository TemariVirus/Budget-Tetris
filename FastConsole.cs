namespace FastConsole;

using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Input;

static class FConsole
{
    static SafeFileHandle ConoutHandle;

    private static Stopwatch Time = Stopwatch.StartNew();
    public static Thread RenderThread { get; private set; }
    public static Thread InputThread { get; private set; }
    static Action RenderCallback;
    static Action InputLoopCallback;

    // Throws an exception if it failed to grab the CONOUT$ file handle
    // Otherwise, starts the console update loop on a separate thread
    // The callback is called at the end of each frame
    public static void Initialise(Action renderCallback = null, Action inputCallback = null)
    {
        ConoutHandle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

        if (ConoutHandle.IsInvalid) throw new System.ComponentModel.Win32Exception();
        else
        {
            Width = (short)Console.BufferWidth;
            Height = (short)Console.BufferHeight;
            Console.CursorVisible = CursorVisible;
            ConsoleBuffer = new int[Width * Height];
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            RenderThread = new Thread(RenderLoop);
            RenderCallback = renderCallback;
            RenderThread.Start();

            InputThread = new Thread(InputLoop);
            InputThread.SetApartmentState(ApartmentState.STA);
            InputLoopCallback = inputCallback;
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
    private static int Width, Height;
    private static int XOffset, YOffset;
    private static bool _CursorVisible = true;
    public static bool CursorVisible
    {
        get => _CursorVisible;
        set
        {
            if (_CursorVisible != value)
            {
                _CursorVisible = value;
                Console.CursorVisible = value;
            }
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
        Queue<long> times = new Queue<long>();
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
            }
            ForceRender();

            // Write fps
            long newT = Time.ElapsedTicks;
            times.Enqueue(newT);
            long oldT;
            if (times.Count > Framerate) oldT = times.Dequeue();
            else oldT = times.Peek();
            WriteAt($"{(float)(times.Count - 1) / (newT - oldT) * Stopwatch.Frequency}fps".PadRight(12), 0, 0);

            // Callback
            RenderCallback?.Invoke();

            // Wait until next render
            Thread.Sleep((int)Math.Max(0, 0.9 * (1000D / Framerate) - (1000D * (Time.ElapsedTicks - prevT) / Stopwatch.Frequency)));
            //while (Time.ElapsedTicks - prevT < Stopwatch.Frequency / Framerate * 0.95) Thread.Sleep(0);
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

    public static void WriteLine(string text)
    {
        WriteAt(text, CursorLeft, CursorTop);
        CursorLeft = 0;
        CursorTop = Math.Min(CursorTop + 1, Width - 1);
    }

    public static void WriteAt(string text, int x, int y, ConsoleColor foreground = ConsoleColor.White, ConsoleColor background = ConsoleColor.Black)
    {
        int index = y * Width + x;
        for (int i = 0; i < text.Length && index < Height * Width; i++, index++)
        {
            // might need to do checks for tab, return and newline?
            ConsoleBuffer[index] = text[i] | ((int)foreground << 16) | ((int)background << 20);
        }
        int cursor_pos = Math.Min(index, ConsoleBuffer.Length - 1);
        CursorLeft = cursor_pos % Width;
        CursorTop = cursor_pos / Width;
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

                if (isPressed == OnPress)
                {
                    if (Delay <= 0)
                        Action?.Invoke();
                    if (Delay == 0)
                        LastStateChangeTime += RepeatDelay; // - Delay, but Delay == 0
                }
            }
            else if (Delay >= 0)
            {
                if (isPressed == OnPress)
                {
                    if (currentTime - LastStateChangeTime > Delay)
                    {
                        Action?.Invoke();
                        LastStateChangeTime = currentTime + RepeatDelay - Delay;
                        if (RepeatDelay < 0) LastStateChangeTime = long.MinValue;
                    }
                }
            }
        }
    }

    private static readonly List<KeyListener> KeyListeners = new List<KeyListener>();

    private static void InputLoop()
    {
        Queue<long> times = new Queue<long>();
        while (true)
        {
            long time = Time.ElapsedTicks;
            // Remove listeners that are to be removed
            for (int i = 0; i < KeyListeners.Count; i++)
                if (KeyListeners[i] != null)
                    if (KeyListeners[i].Remove)
                        KeyListeners.RemoveAt(i--);
            // Check for key events
            for (int i = 0; i < KeyListeners.Count; i++)
                KeyListeners[i]?.Check();

            // Callback function
            InputLoopCallback?.Invoke();

            // Try to get ~2000 loops per second, since the smallest precision is a millisecond
            while (Time.ElapsedTicks - time < Stopwatch.Frequency / 2400) Thread.Sleep(0);
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
    #endregion
}