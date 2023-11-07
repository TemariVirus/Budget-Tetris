//! Generates pieces completely at random. Equivalent to a 1-bag.

const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;

const root = @import("../main.zig");
const PieceKind = root.pieces.PieceKind;

const Bag = root.bags.Bag;
const sourceRandom = root.bags.sourceRandom;

const Self = @This();

random: Xoroshiro128,

pub fn init() Self {
    const seed = sourceRandom();
    return Self{ .random = Xoroshiro128.init(seed) };
}

pub fn next(ptr: *anyopaque) PieceKind {
    const self: *Self = @ptrCast(@alignCast(ptr));
    return @enumFromInt(self.random.next() % 7);
}

pub fn setSeed(ptr: *anyopaque, seed: u64) void {
    const self: *Self = @ptrCast(@alignCast(ptr));
    self.random = Xoroshiro128.init(seed);
}

pub fn bag(self: *Self) Bag {
    return Bag{
        .bag = self,
        .next_fn = Self.next,
        .set_seed_fn = Self.setSeed,
    };
}
