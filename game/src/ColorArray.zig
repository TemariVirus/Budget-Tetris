const assert = @import("std").debug.assert;
const Color = @import("nterm").Color;

const Self = @This();

pub const WIDTH = 10;
pub const HEIGHT = 40;
pub const EMPTY_COLOR = Color.black;
pub const GARBAGE_COLOR = Color.white;
const EMPTY_BYTE = (@intFromEnum(PackedColor.empty) << 4) | @intFromEnum(PackedColor.empty);

const PackedColor = enum(u8) {
    empty,
    red,
    green,
    yellow,
    blue,
    magenta,
    cyan,
    garbage,
    bright_black,
    bright_red,
    bright_green,
    bright_yellow,
    bright_blue,
    bright_magenta,
    bright_cyan,
    bright_white,
};

data: [WIDTH * HEIGHT / 2]u8,

pub fn init() Self {
    const data = [_]u8{EMPTY_BYTE} ** (WIDTH * HEIGHT / 2);
    return Self{ .data = data };
}

fn pack(color: Color) PackedColor {
    return switch (color) {
        .black => .empty,
        .red => .red,
        .green => .green,
        .yellow => .yellow,
        .blue => .blue,
        .magenta => .magenta,
        .cyan => .cyan,
        .white => .garbage,
        .bright_black => .bright_black,
        .bright_red => .bright_red,
        .bright_green => .bright_green,
        .bright_yellow => .bright_yellow,
        .bright_blue => .bright_blue,
        .bright_magenta => .bright_magenta,
        .bright_cyan => .bright_cyan,
        .bright_white => .bright_white,
    };
}

fn unpack(color: PackedColor) Color {
    return switch (color) {
        .empty => .black,
        .red => .red,
        .green => .green,
        .yellow => .yellow,
        .blue => .blue,
        .magenta => .magenta,
        .cyan => .cyan,
        .garbage => .white,
        .bright_black => .bright_black,
        .bright_red => .bright_red,
        .bright_green => .bright_green,
        .bright_yellow => .bright_yellow,
        .bright_blue => .bright_blue,
        .bright_magenta => .bright_magenta,
        .bright_cyan => .bright_cyan,
        .bright_white => .bright_white,
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
    const empty = @intFromEnum(PackedColor.empty);

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
    const garbage = @intFromEnum(PackedColor.garbage);

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
