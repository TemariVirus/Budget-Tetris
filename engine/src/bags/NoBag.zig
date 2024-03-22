//! Generates pieces completely at random.

const root = @import("../root.zig");
const SplitMix64 = root.bags.SplitMix64;
const PieceKind = root.pieces.PieceKind;

const Self = @This();

random: SplitMix64,

pub fn init(seed: u64) Self {
    return Self{ .random = SplitMix64.init(seed) };
}

/// Returns the next piece.
pub fn next(self: *Self) PieceKind {
    return @enumFromInt(self.random.next() % 7);
}

/// Sets the seed of the bag.
pub fn setSeed(self: *Self, seed: u64) void {
    self.* = init(seed);
}
