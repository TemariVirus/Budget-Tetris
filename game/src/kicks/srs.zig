const root = @import("../root.zig");
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

const ccw_kicks = [4][4]Position{
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

const ccw_i_kicks = [4][4]Position{
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
        .quarter_cw => switch (piece.kind) {
            .i => cw_i_kicks[@intFromEnum(piece.facing)],
            .o => no_kicks,
            .t, .s, .z, .j, .l => cw_kicks[@intFromEnum(piece.facing)],
        },
        .half => no_kicks,
        .quarter_ccw => switch (piece.kind) {
            .i => ccw_i_kicks[@intFromEnum(piece.facing)],
            .o => no_kicks,
            .t, .s, .z, .j, .l => ccw_kicks[@intFromEnum(piece.facing)],
        },
    };
}
