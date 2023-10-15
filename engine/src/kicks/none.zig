const root = @import("../main.zig");
const Position = root.pieces.Position;
const Piece = root.pieces.Piece;
const Rotation = root.kicks.Rotation;

const no_kicks = [0]Position{};

/// No kicks.
pub fn none(_: Piece, _: Rotation) []const Position {
    return &no_kicks;
}
