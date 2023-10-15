const std = @import("std");
const assert = std.debug.assert;

pub const PieceMask = BitMask(4);
pub const BoardMask = BitMask(40);

/// A 2-dimensional bit mask. Width is fixed at 10.
/// Coordinates start at (0, 0) at the bottom left corner.
/// X increases rightwards.
/// Y increases upwards.
pub fn BitMask(comptime height: usize) type {
    return struct {
        const Self = @This();
        const width = 10;

        rows: [height]u16 = [_]u16{0} ** height,

        /// Returns true if the bit at (x, y) is set; otherwise, false.
        /// Panics if (x, y) is out of bounds.
        pub fn get(self: Self, x: usize, y: usize) bool {
            assert(x < width);

            const shift: u4 = @truncate(width - x - 1);
            return (self.rows[y] >> shift) & 1 == 1;
        }

        pub fn shr(self: *Self, shift: u4) void {
            for (&self.rows) |*row| {
                // No need to clear bits on left as there is no shift left,
                // so those bits must be 0.
                row.* >>= shift;
            }
        }

        /// For debugging
        pub fn format(self: Self, comptime fmt: []const u8, options: std.fmt.FormatOptions, writer: anytype) !void {
            _ = fmt;
            const top = @min(height, options.width orelse height);

            for (0..top) |i| {
                const y = top - i - 1;
                for (0..width) |x| {
                    _ = try writer.write(if (self.get(x, y)) "#" else ".");
                }
                _ = try writer.write("\n");
            }
        }
    };
}
