const std = @import("std");
const tokenizeScalar = std.mem.tokenizeScalar;

const Color = @import("nterm").Color;

const root = @import("root.zig");
const BoardMask = root.bit_masks.BoardMask;
const PieceMask = root.bit_masks.PieceMask;
const Rotation = root.kicks.Rotation;

/// The position of a piece on the playfield.
pub const Position = struct {
    x: i8,
    y: i8,

    pub fn add(self: Position, other: Position) Position {
        return Position{ .x = self.x + other.x, .y = self.y + other.y };
    }

    pub fn sub(self: Position, other: Position) Position {
        return Position{ .x = self.x - other.x, .y = self.y - other.y };
    }
};

/// The 4 possible orientations of a piece.
pub const Facing = enum(u2) {
    // Do not touch; other code depends on the order!
    up = 0,
    right = 1,
    down = 2,
    left = 3,

    pub fn rotate(self: Facing, rotation: Rotation) Facing {
        return switch (self) {
            .up => switch (rotation) {
                .quarter_cw => .right,
                .half => .down,
                .quarter_ccw => .left,
            },
            .right => switch (rotation) {
                .quarter_cw => .down,
                .half => .left,
                .quarter_ccw => .up,
            },
            .down => switch (rotation) {
                .quarter_cw => .left,
                .half => .up,
                .quarter_ccw => .right,
            },
            .left => switch (rotation) {
                .quarter_cw => .up,
                .half => .right,
                .quarter_ccw => .down,
            },
        };
    }
};

pub const PieceKind = enum(u3) {
    i = 0,
    o = 1,
    t = 2,
    s = 3,
    z = 4,
    l = 5,
    j = 6,

    pub fn startPos(self: PieceKind) Position {
        return switch (self) {
            .i => Position{ .x = 3, .y = 18 },
            .o => Position{ .x = 3, .y = 19 },
            .t => Position{ .x = 3, .y = 19 },
            .s => Position{ .x = 3, .y = 19 },
            .z => Position{ .x = 3, .y = 19 },
            .j => Position{ .x = 3, .y = 19 },
            .l => Position{ .x = 3, .y = 19 },
        };
    }

    pub fn color(self: PieceKind) Color {
        return switch (self) {
            .i => .bright_cyan,
            .o => .bright_yellow,
            .t => .bright_magenta,
            .s => .bright_green,
            .z => .red,
            .l => .yellow,
            .j => .blue,
        };
    }
};

