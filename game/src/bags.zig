const std = @import("std");
const testing = std.testing;

const PieceKind = @import("root.zig").pieces.PieceKind;

pub const SevenBag = @import("bags/SevenBag.zig");
pub const FourteenBag = @import("bags/FourteenBag.zig");
pub const NoBag = @import("bags/NoBag.zig");
pub const NBag = @import("bags/n_bag.zig").NBag;

/// A bag that generate a random sequence of pieces.
pub const Bag = struct {
    /// Fills `pieces` with the next sequence of pieces from the bag. If `advance`
    /// is `true`, the bag will advance to the next sequence of pieces. Otherwise,
    /// the state of the bag will remain unchanged.
    pub fn next(self: Bag, pieces: []PieceKind, advance: bool) void {
        _ = self;
        _ = pieces;
        _ = advance;

        @compileError("TODO: implement");
    }

    /// Sets the seed of the bag. The bag will be refilled, if applicable.
    pub fn setSeed(self: Bag, seed: u64) Bag {
        _ = self;
        _ = seed;

        @compileError("TODO: implement");
    }
};

test {
    testing.refAllDecls(@This());
}
