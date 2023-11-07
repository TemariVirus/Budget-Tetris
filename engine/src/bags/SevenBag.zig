//! Draws from a bag of 7 pieces without replacement. The bag is refilled with one of each piece.

const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;
const testing = std.testing;
const expect = testing.expect;

const root = @import("../main.zig");
const PieceType = root.pieces.PieceType;

const Bag = root.bags.Bag;
const sourceRandom = root.bags.sourceRandom;

const Self = @This();

pieces: [7]PieceType = .{ .I, .O, .T, .S, .Z, .J, .L },
index: u8 = 7,
random: Xoroshiro128,

pub fn init() Self {
    const seed = sourceRandom();
    return Self{ .random = Xoroshiro128.init(seed) };
}

pub fn next(ptr: *anyopaque) PieceType {
    const self: *Self = @ptrCast(@alignCast(ptr));
    if (self.index >= self.pieces.len) {
        self.random.random().shuffle(PieceType, &self.pieces);
        self.index = 0;
    }

    defer self.index += 1;
    return self.pieces[self.index];
}

pub fn setSeed(ptr: *anyopaque, seed: u64) void {
    const self: *Self = @ptrCast(@alignCast(ptr));
    self.index = 7;
    self.random = Xoroshiro128.init(seed);
}

pub fn bag(self: *Self) Bag {
    return Bag{
        .bag = self,
        .next_fn = Self.next,
        .set_seed_fn = Self.setSeed,
    };
}

test "7-bag randomizer" {
    var sb = init();
    var b = sb.bag();

    var actual = std.AutoHashMap(PieceType, i32).init(testing.allocator);
    defer actual.deinit();

    // Get first 21 pieces
    for (0..21) |_| {
        const piece = b.next();
        const count = actual.get(piece) orelse 0;
        try actual.put(piece, count + 1);
    }

    // Should have 3 of each piece
    const expected = [_]PieceType{ .I, .O, .T, .S, .Z, .J, .L };
    for (expected) |piece| {
        const count = actual.get(piece) orelse 0;
        try expect(count == 3);
    }
}
