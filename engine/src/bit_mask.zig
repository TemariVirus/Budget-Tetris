const math = std.math;
const std = @import("std");
const testing = std.testing;

pub const PieceMask = BitMask(4);
pub const BoardMask = BitMask(40);

/// A 2-dimensional bit mask.
/// Coordinates start at (0, 0) at the bottom left corner.
/// X increases rightwards.
/// Y increases upwards.
pub fn BitMask(comptime height: usize) type {
    return struct {
        const Self = @This();
        const width = 10;

        rows: [height]u16 = undefined,

        pub fn format(self: Self, comptime fmt: []const u8, options: std.fmt.FormatOptions, writer: anytype) !void {
            _ = fmt;
            const top = options.width orelse height;

            for (0..top) |i| {
                const row = self.rows[top - i - 1];
                for (0..width) |j| {
                    const shift = width - j - 1;
                    const mino = if ((row >> @truncate(shift)) & 1 == 1) "#" else ".";
                    _ = try writer.write(mino);
                }
                _ = try writer.write("\n");
            }
        }
    };
}

test "init BitMask" {
    const mask = BitMask(40){};

    try testing.expect(mask.rows.len == 40);
}
