const root = @import("../root.zig");
const Position = root.pieces.Position;
const Piece = root.pieces.Piece;
const Rotation = root.kicks.Rotation;
const srs = root.kicks.srs;

const no_kicks = [0]Position{};

const double_kicks = [4][5]Position{
    [5]Position{
        Position{ .x = 0, .y = 1 },
        Position{ .x = 1, .y = 1 },
        Position{ .x = -1, .y = 1 },
        Position{ .x = 1, .y = 0 },
        Position{ .x = -1, .y = 0 },
    },
    [5]Position{
        Position{ .x = 1, .y = 0 },
        Position{ .x = 1, .y = 2 },
        Position{ .x = 1, .y = 1 },
        Position{ .x = 0, .y = 2 },
        Position{ .x = 0, .y = 1 },
    },
    [5]Position{
        Position{ .x = 0, .y = -1 },
        Position{ .x = -1, .y = -1 },
        Position{ .x = 1, .y = -1 },
        Position{ .x = -1, .y = 0 },
        Position{ .x = 1, .y = 0 },
    },
    [5]Position{
        Position{ .x = -1, .y = 0 },
        Position{ .x = -1, .y = 2 },
        Position{ .x = -1, .y = 1 },
        Position{ .x = 0, .y = 2 },
        Position{ .x = 0, .y = 1 },
    },
};

/// The modified SRS kicks that Tetr.io uses. Introduces some 180 kicks.
pub fn srs180(piece: Piece, rotation: Rotation) []const Position {
    if (rotation == .Double) {
        return &switch (piece.kind) {
            .I, .O => no_kicks,
            .T, .S, .Z, .J, .L => double_kicks[@intFromEnum(piece.facing)],
        };
    }

    return srs(piece, rotation);
}
