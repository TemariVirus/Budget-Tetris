//! Provides a canvas-like interface for drawing to the terminal.

const std = @import("std");
const kernel32 = windows.kernel32;
const system = std.os.system;
const terminal = @This();
const time = std.time;
const unicode = std.unicode;
const windows = std.os.windows;

const Allocator = std.mem.Allocator;
const ByteList = std.ArrayListUnmanaged(u8);
const File = std.fs.File;
const SIG = std.os.SIG;
const Sigaction = std.os.Sigaction;

const assert = std.debug.assert;
const sigaction = std.os.sigaction;

const is_windows = @import("builtin").os.tag == .windows;
const ESC = "\x1B";
const ST = ESC ++ "\\";
const CSI = ESC ++ "[";
const OSC = ESC ++ "]";

var initialised = false;

var _allocator: std.mem.Allocator = undefined;
var stdout: File = undefined;
var draw_buffer: ByteList = undefined;

var frame_pool: FramePool = undefined;
var last: ?Frame = null;
var current: Frame = undefined;

var canvas_size: Size = undefined;
var terminal_size: Size = undefined;

var init_time: i128 = undefined;
var frames_drawn: usize = undefined;

pub const Color = enum(u8) {
    Black = 30,
    Red,
    Green,
    Yellow,
    Blue,
    Magenta,
    Cyan,
    White,
    BrightBlack = 90,
    BrightRed,
    BrightGreen,
    BrightYellow,
    BrightBlue,
    BrightMagenta,
    BrightCyan,
    BrightWhite,
};

const Pixel = struct {
    fg: Color,
    bg: Color,
    char: u21,

    pub fn eql(self: Pixel, other: Pixel) bool {
        return self.fg == other.fg and
            self.bg == other.bg and
            self.char == other.char;
    }
};

const Size = struct {
    width: u16,
    height: u16,

    pub fn eql(self: Size, other: Size) bool {
        return self.width == other.width and self.height == other.height;
    }

    pub fn area(self: Size) u32 {
        return @as(u32, self.width) * @as(u32, self.height);
    }

    pub fn bound(self: Size, other: Size) Size {
        return .{
            .width = @min(self.width, other.width),
            .height = @min(self.height, other.height),
        };
    }
};

const Frame = struct {
    size: Size,
    pixels: []Pixel,

    fn init(allocator: std.mem.Allocator, size: Size) !Frame {
        const pixels = try allocator.alloc(Pixel, size.area());
        return Frame{ .size = size, .pixels = pixels };
    }

    fn deinit(self: Frame, allocator: std.mem.Allocator) void {
        allocator.free(self.pixels);
    }

    fn inBounds(self: Frame, x: u16, y: u16) bool {
        return x < self.size.width and y < self.size.height;
    }

    fn get(self: Frame, x: u16, y: u16) Pixel {
        assert(self.inBounds(x, y));
        const index = @as(usize, y) * self.size.width + x;
        return self.pixels[index];
    }

    fn set(self: Frame, x: u16, y: u16, p: Pixel) void {
        assert(self.inBounds(x, y));
        const index = @as(usize, y) * self.size.width + x;
        self.pixels[index] = p;
    }
};

const FramePool = struct {
    frames: []Frame,
    used: []bool,

    fn init(
        allocator: std.mem.Allocator,
        size: Size,
        capacity: u32,
    ) !FramePool {
        const frames = try allocator.alloc(Frame, capacity);
        for (0..capacity) |i| {
            frames[i] = try Frame.init(allocator, size);
        }

        const is_used = try allocator.alloc(bool, capacity);
        for (0..capacity) |i| {
            is_used[i] = false;
        }

        return FramePool{ .frames = frames, .used = is_used };
    }

    fn deinit(self: FramePool, allocator: std.mem.Allocator) void {
        for (0..self.frames.len) |i| {
            self.frames[i].deinit(allocator);
        }

        allocator.free(self.frames);
        allocator.free(self.used);
    }

    /// Allocates a frame of the given size. Returns null if no frames are
    /// free, or if the requested size is larger than the size specified at
    /// initialisation. The returned frame is filled with spaces, with black
    /// foreground and background color.
    fn alloc(self: FramePool, size: Size) ?Frame {
        const len = size.area();
        if (len > self.frames[0].size.area()) {
            return null;
        }

        const frame = blk: for (0..self.used.len) |i| {
            if (!self.used[i]) {
                self.used[i] = true;
                break :blk self.frames[i];
            }
        } else return null;

        for (0..len) |i| {
            frame.pixels[i] = .{
                .fg = Color.Black,
                .bg = Color.Black,
                .char = ' ',
            };
        }
        return Frame{ .size = size, .pixels = frame.pixels[0..len] };
    }

    fn free(self: FramePool, frame: Frame) void {
        for (0..self.frames.len) |i| {
            // Use their pixel arrays to identify frames
            if (self.frames[i].pixels.ptr == frame.pixels.ptr) {
                self.used[i] = false;
                return;
            }
        }
    }
};

