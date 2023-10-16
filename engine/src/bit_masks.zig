const std = @import("std");
const math = std.math;
const assert = std.debug.assert;

const Position = @import("pieces.zig").Position;

/// A 10 x 40 bit mask. Contains 1 bit of 1 padding at both the left and right ends.
/// Coordinates start at (0, 0) at the bottom left corner.
/// X increases rightwards.
/// Y increases upwards.
pub const BoardMask = struct {
    const width = 10;
    const height = 40;

    rows: [height]u16 = [_]u16{0b1_0000000000_1} ** height,

    /// Returns true if the bit at (x, y) is set; otherwise, false.
    /// Panics if (x, y) is out of bounds.
    pub fn get(self: BoardMask, x: usize, y: usize) bool {
        assert(x < width);

        const shift: u4 = @truncate(width - x);
        return (self.rows[y] >> shift) & 1 == 1;
    }

    pub fn collides(self: BoardMask, piece: PieceMask, pos: Position) bool {
        const start = @max(0, -pos.y);
        for (0..start) |i| {
            if (piece.rows[i] != 0) {
                return true;
            }
        }

        for (piece.rows[start..], start..) |row, i| {
            const y: usize = @bitCast(pos.y + @as(isize, @bitCast(i)));
            const intersect = self.rows[y] & (row >> @truncate(@as(u8, @bitCast(pos.x))));
            if (intersect != 0) {
                return true;
            }
        }
        return false;
    }

    pub fn place(self: *BoardMask, piece: PieceMask, pos: Position) void {
        const start = @max(0, -pos.y);
        for (piece.rows[start..], start..) |row, i| {
            self.rows[pos.y + i] |= row >> pos.x;
        }
    }

    pub fn unplace(self: *BoardMask, piece: PieceMask, pos: Position) void {
        const start = @max(0, -pos.y);
        for (piece.rows[start..], start..) |row, i| {
            self.rows[pos.y + i] ^= row >> pos.x;
        }
    }
};

/// A 10 x 4 bit mask. Contains 1 bit of 0 padding on the right.
/// Coordinates start at (0, 0) at the bottom left corner.
/// X increases rightwards.
/// Y increases upwards.
pub const PieceMask = struct {
    const width = 10;
    const height = 4;

    rows: [height]u16 = [_]u16{0} ** height,

    /// Returns true if the bit at (x, y) is set; otherwise, false.
    /// Panics if (x, y) is out of bounds.
    pub fn get(self: PieceMask, x: usize, y: usize) bool {
        assert(x < width);

        const shift: u4 = @truncate(width - x);
        return (self.rows[y] >> shift) & 1 == 1;
    }
};
