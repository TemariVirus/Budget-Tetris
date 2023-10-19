const std = @import("std");
const math = std.math;
const assert = std.debug.assert;

const Position = @import("pieces.zig").Position;

/// A 10 x 40 bit mask. Contains 5 bits of 1 padding at the left end,
/// and 1 bit of 1 padding at the right end.
/// Coordinates start at (0, 0) at the bottom left corner.
/// X increases rightwards.
/// Y increases upwards.
pub const BoardMask = struct {
    const width = 10;
    const height = 40;
    pub const empty_row: u16 = 0b11111_0000000000_1;
    pub const full_row: u16 = 0b11111_1111111111_1;

    rows: [height]u16 = [_]u16{empty_row} ** height,

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
            const y_size = @as(isize, pos.y);
            const i_size = @as(isize, @bitCast(i));
            const shifted = if (pos.x < 0)
                row << @truncate(@as(u8, @bitCast(-pos.x)))
            else
                row >> @truncate(@as(u8, @bitCast(pos.x)));
            self.rows[@as(usize, @bitCast(y_size + i_size))] |= shifted;
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

    pub fn bitAnd(self: PieceMask, other: PieceMask) PieceMask {
        var result: PieceMask = undefined;
        for (0..height) |i| {
            result.rows[i] = self.rows[i] & other.rows[i];
        }
        return result;
    }

    pub fn eql(self: PieceMask, other: PieceMask) bool {
        const val1: *const u64 = @ptrCast(@alignCast(&self.rows));
        const val2: *const u64 = @ptrCast(@alignCast(&other.rows));
        return val1.* == val2.*;
    }
};