pub const View = struct {
    left: u16,
    top: u16,
    width: u16,
    height: u16,

    pub fn init(left: u16, top: u16, width: u16, height: u16) View {
        assert(left + width <= canvas_size.width and
            top + height <= canvas_size.height);
        return View{
            .left = left,
            .top = top,
            .width = width,
            .height = height,
        };
    }

    pub fn drawPixel(self: View, x: u16, y: u16, fg: Color, bg: Color, char: u21) void {
        assert(x < self.width and y < self.height);
        terminal.drawPixel(x + self.left, y + self.top, fg, bg, char);
    }
};

const signal = if (is_windows)
    struct {
        extern "C" fn signal(
            sig: c_int,
            func: *const fn (c_int, c_int) callconv(windows.WINAPI) void,
        ) callconv(.C) *anyopaque;
    }.signal
else
    void;

const setConsoleMode = if (is_windows)
    struct {
        extern "kernel32" fn SetConsoleMode(
            console: windows.HANDLE,
            mode: windows.DWORD,
        ) callconv(windows.WINAPI) windows.BOOL;
    }.SetConsoleMode
else
    void;

pub const InitError = error{
    AlreadyInitialised,
    FailedToSetConsoleOutputCP,
    FailedToSetConsoleMode,
};
pub fn init(allocator: Allocator, width: u16, height: u16) !void {
    if (initialised) {
        return InitError.AlreadyInitialised;
    }

    _allocator = allocator;
    stdout = std.io.getStdOut();

    if (is_windows) {
        _ = signal(SIG.INT, handleExitWindows);
    } else {
        const action = std.os.Sigaction{
            .handler = .{ .handler = handleExit },
            .mask = std.os.empty_sigset,
            .flags = 0,
        };
        std.os.sigaction(SIG.INT, &action, null) catch unreachable;
    }

    if (is_windows) {
        const CP_UTF8 = 65001;
        const result = kernel32.SetConsoleOutputCP(CP_UTF8);
        if (system.getErrno(result) != .SUCCESS) {
            return InitError.FailedToSetConsoleOutputCP;
        }

        const ENABLE_PROCESSED_OUTPUT = 0x1;
        const ENABLE_WRAP_AT_EOL_OUTPUT = 0x2;
        const ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x4;
        const DISABLE_NEWLINE_AUTO_RETURN = 0x8;
        const ENABLE_LVB_GRID_WORLDWIDE = 0x10;
        const result2 = setConsoleMode(
            stdout.handle,
            ENABLE_PROCESSED_OUTPUT |
                ENABLE_WRAP_AT_EOL_OUTPUT |
                ENABLE_VIRTUAL_TERMINAL_PROCESSING |
                DISABLE_NEWLINE_AUTO_RETURN |
                ENABLE_LVB_GRID_WORLDWIDE,
        );
        if (system.getErrno(result2) != .SUCCESS) {
            return InitError.FailedToSetConsoleMode;
        }
    }

    canvas_size = Size{ .width = width, .height = height };
    // Use a guess if we can't get the terminal size
    terminal_size = getTerminalSize() orelse Size{ .width = 120, .height = 30 };
    draw_buffer = ByteList{};
    // The actual frame buffers can never be larger than canvas_size,
    // so allocate buffers of that size
    frame_pool = try FramePool.init(_allocator, canvas_size, 2);

    const current_size = canvas_size.bound(terminal_size);
    current = frame_pool.alloc(current_size).?;

    useAlternateBuffer();
    hideCursor(stdout.writer()) catch {};

    init_time = time.nanoTimestamp();
    frames_drawn = 0;
    initialised = true;
}

