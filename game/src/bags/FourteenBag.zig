//! Draws from a bag of 14 pieces without replacement. The bag is refilled with two of each piece.

const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;
const testing = std.testing;
const expect = testing.expect;

const root = @import("../root.zig");
const PieceKind = root.pieces.PieceKind;

const Bag = root.bags.Bag;
const sourceRandom = root.bags.sourceRandom;

const Self = @This();

pieces: [14]PieceKind = .{
    .I, .O, .T, .S, .Z, .J, .L,
    .I, .O, .T, .S, .Z, .J, .L,
},
index: u8 = 14,
random: Xoroshiro128,

pub fn init() Self {
    const seed = sourceRandom();
    return Self{ .random = Xoroshiro128.init(seed) };
}

pub fn next(ptr: *anyopaque) PieceKind {
    const self: *Self = @ptrCast(@alignCast(ptr));
    if (self.index >= self.pieces.len) {
        self.random.random().shuffle(PieceKind, &self.pieces);
        self.index = 0;
    }

    defer self.index += 1;
    return self.pieces[self.index];
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

test "14-bag randomizer" {
    var fb = init();
    var b = fb.bag();

    var actual = std.AutoHashMap(PieceKind, i32).init(testing.allocator);
    defer actual.deinit();

    // Get first 28 pieces
    for (0..28) |_| {
        const piece = b.next();
        const count = actual.get(piece) orelse 0;
        try actual.put(piece, count + 1);
    }

    // Should have 4 of each piece
    const expected = [_]PieceKind{ .I, .O, .T, .S, .Z, .J, .L };
    for (expected) |piece| {
        const count = actual.get(piece) orelse 0;
        try expect(count == 4);
    }
}
