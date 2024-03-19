//! Draws from a bag of 7 pieces without replacement. The bag is refilled with one of each piece.

const std = @import("std");
const testing = std.testing;
const expect = testing.expect;

const root = @import("../root.zig");
const PieceKind = root.pieces.PieceKind;
const SplitMix64 = root.bags.SplitMix64;

const Self = @This();

pieces: [7]PieceKind = .{ .i, .o, .t, .s, .z, .j, .l },
index: u8 = 7,
random: SplitMix64,

pub fn init(seed: u64) Self {
    return Self{ .random = SplitMix64.init(seed) };
}

/// Returns the next piece in the bag.
pub fn next(self: *Self) PieceKind {
    if (self.index >= self.pieces.len) {
        self.random.random().shuffle(PieceKind, &self.pieces);
        self.index = 0;
    }

    defer self.index += 1;
    return self.pieces[self.index];
}

/// Sets the seed of the bag. The current bag will be discarded and refilled.
pub fn setSeed(self: *Self, seed: u64) void {
    self.* = init(seed);
}

test "7-bag randomizer" {
    var bag = init(1234);

    var actual = std.AutoHashMap(PieceKind, i32).init(testing.allocator);
    defer actual.deinit();

    // Get first 21 pieces
    for (0..21) |_| {
        const piece = bag.next();
        const count = actual.get(piece) orelse 0;
        try actual.put(piece, count + 1);
    }

    // Should have 3 of each piece
    const expected = [_]PieceKind{ .i, .o, .t, .s, .z, .j, .l };
    for (expected) |piece| {
        const count = actual.get(piece) orelse 0;
        try expect(count == 3);
    }
}