pub fn deinit() void {
    if (!initialised) {
        return;
    }
    initialised = false;

    useMainBuffer();
    showCursor(stdout.writer()) catch {};

    frame_pool.deinit(_allocator);
    draw_buffer.deinit(_allocator);

    const duration: u64 = @intCast(time.nanoTimestamp() - init_time);
    const fps = @as(f64, @floatFromInt(frames_drawn)) /
        @as(f64, @floatFromInt(duration)) *
        @as(f64, @floatFromInt(time.ns_per_s));
    std.debug.print(
        "Rendered at {d:.3}fps for {}\n",
        .{ fps, std.fmt.fmtDuration(duration) },
    );
}

fn handleExit(sig: c_int) callconv(.C) void {
    switch (sig) {
        // Handle interrupt
        SIG.INT => {
            deinit();
            std.process.exit(0);
        },
        else => unreachable,
    }
}

fn handleExitWindows(sig: c_int, _: c_int) callconv(.C) void {
    handleExit(sig);
}

pub fn getTerminalSize() ?Size {
    if (is_windows) {
        return getTerminalSizeWindows();
    }

    if (!@hasDecl(system, "ioctl") or
        !@hasDecl(system, "T") or
        !@hasDecl(system.T, "IOCGWINSZ"))
    {
        @compileError("ioctl not available; cannot get terminal size.");
    }

    var size: system.winsize = undefined;
    const result = system.ioctl(
        std.os.STDOUT_FILENO,
        system.T.IOCGWINSZ,
        @intFromPtr(&size),
    );
    if (system.getErrno(result) != .SUCCESS) {
        return null;
    }

    return Size{
        .width = size.ws_col,
        .height = size.ws_row,
    };
}

fn getTerminalSizeWindows() ?Size {
    var info: windows.CONSOLE_SCREEN_BUFFER_INFO = undefined;
    const result = kernel32.GetConsoleScreenBufferInfo(stdout.handle, &info);
    if (system.getErrno(result) != .SUCCESS) {
        return null;
    }

    return Size{
        .width = @bitCast(info.dwSize.X),
        .height = @bitCast(info.dwSize.Y),
    };
}

pub fn setCanvasSize(width: u16, height: u16) !void {
    canvas_size = Size{ .width = width, .height = height };
    frame_pool.deinit(_allocator);
    // The actual frame buffers can never be larger than canvas_size,
    // so allocate that much
    frame_pool = try FramePool.init(_allocator, canvas_size, 2);

    const actual_size = canvas_size.bound(terminal_size);
    last = null;
    current = frame_pool.alloc(actual_size).?;
}

pub fn actualSize() Size {
    return current.size;
}

pub fn setTitle(title: []const u8) void {
    stdout.writeAll(OSC ++ "0;") catch {};
    stdout.writeAll(title) catch {};
    stdout.writeAll(ST) catch {};
}

fn useAlternateBuffer() void {
    stdout.writeAll(CSI ++ "?1049h") catch {};
}

fn useMainBuffer() void {
    stdout.writeAll(CSI ++ "?1049l") catch {};
}

pub fn drawPixel(x: u16, y: u16, fg: Color, bg: Color, char: u21) void {
    assert(x < canvas_size.width and y < canvas_size.height);

    if (!current.inBounds(x, y)) {
        return;
    }
    current.set(x, y, .{ .fg = fg, .bg = bg, .char = char });
}

