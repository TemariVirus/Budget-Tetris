const root = @import("../root.zig");
const Position = root.pieces.Position;
const Piece = root.pieces.Piece;
const Rotation = root.kicks.Rotation;
const srs180 = @import("srs180.zig").srs180;

const no_kicks = [0]Position{};

const cw_i_kicks = [4][4]Position{
    [4]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = -2, .y = 0 },
        Position{ .x = -2, .y = -1 },
        Position{ .x = 1, .y = 2 },
    },
    [4]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = 2, .y = 0 },
        Position{ .x = -1, .y = 2 },
        Position{ .x = 2, .y = -1 },
    },
    [4]Position{
        Position{ .x = 2, .y = 0 },
        Position{ .x = -1, .y = 0 },
        Position{ .x = 2, .y = 1 },
        Position{ .x = -1, .y = -2 },
    },
    [4]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = -2, .y = 0 },
        Position{ .x = 1, .y = -2 },
        Position{ .x = -2, .y = 1 },
    },
};

const double_i_kicks = [4][1]Position{
    [1]Position{
        Position{ .x = 0, .y = 1 },
    },
    [1]Position{
        Position{ .x = 1, .y = 0 },
    },
    [1]Position{
        Position{ .x = 0, .y = -1 },
    },
    [1]Position{
        Position{ .x = -1, .y = 0 },
    },
};

const CCw_i_kicks = [4][4]Position{
    [4]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = 2, .y = 0 },
        Position{ .x = 2, .y = -1 },
        Position{ .x = -1, .y = 2 },
    },
    [4]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = 2, .y = 0 },
        Position{ .x = -1, .y = -2 },
        Position{ .x = 2, .y = 1 },
    },
    [4]Position{
        Position{ .x = -2, .y = 0 },
        Position{ .x = 1, .y = 0 },
        Position{ .x = -2, .y = 1 },
        Position{ .x = 1, .y = -2 },
    },
    [4]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = -2, .y = 0 },
        Position{ .x = 1, .y = 2 },
        Position{ .x = -2, .y = -1 },
    },
};

/// Tetr.io's SRS+ kicks.
pub fn srsPlus(piece: Piece, rotation: Rotation) []const Position {
    if (piece.kind == .I) {
        return &switch (rotation) {
            .Cw => cw_i_kicks[@intFromEnum(piece.facing)],
            .Double => double_i_kicks[@intFromEnum(piece.facing)],
            .CCw => CCw_i_kicks[@intFromEnum(piece.facing)],
        };
    }

    return srs180(piece, rotation);
}
