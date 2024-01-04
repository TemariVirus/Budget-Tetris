const std = @import("std");
const math = std.math;
const assert = std.debug.assert;
const tokenizeScalar = std.mem.tokenizeScalar;

const Position = @import("pieces.zig").Position;

/// A 10 x 40 bit mask. Contains 5 bits of 1 padding at the left end,
/// and 1 bit of 1 padding at the right end.
/// Coordinates start at (0, 0) at the bottom left corner.
/// X increases rightwards.
/// Y increases upwards.
pub const BoardMask = struct {
    pub const WIDTH = 10;
    pub const HEIGHT = 40;
    pub const EMPTY_ROW: u16 = 0b11111_0000000000_1;
    pub const FULL_ROW: u16 = 0b11111_1111111111_1;

    rows: [HEIGHT]u16 = [_]u16{EMPTY_ROW} ** HEIGHT,

    /// Returns true if the bit at (x, y) is set; otherwise, false.
    /// Panics if (x, y) is out of bounds.
    pub fn get(self: BoardMask, x: usize, y: usize) bool {
        assert(x < WIDTH);
        assert(y < HEIGHT);

        const shift: u4 = @intCast(WIDTH - x);
        return (self.rows[y] >> shift) & 1 == 1;
    }

    pub fn set(self: *BoardMask, x: usize, y: usize, value: bool) void {
        assert(x < WIDTH);
        assert(y < HEIGHT);

        const shift: u4 = @intCast(WIDTH - x);
        if (value) {
            self.rows[y] |= @as(u16, 1) << shift;
        } else {
            self.rows[y] &= ~(@as(u16, 1) << shift);
        }
    }

    pub fn collides(self: BoardMask, piece: PieceMask, pos: Position) bool {
        const start = @max(0, -pos.y);
        for (0..start) |i| {
            if (piece.rows[i] != 0) {
                return true;
            }
        }

        for (piece.rows[start..], start..) |row, i| {
            const y: usize = @intCast(pos.y + @as(isize, @intCast(i)));
            const shifted_row = if (pos.x < 0)
                row << @intCast(-pos.x)
            else
                row >> @intCast(pos.x);
            const intersect = self.rows[y] & shifted_row;
            if (intersect != 0) {
                return true;
            }
        }
        return false;
    }

    pub fn place(self: *BoardMask, piece: PieceMask, pos: Position) void {
        const start = @max(0, -pos.y);
        for (piece.rows[start..], start..) |row, i| {
            const shifted = if (pos.x < 0)
                row << @intCast(-pos.x)
            else
                row >> @intCast(pos.x);
            self.rows[@intCast(pos.y + @as(i8, @intCast(i)))] |= shifted;
        }
    }

    pub fn unplace(self: *BoardMask, piece: PieceMask, pos: Position) void {
        const start = @max(0, -pos.y);
        for (piece.rows[start..], start..) |row, i| {
            const shifted = if (pos.x < 0)
                row << @intCast(-pos.x)
            else
                row >> @intCast(pos.x);
            self.rows[@intCast(pos.y + @as(i8, @intCast(i)))] &= ~shifted;
        }
    }
};

/// A 10 x 4 bit mask. Contains 1 bit of 0 padding on the right.
/// Coordinates start at (0, 0) at the bottom left corner.
/// X increases rightwards.
/// Y increases upwards.
pub const PieceMask = struct {
    pub const WIDTH = 10;
    pub const HEIGHT = 4;

    rows: [HEIGHT]u16 = [_]u16{0} ** HEIGHT,

    pub fn parse(comptime str: []const u8) PieceMask {
        var rows = [_]u16{0} ** 4;
        var lines = tokenizeScalar(u8, str, '\n');

        var i: usize = 4;
        while (lines.next()) |line| {
            i -= 1;
            for (0..10) |j| {
                if (j < line.len and line[j] == '#') {
                    rows[i] |= 1;
                }
                rows[i] <<= 1;
            }
        }

        return PieceMask{ .rows = rows };
    }

    /// Returns true if the bit at (x, y) is set; otherwise, false.
    /// Panics if (x, y) is out of bounds.
    pub fn get(self: PieceMask, x: usize, y: usize) bool {
        assert(x < WIDTH);
        assert(y < HEIGHT);

        const shift: u4 = @intCast(WIDTH - x);
        return (self.rows[y] >> shift) & 1 == 1;
    }
};
