const std = @import("std");
const bit_mask = @import("bit_mask.zig");
const BoardMask = bit_mask.BoardMask;
const PieceMask = bit_mask.PieceMask;

pub const Rotation = enum {
    Right,
    Left,
    Down,
};

pub const PieceKind = enum(u8) {
    IUp = 0,
    IRight,
    IDown,
    ILeft,
    OUp,
    ORight,
    ODown,
    OLeft,
    TUp,
    TRight,
    TDown,
    TLeft,
    SUp,
    SRight,
    SDown,
    SLeft,
    ZUp,
    ZRight,
    ZDown,
    ZLeft,
    JUp,
    JRight,
    JDown,
    JLeft,
    LUp,
    LRight,
    LDown,
    LLeft,
};

pub const PiecePos = struct {
    x: u8,
    y: u8,
};

pub const PieceSpawnError = error{BlockOut};

pub const Piece = struct {
    kind: PieceKind,
    pos: PiecePos,

    pub fn spawn(playfield: BoardMask, kind: PieceKind) PieceSpawnError!Piece {
        const pos = switch (kind) {
            .IUp => PiecePos{ .x = 3, .y = 20 },
            .OUp => PiecePos{ .x = 4, .y = 20 },
            .TUp => PiecePos{ .x = 3, .y = 20 },
            .SUp => PiecePos{ .x = 3, .y = 20 },
            .ZUp => PiecePos{ .x = 3, .y = 20 },
            .JUp => PiecePos{ .x = 3, .y = 20 },
            .LUp => PiecePos{ .x = 3, .y = 20 },
        };

        var piece = Piece{
            .kind = kind,
            .pos = pos,
        };
        const bottom = piece.mask().rows[0];

        if (playfield.rows[piece.pos.y] & bottom != 0) {
            return error.BlockOut;
        }
        if (playfield.rows[piece.pos.y - 1] & bottom != 0) {
            piece.pos.y -= 1;
        }

        return piece;
    }

    pub fn mask(self: Piece) PieceMask {
        return switch (self.kind) {
            .IUp, .IDown => parsePiece(
                \\....
                \\....
                \\....
                \\####
            ),
            .IRight, .ILeft => parsePiece(
                \\#...
                \\#...
                \\#...
                \\#...
            ),
            .OUp, .ORight, .ODown, .OLeft => parsePiece(
                \\....
                \\....
                \\##..
                \\##..
            ),
            .TUp => parsePiece(
                \\....
                \\....
                \\.#..
                \\###.
            ),
            .TRight => parsePiece(
                \\....
                \\#...
                \\##..
                \\#...
            ),
            .TDown => parsePiece(
                \\....
                \\....
                \\###.
                \\.#..
            ),
            .TLeft => parsePiece(
                \\....
                \\.#..
                \\##..
                \\.#..
            ),
            .SUp, .SDown => parsePiece(
                \\....
                \\....
                \\.##.
                \\##..
            ),
            .SRight, .SLeft => parsePiece(
                \\....
                \\#...
                \\##..
                \\.#..
            ),
            .ZUp, .ZDown => parsePiece(
                \\....
                \\....
                \\##..
                \\.##.
            ),
            .ZRight, .ZLeft => parsePiece(
                \\....
                \\.#..
                \\##..
                \\#...
            ),
            .JUp => parsePiece(
                \\....
                \\....
                \\#...
                \\###.
            ),
            .JRight => parsePiece(
                \\....
                \\##..
                \\#...
                \\#...
            ),
            .JDown => parsePiece(
                \\....
                \\....
                \\###.
                \\..#.
            ),
            .JLeft => parsePiece(
                \\....
                \\.#..
                \\.#..
                \\##..
            ),
            .LUp => parsePiece(
                \\....
                \\....
                \\..#.
                \\###.
            ),
            .LRight => parsePiece(
                \\....
                \\#...
                \\#...
                \\##..
            ),
            .LDown => parsePiece(
                \\....
                \\....
                \\###.
                \\#...
            ),
            .LLeft => parsePiece(
                \\....
                \\##..
                \\.#..
                \\.#..
            ),
        };
    }
};

fn parsePiece(comptime str: []const u8) PieceMask {
    var result = [_]u16{0} ** 4;
    var lines = std.mem.tokenizeScalar(u8, str, '\n');

    var i: usize = 4;
    while (lines.next()) |line| {
        i -= 1;
        for (0..10) |j| {
            result[i] <<= 1;
            if (j < line.len and line[j] == '#') {
                result[i] |= 1;
            }
        }
    }

    return PieceMask{ .rows = result };
}