pub fn render() !void {
    defer frames_drawn += 1;

    const old_terminal_size = terminal_size;
    terminal_size = getTerminalSize() orelse terminal_size;
    const draw_size = current.size.bound(terminal_size);

    const assume_wrap = draw_size.width >= terminal_size.width;
    const draw_diff = old_terminal_size.eql(terminal_size) and
        last != null and
        last.?.size.eql(current.size);

    const writer = draw_buffer.writer(_allocator);
    var last_x: u16 = 0;
    var last_y: u16 = 0;
    if (draw_diff) {
        // Find first difference
        for (0..current.pixels.len) |i| {
            if (!last.?.pixels[i].eql(current.pixels[i])) {
                last_x = @intCast(i % current.size.width);
                last_y = @intCast(i / current.size.width);
                break;
            }
        } else {
            // No diff to draw, advance and return
            advanceBuffers();
            return;
        }
    } else {
        // First frame, or either the canvas or terminal was resized,
        // so clear the screen and re-draw from scratch
        try clearScreen(writer);
        // Resizing the terminal may cause the cursor to be shown,
        // so hide it again
        try hideCursor(writer);
    }
    try setCursorPos(writer, last_x, last_y);

    var last_color = current.pixels[toCurrentIndex(last_x, last_y)];
    try setColor(writer, last_color.fg, last_color.bg);

    for (last_x..draw_size.width) |x| {
        try renderPixel(
            writer,
            &last_x,
            &last_y,
            &last_color,
            @intCast(x),
            last_y,
            draw_diff,
            assume_wrap,
        );
    }
    for (last_y + 1..draw_size.height) |y| {
        for (0..draw_size.width) |x| {
            try renderPixel(
                writer,
                &last_x,
                &last_y,
                &last_color,
                @intCast(x),
                @intCast(y),
                draw_diff,
                assume_wrap,
            );
        }
    }

    // Reset colors at the end so that the area outside the canvas stays black
    try resetColors(writer);
    stdout.writeAll(draw_buffer.items[0..draw_buffer.items.len]) catch {};

    advanceBuffers();
}

inline fn toCurrentIndex(x: u16, y: u16) usize {
    return @as(usize, y) * @as(usize, current.size.width) + @as(usize, x);
}

inline fn cursorDiff(
    writer: anytype,
    last_x: u16,
    last_y: u16,
    x: u16,
    y: u16,
    assume_wrap: bool,
) !void {
    // We're printing a character anyway, so no need to advance cursor by 1
    // However, if we're at the end of a line and can't wrap, we need to move
    // the cursor ourselves
    if (last_x + 1 == x and last_y == y) {
        return;
    }
    if (assume_wrap and
        last_x == current.size.width - 1 and
        x == 0 and
        last_y + 1 == y)
    {
        return;
    }

    try setCursorPos(writer, x, y);
}

inline fn renderPixel(
    writer: anytype,
    last_x: *u16,
    last_y: *u16,
    last_color: *Pixel,
    x: u16,
    y: u16,
    draw_diff: bool,
    assume_wrap: bool,
) !void {
    const i = toCurrentIndex(@intCast(x), @intCast(y));
    const p = current.pixels[i];

    if (draw_diff and last.?.pixels[i].eql(p)) {
        return;
    }

    if (last_color.fg != p.fg or last_color.bg != p.bg) {
        try setColor(writer, p.fg, p.bg);
    }
    try cursorDiff(
        writer,
        last_x.*,
        last_y.*,
        x,
        y,
        assume_wrap,
    );

    var utf8_bytes: [4]u8 = undefined;
    const len = try unicode.utf8Encode(p.char, &utf8_bytes);
    try writer.writeAll(utf8_bytes[0..len]);

    last_x.* = x;
    last_y.* = y;
    last_color.* = p;
}

fn advanceBuffers() void {
    const size = canvas_size.bound(terminal_size);
    if (last) |_last| {
        frame_pool.free(_last);
    }
    last = current;
    current = frame_pool.alloc(size).?;

    const max_size = canvas_size.area() * 4;
    if (draw_buffer.items.len > max_size) {
        draw_buffer.shrinkAndFree(_allocator, max_size);
    }
    draw_buffer.items.len = 0;
}

fn clearScreen(writer: anytype) !void {
    try writer.writeAll(CSI ++ "2J");
}

fn resetColors(writer: anytype) !void {
    try writer.writeAll(CSI ++ "m");
}

fn setColor(writer: anytype, fg: Color, bg: Color) !void {
    try writer.print(CSI ++ "{};{}m", .{ @intFromEnum(fg), @intFromEnum(bg) + 10 });
}

fn resetCursor(writer: anytype) !void {
    try writer.writeAll(CSI ++ "H");
}

fn setCursorPos(writer: anytype, x: u16, y: u16) !void {
    try writer.print(CSI ++ "{};{}H", .{ y + 1, x + 1 });
}

fn showCursor(writer: anytype) !void {
    try writer.writeAll(CSI ++ "?25h");
}

fn hideCursor(writer: anytype) !void {
    try writer.writeAll(CSI ++ "?25l");
}
