const tokenizeScalar = @import("std").mem.tokenizeScalar;

const Color = @import("nterm").Color;
const PieceMask = @import("bit_masks.zig").PieceMask;
const Rotation = @import("kicks.zig").Rotation;

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
                .Cw => .Right,
                .Double => .Down,
                .CCw => .Left,
            },
            .Right => switch (rotation) {
                .Cw => .Down,
                .Double => .Left,
                .CCw => .Up,
            },
            .Down => switch (rotation) {
                .Cw => .Left,
                .Double => .Up,
                .CCw => .Right,
            },
            .Left => switch (rotation) {
                .Cw => .Up,
                .Double => .Right,
                .CCw => .Down,
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
                .Up => comptime parsePiece(
                    \\....
                    \\####
                    \\....
                    \\....
                ),
                .Right => comptime parsePiece(
                    \\..#.
                    \\..#.
                    \\..#.
                    \\..#.
                ),
                .Down => comptime parsePiece(
                    \\....
                    \\....
                    \\####
                    \\....
                ),
                .Left => comptime parsePiece(
                    \\.#..
                    \\.#..
                    \\.#..
                    \\.#..
                ),
            },
            .O => comptime parsePiece(
                \\....
                \\.##.
                \\.##.
                \\....
            ),
            .T => switch (self.facing) {
                .Up => comptime parsePiece(
                    \\....
                    \\.#..
                    \\###.
                    \\....
                ),
                .Right => comptime parsePiece(
                    \\....
                    \\.#..
                    \\.##.
                    \\.#..
                ),
                .Down => comptime parsePiece(
                    \\....
                    \\....
                    \\###.
                    \\.#..
                ),
                .Left => comptime parsePiece(
                    \\....
                    \\.#..
                    \\##..
                    \\.#..
                ),
            },
            .S => switch (self.facing) {
                .Up => comptime parsePiece(
                    \\....
                    \\.##.
                    \\##..
                    \\....
                ),
                .Right => comptime parsePiece(
                    \\....
                    \\.#..
                    \\.##.
                    \\..#.
                ),
                .Down => comptime parsePiece(
                    \\....
                    \\....
                    \\.##.
                    \\##..
                ),
                .Left => comptime parsePiece(
                    \\....
                    \\#...
                    \\##..
                    \\.#..
                ),
            },
            .Z => switch (self.facing) {
                .Up => comptime parsePiece(
                    \\....
                    \\##..
                    \\.##.
                    \\....
                ),
                .Right => comptime parsePiece(
                    \\....
                    \\..#.
                    \\.##.
                    \\.#..
                ),
                .Down => comptime parsePiece(
                    \\....
                    \\....
                    \\##..
                    \\.##.
                ),
                .Left => comptime parsePiece(
                    \\....
                    \\.#..
                    \\##..
                    \\#...
                ),
            },
            .J => switch (self.facing) {
                .Up => comptime parsePiece(
                    \\....
                    \\#...
                    \\###.
                    \\....
                ),
                .Right => comptime parsePiece(
                    \\....
                    \\.##.
                    \\.#..
                    \\.#..
                ),
                .Down => comptime parsePiece(
                    \\....
                    \\....
                    \\###.
                    \\..#.
                ),
                .Left => comptime parsePiece(
                    \\....
                    \\.#..
                    \\.#..
                    \\##..
                ),
            },
            .L => switch (self.facing) {
                .Up => comptime parsePiece(
                    \\....
                    \\..#.
                    \\###.
                    \\....
                ),
                .Right => comptime parsePiece(
                    \\....
                    \\.#..
                    \\.#..
                    \\.##.
                ),
                .Down => comptime parsePiece(
                    \\....
                    \\....
                    \\###.
                    \\#...
                ),
                .Left => comptime parsePiece(
                    \\....
                    \\##..
                    \\.#..
                    \\.#..
                ),
            },
        };
    }

    pub fn top(self: Piece) u8 {
        return switch (self.kind) {
            .I => switch (self.facing) {
                .Up => comptime topComptime(.{
                    .facing = .Up,
                    .kind = .I,
                }),
                .Right => comptime topComptime(.{
                    .facing = .Right,
                    .kind = .I,
                }),
                .Down => comptime topComptime(.{
                    .facing = .Down,
                    .kind = .I,
                }),
                .Left => comptime topComptime(.{
                    .facing = .Left,
                    .kind = .I,
                }),
            },
            .O => comptime topComptime(.{
                .facing = .Up,
                .kind = .O,
            }),
            .T => switch (self.facing) {
                .Up => comptime topComptime(.{
                    .facing = .Up,
                    .kind = .T,
                }),
                .Right => comptime topComptime(.{
                    .facing = .Right,
                    .kind = .T,
                }),
                .Down => comptime topComptime(.{
                    .facing = .Down,
                    .kind = .T,
                }),
                .Left => comptime topComptime(.{
                    .facing = .Left,
                    .kind = .T,
                }),
            },
            .S => switch (self.facing) {
                .Up => comptime topComptime(.{
                    .facing = .Up,
                    .kind = .S,
                }),
                .Right => comptime topComptime(.{
                    .facing = .Right,
                    .kind = .S,
                }),
                .Down => comptime topComptime(.{
                    .facing = .Down,
                    .kind = .S,
                }),
                .Left => comptime topComptime(.{
                    .facing = .Left,
                    .kind = .S,
                }),
            },
            .Z => switch (self.facing) {
                .Up => comptime topComptime(.{
                    .facing = .Up,
                    .kind = .Z,
                }),
                .Right => comptime topComptime(.{
                    .facing = .Right,
                    .kind = .Z,
                }),
                .Down => comptime topComptime(.{
                    .facing = .Down,
                    .kind = .Z,
                }),
                .Left => comptime topComptime(.{
                    .facing = .Left,
                    .kind = .Z,
                }),
            },
            .J => switch (self.facing) {
                .Up => comptime topComptime(.{
                    .facing = .Up,
                    .kind = .J,
                }),
                .Right => comptime topComptime(.{
                    .facing = .Right,
                    .kind = .J,
                }),
                .Down => comptime topComptime(.{
                    .facing = .Down,
                    .kind = .J,
                }),
                .Left => comptime topComptime(.{
                    .facing = .Left,
                    .kind = .J,
                }),
            },
            .L => switch (self.facing) {
                .Up => comptime topComptime(.{
                    .facing = .Up,
                    .kind = .L,
                }),
                .Right => comptime topComptime(.{
                    .facing = .Right,
                    .kind = .L,
                }),
                .Down => comptime topComptime(.{
                    .facing = .Down,
                    .kind = .L,
                }),
                .Left => comptime topComptime(.{
                    .facing = .Left,
                    .kind = .L,
                }),
            },
        };
    }

    fn topComptime(comptime piece: Piece) u8 {
        const piece_mask = comptime piece.mask();
        var height = 4;
        inline while (height > 0) : (height -= 1) {
            if (piece_mask.rows[height - 1] != 0) {
                return height;
            }
        }
        return height;
    }
};

pub fn parsePiece(comptime str: []const u8) PieceMask {
    var result = [_]u16{0} ** 4;
    var lines = tokenizeScalar(u8, str, '\n');

    var i: usize = 4;
    while (lines.next()) |line| {
        i -= 1;
        for (0..10) |j| {
            if (j < line.len and line[j] == '#') {
                result[i] |= 1;
            }
            result[i] <<= 1;
        }
    }

    return PieceMask{ .rows = result };
}
