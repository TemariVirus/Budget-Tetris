const std = @import("std");
const testing = std.testing;

const PieceKind = @import("root.zig").pieces.PieceKind;

pub const SevenBag = @import("bags/SevenBag.zig");
pub const FourteenBag = @import("bags/FourteenBag.zig");
pub const NoBag = @import("bags/NoBag.zig");
pub const NBag = @import("bags/n_bag.zig").NBag;

/// A bag that generate a random sequence of pieces.
pub const Bag = struct {
    pub fn next(self: Bag) PieceKind {
        _ = self;
        @compileError("TODO: implement");
    }
};

test {
    testing.refAllDecls(@This());
}
