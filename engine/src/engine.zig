const pieces = @import("pieces.zig");
const PieceKind = pieces.PieceKind;
const PiecePos = pieces.PiecePos;
const Rotation = @import("kicks.zig").Rotation;
const Bag = @import("bags.zig").Bag;

pub fn Engine(
    kicks: *const fn (PieceKind, Rotation) []PiecePos,
) type {
    _ = kicks;
    return struct {
        const Self = @This();

        bag: Bag,
    };
}
