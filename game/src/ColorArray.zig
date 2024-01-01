const assert = @import("std").debug.assert;
const Color = @import("nterm").Color;

const Self = @This();

pub const width = 10;
pub const height = 40;
const empty_byte = (@intFromEnum(PackedColor.Empty) << 4) | @intFromEnum(PackedColor.Empty);

const PackedColor = enum(u8) {
    Empty,
    Red,
    Green,
    Yellow,
    Blue,
    Magenta,
    Cyan,
    White,
    Garbage,
    BrightRed,
    BrightGreen,
    BrightYellow,
    BrightBlue,
    BrightMagenta,
    BrightCyan,
    BrightWhite,
};

data: [width * height / 2]u8,

pub fn init() Self {
    const data = [_]u8{empty_byte} ** (width * height / 2);
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
        .White => .White,
        .BrightBlack => .Garbage,
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
        .White => .White,
        .Garbage => .BrightBlack,
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
    assert(x < width and y < height);

    const i = (y * width + x) / 2;
    const color = if (x % 2 == 0)
        self.data[i] & 0xF
    else
        self.data[i] >> 4;
    return unpack(@enumFromInt(color));
}

pub fn set(self: *Self, x: usize, y: usize, color: Color) void {
    assert(x < width and y < height);

    const i = (y * width + x) / 2;
    const packed_color = @intFromEnum(pack(color));
    if (x % 2 == 0) {
        self.data[i] = (self.data[i] & 0xF0) | packed_color;
    } else {
        self.data[i] = (packed_color << 4) | (self.data[i] & 0x0F);
    }
}

pub fn copyRow(self: *Self, dst: usize, src: usize) void {
    assert(src < height and dst < height);

    const src_index = src * width / 2;
    const dst_index = dst * width / 2;
    for (0..width / 2) |i| {
        self.data[dst_index + i] = self.data[src_index + i];
    }
}

pub fn isRowFull(colors: Self, y: usize) bool {
    assert(y < height);
    const empty = @intFromEnum(PackedColor.Empty);

    const i = y * width / 2;
    for (colors.data[i .. i + width / 2]) |color| {
        const high = color >> 4;
        const low = color & 0xF;
        if (high == empty or low == empty) {
            return false;
        }
    }
    return true;
}

pub fn isRowGarbage(colors: Self, y: usize) bool {
    assert(y < height);
    const garbage = @intFromEnum(PackedColor.Garbage);

    const i = y * width / 2;
    for (colors.data[i .. i + width / 2]) |color| {
        const high = color >> 4;
        const low = color & 0xF;
        if (high == garbage or low == garbage) {
            return true;
        }
    }
    return false;
}

pub fn emptyRow(self: *Self, y: usize) void {
    assert(y < height);

    const i = y * width / 2;
    for (self.data[i .. i + width / 2]) |*color| {
        color.* = empty_byte;
    }
}
