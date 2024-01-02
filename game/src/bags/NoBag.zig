//! Generates pieces completely at random.

const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;

const root = @import("../root.zig");
const PieceKind = root.pieces.PieceKind;

const Bag = root.bags.Bag;

const Self = @This();

random: Xoroshiro128,

pub fn init(seed: u64) Self {
    return Self{ .random = Xoroshiro128.init(seed) };
}

pub fn next(self: *Self) PieceKind {
    return @enumFromInt(self.random.next() % 7);
}

pub fn setSeed(self: *Self, seed: u64) void {
    self.random = Xoroshiro128.init(seed);
}

pub fn bag(self: Self) Bag {
    _ = self;
    @compileError("TODO: implement");
}
