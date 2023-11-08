const std = @import("std");
const terminal = @import("../terminal.zig");
const Utf8View = std.unicode.Utf8View;
const Color = terminal.Color;
const assert = std.debug.assert;

const Self = @This();

const WriterContext = struct {
    self: Self,
    x: u16,
    y: u16,
    fg: Color,
    bg: Color,
};
const Writer = std.io.Writer(WriterContext, WriterError, writeFn);

left: u16,
top: u16,
width: u16,
height: u16,

pub fn init(left: u16, top: u16, width: u16, height: u16) Self {
    const canvas_size = terminal.getCanvasSize();
    assert(left + width <= canvas_size.width and
        top + height <= canvas_size.height);

    return Self{
        .left = left,
        .top = top,
        .width = width,
        .height = height,
    };
}

pub fn sub(self: Self, left: u16, top: u16, width: u16, height: u16) Self {
    assert(left + width <= self.width and top + height <= self.height);

    const new_left = self.left + left;
    const new_top = self.top + top;
    return Self{
        .left = new_left,
        .top = new_top,
        .width = width,
        .height = height,
    };
}

pub fn drawPixel(
    self: Self,
    x: u16,
    y: u16,
    fg: Color,
    bg: Color,
    char: u21,
) void {
    assert(x < self.width and y < self.height);
    terminal.drawPixel(x + self.left, y + self.top, fg, bg, char);
}

/// Overflows are truncated.
pub fn drawText(
    self: Self,
    x: u16,
    y: u16,
    fg: Color,
    bg: Color,
    text: []const u8,
) void {
    var code_points = (Utf8View.init(text) catch unreachable).iterator();
    var i = x;
    while (code_points.nextCodepoint()) |c| {
        if (i >= self.width) {
            break;
        }
        self.drawPixel(i, y, fg, bg, c);
        i += 1;
    }
}

/// Overflows are truncated.
pub fn printAt(
    self: Self,
    x: u16,
    y: u16,
    fg: Color,
    bg: Color,
    comptime fmt: []const u8,
    args: anytype,
) void {
    const writer = Writer{ .context = .{
        .self = self,
        .x = x,
        .y = y,
        .fg = fg,
        .bg = bg,
    } };
    std.fmt.format(writer, fmt, args) catch unreachable;
}

const WriterError = error{};
fn writeFn(context: WriterContext, bytes: []const u8) WriterError!usize {
    context.self.drawText(context.x, context.y, context.fg, context.bg, bytes);
    // Bytes that were truncated are also considered written
    return bytes.len;
}

pub fn drawBox(self: Self, left: u16, top: u16, width: u16, height: u16) void {
    const right = left + width - 1;
    const bottom = top + height - 1;

    self.drawPixel(left, top, .White, .Black, '╔');
    for (left + 1..right) |x| {
        self.drawPixel(@intCast(x), top, .White, .Black, '═');
    }
    self.drawPixel(right, top, .White, .Black, '╗');

    for (top + 1..bottom) |y| {
        self.drawPixel(left, @intCast(y), .White, .Black, '║');
        self.drawPixel(right, @intCast(y), .White, .Black, '║');
    }

    self.drawPixel(left, bottom, .White, .Black, '╚');
    for (left + 1..right) |x| {
        self.drawPixel(@intCast(x), bottom, .White, .Black, '═');
    }
    self.drawPixel(right, bottom, .White, .Black, '╝');
}
