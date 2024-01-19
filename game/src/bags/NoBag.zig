//! Generates pieces completely at random.

const Xoroshiro128 = @import("std").rand.Xoroshiro128;
const PieceKind = @import("../root.zig").pieces.PieceKind;

const Self = @This();

random: Xoroshiro128,

pub fn init(seed: u64) Self {
    return Self{ .random = Xoroshiro128.init(seed) };
}

/// Returns the next piece.
pub fn next(self: *Self) PieceKind {
    return @enumFromInt(self.random.next() % 7);
}

/// Sets the seed of the bag.
pub fn setSeed(self: *Self, seed: u64) void {
    self.random = Xoroshiro128.init(seed);
}
