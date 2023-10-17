//! Draws from a bag of 14 pieces without replacement. The bag is refilled with two of each piece.

const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;
const testing = std.testing;
const expect = testing.expect;

const root = @import("../main.zig");
const PieceType = root.pieces.PieceType;

const Bag = root.bags.Bag;
const sourceRandom = root.bags.sourceRandom;
const shuffle = root.bags.shuffle;

const Self = @This();

pieces: [14]PieceType = .{
    .I, .O, .T, .S, .Z, .J, .L,
    .I, .O, .T, .S, .Z, .J, .L,
},
index: u8 = 14,
random: Xoroshiro128,

pub fn init() Self {
    const seed = sourceRandom();
    return Self{ .random = Xoroshiro128.init(seed) };
}

pub fn next(ptr: *anyopaque) PieceType {
    const self: *Self = @ptrCast(@alignCast(ptr));
    if (self.index >= self.pieces.len) {
        shuffle(&self.pieces, &self.random);
        self.index = 0;
    }

    defer self.index += 1;
    return self.pieces[self.index];
}

pub fn bag(self: *Self) Bag {
    return Bag{
        .bag = self,
        .nextFn = Self.next,
    };
}

test "14-bag randomiser" {
    var fb = init();
    var b = fb.bag();

    var actual = std.AutoHashMap(PieceType, i32).init(testing.allocator);
    defer actual.deinit();

    // Get first 28 pieces
    for (0..28) |_| {
        const piece = b.next();
        const count = actual.get(piece) orelse 0;
        try actual.put(piece, count + 1);
    }

    // Should have 4 of each piece
    const expected = [_]PieceType{ .I, .O, .T, .S, .Z, .J, .L };
    for (expected) |piece| {
        const count = actual.get(piece) orelse 0;
        try expect(count == 4);
    }
}