pub const Piece = packed struct {
    facing: Facing,
    kind: PieceKind,

    pub fn mask(self: Piece) PieceMask {
        @setEvalBranchQuota(10_000);
        // TODO: Check if this is automatically optimised to an array
        return switch (self.kind) {
            .i => switch (self.facing) {
                .up => comptime PieceMask.parse(
                    \\....
                    \\####
                    \\....
                    \\....
                ),
                .right => comptime PieceMask.parse(
                    \\..#.
                    \\..#.
                    \\..#.
                    \\..#.
                ),
                .down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\####
                    \\....
                ),
                .left => comptime PieceMask.parse(
                    \\.#..
                    \\.#..
                    \\.#..
                    \\.#..
                ),
            },
            .o => comptime PieceMask.parse(
                \\....
                \\.##.
                \\.##.
                \\....
            ),
            .t => switch (self.facing) {
                .up => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\###.
                    \\....
                ),
                .right => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.##.
                    \\.#..
                ),
                .down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\###.
                    \\.#..
                ),
                .left => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\##..
                    \\.#..
                ),
            },
            .s => switch (self.facing) {
                .up => comptime PieceMask.parse(
                    \\....
                    \\.##.
                    \\##..
                    \\....
                ),
                .right => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.##.
                    \\..#.
                ),
                .down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\.##.
                    \\##..
                ),
                .left => comptime PieceMask.parse(
                    \\....
                    \\#...
                    \\##..
                    \\.#..
                ),
            },
            .z => switch (self.facing) {
                .up => comptime PieceMask.parse(
                    \\....
                    \\##..
                    \\.##.
                    \\....
                ),
                .right => comptime PieceMask.parse(
                    \\....
                    \\..#.
                    \\.##.
                    \\.#..
                ),
                .down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\##..
                    \\.##.
                ),
                .left => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\##..
                    \\#...
                ),
            },
            .j => switch (self.facing) {
                .up => comptime PieceMask.parse(
                    \\....
                    \\#...
                    \\###.
                    \\....
                ),
                .right => comptime PieceMask.parse(
                    \\....
                    \\.##.
                    \\.#..
                    \\.#..
                ),
                .down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\###.
                    \\..#.
                ),
                .left => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.#..
                    \\##..
                ),
            },
            .l => switch (self.facing) {
                .up => comptime PieceMask.parse(
                    \\....
                    \\..#.
                    \\###.
                    \\....
                ),
                .right => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.#..
                    \\.##.
                ),
                .down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\###.
                    \\#...
                ),
                .left => comptime PieceMask.parse(
                    \\....
                    \\##..
                    \\.#..
                    \\.#..
                ),
            },
        };
    }

    pub fn canonicalCenter(self: Piece) Position {
        return switch (self.kind) {
            .i => switch (self.facing) {
                .up => .{ .x = 1, .y = 2 },
                .right => .{ .x = 2, .y = 2 },
                .down => .{ .x = 2, .y = 1 },
                .left => .{ .x = 1, .y = 1 },
            },
            .o => switch (self.facing) {
                .up => .{ .x = 1, .y = 1 },
                .right => .{ .x = 1, .y = 2 },
                .down => .{ .x = 2, .y = 2 },
                .left => .{ .x = 2, .y = 1 },
            },
            .t, .s, .z, .j, .l => .{ .x = 1, .y = 1 },
        };
    }

    pub fn canonicalPosition(self: Piece, pos: Position) struct { x: u4, y: u6 } {
        const canonical_pos = self.canonicalCenter().add(pos);
        return .{
            .x = @intCast(canonical_pos.x),
            .y = @intCast(canonical_pos.y),
        };
    }

    pub fn fromCanonicalPosition(self: Piece, pos: struct { x: u4, y: u6 }) Position {
        const canonical_pos = Position{ .x = pos.x, .y = pos.y };
        return canonical_pos.sub(self.canonicalCenter());
    }

    pub fn left(self: Piece) u3 {
        const table = comptime makeAttributeTable(u3, findLeft);
        return table[@as(u5, @bitCast(self))];
    }

    pub fn right(self: Piece) u3 {
        const table = comptime makeAttributeTable(u3, findRight);
        return table[@as(u5, @bitCast(self))];
    }

    pub fn top(self: Piece) u3 {
        const table = comptime makeAttributeTable(u3, findTop);
        return table[@as(u5, @bitCast(self))];
    }

    pub fn bottom(self: Piece) u3 {
        const table = comptime makeAttributeTable(u3, findBottom);
        return table[@as(u5, @bitCast(self))];
    }

    pub fn minX(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMinX);
        return table[@as(u5, @bitCast(self))];
    }

    pub fn maxX(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMaxX);
        return table[@as(u5, @bitCast(self))];
    }

    pub fn minY(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMinY);
        return table[@as(u5, @bitCast(self))];
    }

    pub fn maxY(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMaxY);
        return table[@as(u5, @bitCast(self))];
    }

    fn makeAttributeTable(comptime T: type, comptime attribute: fn (PieceMask) T) [28]T {
        var table: [28]T = undefined;
        for (0..7) |piece_kind| {
            for (0..4) |facing| {
                const piece = Piece{
                    .facing = @enumFromInt(facing),
                    .kind = @enumFromInt(piece_kind),
                };
                table[@as(u5, @bitCast(piece))] = attribute(piece.mask());
            }
        }
        return table;
    }
};

fn findLeft(mask: PieceMask) u3 {
    var x = 0;
    outer: while (x < 4) : (x += 1) {
        for (0..4) |y| {
            if (mask.get(x, y)) {
                break :outer;
            }
        }
    }
    return x;
}

fn findRight(mask: PieceMask) u3 {
    var right = 3;
    outer: while (right >= 0) : (right -= 1) {
        for (0..4) |y| {
            if (mask.get(right, y)) {
                break :outer;
            }
        }
    }
    return right + 1;
}

fn findTop(mask: PieceMask) u3 {
    var y = 3;
    while (y >= 0) : (y -= 1) {
        if (mask.rows[y] != 0) {
            break;
        }
    }
    return y + 1;
}

fn findBottom(mask: PieceMask) u3 {
    var y = 0;
    while (y < 4) : (y += 1) {
        if (mask.rows[y] != 0) {
            break;
        }
    }
    return y;
}

fn findMinX(mask: PieceMask) i8 {
    return -@as(i8, @intCast(findLeft(mask)));
}

fn findMaxX(mask: PieceMask) i8 {
    return PieceMask.WIDTH - @as(i8, @intCast(findRight(mask)));
}

fn findMinY(mask: PieceMask) i8 {
    return -@as(i8, @intCast(findBottom(mask)));
}

fn findMaxY(mask: PieceMask) i8 {
    return BoardMask.HEIGHT - @as(i8, @intCast(findTop(mask)));
}
