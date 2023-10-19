const tokenizeScalar = @import("std").mem.tokenizeScalar;

const PieceMask = @import("bit_masks.zig").PieceMask;
const Rotation = @import("kicks.zig").Rotation;

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

// Do not touch; other code depends on the order!
pub const Facing = enum(u2) {
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3,

    pub fn rotate(self: Facing, rotation: Rotation) Facing {
        return switch (self) {
            .Up => switch (rotation) {
                .Cw => .Right,
                .Double => .Down,
                .ACw => .Left,
            },
            .Right => switch (rotation) {
                .Cw => .Down,
                .Double => .Left,
                .ACw => .Up,
            },
            .Down => switch (rotation) {
                .Cw => .Left,
                .Double => .Up,
                .ACw => .Right,
            },
            .Left => switch (rotation) {
                .Cw => .Up,
                .Double => .Right,
                .ACw => .Down,
            },
        };
    }
};

pub const PieceType = enum(u3) {
    I,
    O,
    T,
    S,
    Z,
    L,
    J,

    pub fn startPos(piece_type: PieceType) Position {
        return switch (piece_type) {
            .I => Position{ .x = 3, .y = 18 },
            .O => Position{ .x = 3, .y = 19 },
            .T => Position{ .x = 3, .y = 19 },
            .S => Position{ .x = 3, .y = 19 },
            .Z => Position{ .x = 3, .y = 19 },
            .J => Position{ .x = 3, .y = 19 },
            .L => Position{ .x = 3, .y = 19 },
        };
    }
};

pub const Piece = packed struct {
    facing: Facing,
    type: PieceType,

    pub fn mask(self: Piece) PieceMask {
        return switch (self.type) {
            .I => switch (self.facing) {
                .Up => parsePiece(
                    \\....
                    \\####
                    \\....
                    \\....
                ),
                .Right => parsePiece(
                    \\..#.
                    \\..#.
                    \\..#.
                    \\..#.
                ),
                .Down => parsePiece(
                    \\....
                    \\....
                    \\####
                    \\....
                ),
                .Left => parsePiece(
                    \\.#..
                    \\.#..
                    \\.#..
                    \\.#..
                ),
            },
            .O => parsePiece(
                \\....
                \\.##.
                \\.##.
                \\....
            ),
            .T => switch (self.facing) {
                .Up => parsePiece(
                    \\....
                    \\.#..
                    \\###.
                    \\....
                ),
                .Right => parsePiece(
                    \\....
                    \\.#..
                    \\.##.
                    \\.#..
                ),
                .Down => parsePiece(
                    \\....
                    \\....
                    \\###.
                    \\.#..
                ),
                .Left => parsePiece(
                    \\....
                    \\.#..
                    \\##..
                    \\.#..
                ),
            },
            .S => switch (self.facing) {
                .Up => parsePiece(
                    \\....
                    \\.##.
                    \\##..
                    \\....
                ),
                .Right => parsePiece(
                    \\....
                    \\.#..
                    \\.##.
                    \\..#.
                ),
                .Down => parsePiece(
                    \\....
                    \\....
                    \\.##.
                    \\##..
                ),
                .Left => parsePiece(
                    \\....
                    \\#...
                    \\##..
                    \\.#..
                ),
            },
            .Z => switch (self.facing) {
                .Up => parsePiece(
                    \\....
                    \\##..
                    \\.##.
                    \\....
                ),
                .Right => parsePiece(
                    \\....
                    \\..#.
                    \\.##.
                    \\.#..
                ),
                .Down => parsePiece(
                    \\....
                    \\....
                    \\##..
                    \\.##.
                ),
                .Left => parsePiece(
                    \\....
                    \\.#..
                    \\##..
                    \\#...
                ),
            },
            .J => switch (self.facing) {
                .Up => parsePiece(
                    \\....
                    \\#...
                    \\###.
                    \\....
                ),
                .Right => parsePiece(
                    \\....
                    \\.##.
                    \\.#..
                    \\.#..
                ),
                .Down => parsePiece(
                    \\....
                    \\....
                    \\###.
                    \\..#.
                ),
                .Left => parsePiece(
                    \\....
                    \\.#..
                    \\.#..
                    \\##..
                ),
            },
            .L => switch (self.facing) {
                .Up => parsePiece(
                    \\....
                    \\..#.
                    \\###.
                    \\....
                ),
                .Right => parsePiece(
                    \\....
                    \\.#..
                    \\.#..
                    \\.##.
                ),
                .Down => parsePiece(
                    \\....
                    \\....
                    \\###.
                    \\#...
                ),
                .Left => parsePiece(
                    \\....
                    \\##..
                    \\.#..
                    \\.#..
                ),
            },
        };
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
