const root = @import("../main.zig");
const Position = root.pieces.Position;
const Piece = root.pieces.Piece;
const Rotation = root.kicks.Rotation;

const no_kicks = [0]Position{};

const cw_kicks = [4][4]Position{
    [4]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = -1, .y = 1 },
        Position{ .x = 0, .y = -2 },
        Position{ .x = -1, .y = -2 },
    },
    [4]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = 1, .y = -1 },
        Position{ .x = 0, .y = 2 },
        Position{ .x = 1, .y = 2 },
    },
    [4]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = 1, .y = 1 },
        Position{ .x = 0, .y = -2 },
        Position{ .x = 1, .y = -2 },
    },
    [4]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = -1, .y = -1 },
        Position{ .x = 0, .y = 2 },
        Position{ .x = -1, .y = 2 },
    },
};

const acw_kicks = [4][4]Position{
    [4]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = 1, .y = 1 },
        Position{ .x = 0, .y = -2 },
        Position{ .x = 1, .y = -2 },
    },
    [4]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = 1, .y = -1 },
        Position{ .x = 0, .y = 2 },
        Position{ .x = 1, .y = 2 },
    },
    [4]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = -1, .y = 1 },
        Position{ .x = 0, .y = -2 },
        Position{ .x = -1, .y = -2 },
    },
    [4]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = -1, .y = -1 },
        Position{ .x = 0, .y = 2 },
        Position{ .x = -1, .y = 2 },
    },
};

const cw_i_kicks = [4][4]Position{
    [4]Position{
        Position{ .x = -2, .y = 0 },
        Position{ .x = 1, .y = 0 },
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

const acw_i_kicks = [4][4]Position{
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
    [4]Position{
        Position{ .x = -2, .y = 0 },
        Position{ .x = 1, .y = 0 },
        Position{ .x = -2, .y = -1 },
        Position{ .x = 1, .y = 2 },
    },
};

/// Classic SRS kicks. No 180 kicks.
pub fn srs(piece: Piece, rotation: Rotation) []const Position {
    return &switch (rotation) {
        .Cw => switch (piece.type) {
            .I => cw_i_kicks[@intFromEnum(piece.facing)],
            .O => no_kicks,
            .T, .S, .Z, .J, .L => cw_kicks[@intFromEnum(piece.facing)],
        },
        .Double => no_kicks,
        .ACw => switch (piece.type) {
            .I => acw_i_kicks[@intFromEnum(piece.facing)],
            .O => no_kicks,
            .T, .S, .Z, .J, .L => acw_kicks[@intFromEnum(piece.facing)],
        },
    };
}
