//! Generates pieces completely at random. Equivalent to a 1-bag.

const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;

const root = @import("../main.zig");
const PieceType = root.pieces.PieceType;

const Bag = root.bags.Bag;
const sourceRandom = root.bags.sourceRandom;

const Self = @This();

random: Xoroshiro128,

pub fn init() Self {
    const seed = sourceRandom();
    return Self{ .random = Xoroshiro128.init(seed) };
}

pub fn next(ptr: *anyopaque) PieceType {
    const self: *Self = @ptrCast(@alignCast(ptr));
    return @enumFromInt(self.random.next() % 7);
}

pub fn bag(self: *Self) Bag {
    return Bag{
        .bag = self,
        .nextFn = Self.next,
    };
}
