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
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3,

    pub fn rotate(self: Facing, rotation: Rotation) Facing {
        return switch (self) {
            .Up => switch (rotation) {
                .QuarterCw => .Right,
                .Half => .Down,
                .QuarterCCw => .Left,
            },
            .Right => switch (rotation) {
                .QuarterCw => .Down,
                .Half => .Left,
                .QuarterCCw => .Up,
            },
            .Down => switch (rotation) {
                .QuarterCw => .Left,
                .Half => .Up,
                .QuarterCCw => .Right,
            },
            .Left => switch (rotation) {
                .QuarterCw => .Up,
                .Half => .Right,
                .QuarterCCw => .Down,
            },
        };
    }
};

pub const PieceKind = enum(u3) {
    I,
    O,
    T,
    S,
    Z,
    L,
    J,

    pub fn startPos(self: PieceKind) Position {
        return switch (self) {
            .I => Position{ .x = 3, .y = 18 },
            .O => Position{ .x = 3, .y = 19 },
            .T => Position{ .x = 3, .y = 19 },
            .S => Position{ .x = 3, .y = 19 },
            .Z => Position{ .x = 3, .y = 19 },
            .J => Position{ .x = 3, .y = 19 },
            .L => Position{ .x = 3, .y = 19 },
        };
    }

    pub fn color(self: PieceKind) Color {
        return switch (self) {
            .I => .BrightCyan,
            .O => .BrightYellow,
            .T => .BrightMagenta,
            .S => .BrightGreen,
            .Z => .Red,
            .L => .Yellow,
            .J => .Blue,
        };
    }
};

pub const Piece = packed struct {
    facing: Facing,
    kind: PieceKind,

    pub fn mask(self: Piece) PieceMask {
        @setEvalBranchQuota(10_000);
        return switch (self.kind) {
            .I => switch (self.facing) {
                .Up => comptime PieceMask.parse(
                    \\....
                    \\####
                    \\....
                    \\....
                ),
                .Right => comptime PieceMask.parse(
                    \\..#.
                    \\..#.
                    \\..#.
                    \\..#.
                ),
                .Down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\####
                    \\....
                ),
                .Left => comptime PieceMask.parse(
                    \\.#..
                    \\.#..
                    \\.#..
                    \\.#..
                ),
            },
            .O => comptime PieceMask.parse(
                \\....
                \\.##.
                \\.##.
                \\....
            ),
            .T => switch (self.facing) {
                .Up => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\###.
                    \\....
                ),
                .Right => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.##.
                    \\.#..
                ),
                .Down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\###.
                    \\.#..
                ),
                .Left => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\##..
                    \\.#..
                ),
            },
            .S => switch (self.facing) {
                .Up => comptime PieceMask.parse(
                    \\....
                    \\.##.
                    \\##..
                    \\....
                ),
                .Right => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.##.
                    \\..#.
                ),
                .Down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\.##.
                    \\##..
                ),
                .Left => comptime PieceMask.parse(
                    \\....
                    \\#...
                    \\##..
                    \\.#..
                ),
            },
            .Z => switch (self.facing) {
                .Up => comptime PieceMask.parse(
                    \\....
                    \\##..
                    \\.##.
                    \\....
                ),
                .Right => comptime PieceMask.parse(
                    \\....
                    \\..#.
                    \\.##.
                    \\.#..
                ),
                .Down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\##..
                    \\.##.
                ),
                .Left => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\##..
                    \\#...
                ),
            },
            .J => switch (self.facing) {
                .Up => comptime PieceMask.parse(
                    \\....
                    \\#...
                    \\###.
                    \\....
                ),
                .Right => comptime PieceMask.parse(
                    \\....
                    \\.##.
                    \\.#..
                    \\.#..
                ),
                .Down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\###.
                    \\..#.
                ),
                .Left => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.#..
                    \\##..
                ),
            },
            .L => switch (self.facing) {
                .Up => comptime PieceMask.parse(
                    \\....
                    \\..#.
                    \\###.
                    \\....
                ),
                .Right => comptime PieceMask.parse(
                    \\....
                    \\.#..
                    \\.#..
                    \\.##.
                ),
                .Down => comptime PieceMask.parse(
                    \\....
                    \\....
                    \\###.
                    \\#...
                ),
                .Left => comptime PieceMask.parse(
                    \\....
                    \\##..
                    \\.#..
                    \\.#..
                ),
            },
        };
    }

    pub fn left(self: Piece) u8 {
        const table = comptime makeAttributeTable(u8, findLeft);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }

    pub fn right(self: Piece) u8 {
        const table = comptime makeAttributeTable(u8, findRight);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }

    pub fn top(self: Piece) u8 {
        const table = comptime makeAttributeTable(u8, findTop);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }

    pub fn bottom(self: Piece) u8 {
        const table = comptime makeAttributeTable(u8, findBottom);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }

    pub fn minX(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMinX);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }

    pub fn maxX(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMaxX);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }

    pub fn minY(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMinY);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }

    pub fn maxY(self: Piece) i8 {
        const table = comptime makeAttributeTable(i8, findMaxY);
        return table[@intFromEnum(self.kind)][@intFromEnum(self.facing)];
    }
};

fn makeAttributeTable(comptime T: type, comptime attribute: fn (PieceMask) T) [7][4]T {
    var table: [7][4]T = undefined;
    for (0..7) |piece_kind| {
        for (0..4) |facing| {
            const piece = Piece{
                .facing = @enumFromInt(facing),
                .kind = @enumFromInt(piece_kind),
            };
            table[piece_kind][facing] = attribute(piece.mask());
        }
    }
    return table;
}

fn findLeft(mask: PieceMask) u8 {
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

fn findRight(mask: PieceMask) u8 {
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

fn findTop(mask: PieceMask) u8 {
    var y = 3;
    while (y >= 0) : (y -= 1) {
        if (mask.rows[y] != 0) {
            break;
        }
    }
    return y + 1;
}

fn findBottom(mask: PieceMask) u8 {
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
