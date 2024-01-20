const assert = @import("std").debug.assert;
const Color = @import("nterm").Color;

const Self = @This();

pub const WIDTH = 10;
pub const HEIGHT = 40;
pub const EMPTY_COLOR = Color.Black;
pub const GARBAGE_COLOR = Color.White;
const EMPTY_BYTE = (@intFromEnum(PackedColor.Empty) << 4) | @intFromEnum(PackedColor.Empty);

const PackedColor = enum(u8) {
    Empty,
    Red,
    Green,
    Yellow,
    Blue,
    Magenta,
    Cyan,
    Garbage,
    BrightBlack,
    BrightRed,
    BrightGreen,
    BrightYellow,
    BrightBlue,
    BrightMagenta,
    BrightCyan,
    BrightWhite,
};

data: [WIDTH * HEIGHT / 2]u8,

pub fn init() Self {
    const data = [_]u8{EMPTY_BYTE} ** (WIDTH * HEIGHT / 2);
    return Self{ .data = data };
}

fn pack(color: Color) PackedColor {
    return switch (color) {
        .Black => .Empty,
        .Red => .Red,
        .Green => .Green,
        .Yellow => .Yellow,
        .Blue => .Blue,
        .Magenta => .Magenta,
        .Cyan => .Cyan,
        .White => .Garbage,
        .BrightBlack => .BrightBlack,
        .BrightRed => .BrightRed,
        .BrightGreen => .BrightGreen,
        .BrightYellow => .BrightYellow,
        .BrightBlue => .BrightBlue,
        .BrightMagenta => .BrightMagenta,
        .BrightCyan => .BrightCyan,
        .BrightWhite => .BrightWhite,
    };
}

fn unpack(color: PackedColor) Color {
    return switch (color) {
        .Empty => .Black,
        .Red => .Red,
        .Green => .Green,
        .Yellow => .Yellow,
        .Blue => .Blue,
        .Magenta => .Magenta,
        .Cyan => .Cyan,
        .Garbage => .White,
        .BrightBlack => .BrightBlack,
        .BrightRed => .BrightRed,
        .BrightGreen => .BrightGreen,
        .BrightYellow => .BrightYellow,
        .BrightBlue => .BrightBlue,
        .BrightMagenta => .BrightMagenta,
        .BrightCyan => .BrightCyan,
        .BrightWhite => .BrightWhite,
    };
}

pub fn get(self: Self, x: usize, y: usize) Color {
    assert(x < WIDTH and y < HEIGHT);

    const i = (y * WIDTH + x) / 2;
    const color = if (x % 2 == 0)
        self.data[i] & 0xF
    else
        self.data[i] >> 4;
    return unpack(@enumFromInt(color));
}

pub fn set(self: *Self, x: usize, y: usize, color: Color) void {
    assert(x < WIDTH and y < HEIGHT);

    const i = (y * WIDTH + x) / 2;
    const packed_color = @intFromEnum(pack(color));
    if (x % 2 == 0) {
        self.data[i] = (self.data[i] & 0xF0) | packed_color;
    } else {
        self.data[i] = (packed_color << 4) | (self.data[i] & 0x0F);
    }
}

pub fn copyRow(self: *Self, dst: usize, src: usize) void {
    assert(src < HEIGHT and dst < HEIGHT);

    const src_index = src * WIDTH / 2;
    const dst_index = dst * WIDTH / 2;
    for (0..WIDTH / 2) |i| {
        self.data[dst_index + i] = self.data[src_index + i];
    }
}

pub fn isRowFull(colors: Self, y: usize) bool {
    assert(y < HEIGHT);
    const empty = @intFromEnum(PackedColor.Empty);

    const i = y * WIDTH / 2;
    for (colors.data[i .. i + WIDTH / 2]) |color| {
        const high = color >> 4;
        const low = color & 0xF;
        if (high == empty or low == empty) {
            return false;
        }
    }
    return true;
}

pub fn isRowGarbage(colors: Self, y: usize) bool {
    assert(y < HEIGHT);
    const garbage = @intFromEnum(PackedColor.Garbage);

    const i = y * WIDTH / 2;
    for (colors.data[i .. i + WIDTH / 2]) |color| {
        const high = color >> 4;
        const low = color & 0xF;
        if (high == garbage or low == garbage) {
            return true;
        }
    }
    return false;
}

pub fn emptyRow(self: *Self, y: usize) void {
    assert(y < HEIGHT);

    const i = y * WIDTH / 2;
    for (self.data[i .. i + WIDTH / 2]) |*color| {
        color.* = EMPTY_BYTE;
    }
}
