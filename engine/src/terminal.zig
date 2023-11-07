//! Provides a canvas-like interface for drawing to the terminal.

// TODO: Add option to display FPS
// TODO: Resize terminal buffer and window to match canvas size
// TODO: Center rendered frame when terminal is larger than canvas
// TODO: Create animation struct with transperancy
const std = @import("std");
const frame = @import("terminal/frame.zig");
const kernel32 = windows.kernel32;
const system = std.os.system;
const unicode = std.unicode;
const windows = std.os.windows;

const Allocator = std.mem.Allocator;
const ByteList = std.ArrayListUnmanaged(u8);
const File = std.fs.File;
const SIG = std.os.SIG;

const Pixel = frame.Pixel;
const Frame = frame.Frame;
const FramePool = frame.FramePool;
pub const View = @import("terminal/View.zig");

const assert = std.debug.assert;
const sigaction = std.os.sigaction;

const is_windows = @import("builtin").os.tag == .windows;
const ESC = "\x1B";
const CSI = ESC ++ "[";
const OSC = ESC ++ "]";
const ST = ESC ++ "\\";

var initialized = false;

var _allocator: Allocator = undefined;
var stdout: File = undefined;
var draw_buffer: ByteList = undefined;

var frame_pool: FramePool = undefined;
var last: ?Frame = null;
var current: Frame = undefined;

var canvas_size: Size = undefined;
var terminal_size: Size = undefined;

// https://en.wikipedia.org/wiki/ANSI_escape_code#Colors
// Closest 8-bit colors to Windows 10 Console's default 16, calculated using
// CIELAB color space
pub const Color = enum(u8) {
    Black = 232,
    Red = 124,
    Green = 34,
    Yellow = 178,
    Blue = 20, // Originally 27, but IMO 20 looks better
    Magenta = 90,
    Cyan = 32,
    White = 252,
    BrightBlack = 243,
    BrightRed = 203,
    BrightGreen = 40,
    BrightYellow = 229,
    BrightBlue = 69,
    BrightMagenta = 127,
    BrightCyan = 80,
    BrightWhite = 255,
};

pub const Size = struct {
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

const signal = if (is_windows)
    struct {
        extern "c" fn signal(
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
    AlreadyInitialized,
    FailedToSetConsoleOutputCP,
    FailedToSetConsoleMode,
};
pub fn init(allocator: Allocator, width: u16, height: u16) !void {
    if (initialized) {
        return InitError.AlreadyInitialized;
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
        const ENABLE_LVB_GRID_WORLDWIDE = 0x10;
        const result2 = setConsoleMode(
            stdout.handle,
            ENABLE_PROCESSED_OUTPUT |
                ENABLE_WRAP_AT_EOL_OUTPUT |
                ENABLE_VIRTUAL_TERMINAL_PROCESSING |
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

    initialized = true;
}

pub fn deinit() void {
    if (!initialized) {
        return;
    }
    initialized = false;

    useMainBuffer();
    showCursor(stdout.writer()) catch {};

    frame_pool.deinit(_allocator);
    draw_buffer.deinit(_allocator);
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

pub fn getCanvasSize() Size {
    return canvas_size;
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

fn toCurrentIndex(x: u16, y: u16) usize {
    return @as(usize, y) * @as(usize, current.size.width) + @as(usize, x);
}

fn cursorDiff(
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

fn renderPixel(
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
    try writer.print(CSI ++ "38;5;{};48;5;{}m", .{ @intFromEnum(fg), @intFromEnum(bg) });
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
