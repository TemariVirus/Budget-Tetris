//! Draws from a bag of 14 pieces without replacement. The bag is refilled with two of each piece.

const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;
const testing = std.testing;
const expect = testing.expect;

const PieceKind = @import("../root.zig").pieces.PieceKind;

const Self = @This();

pieces: [14]PieceKind = .{
    .i, .o, .t, .s, .z, .j, .l,
    .i, .o, .t, .s, .z, .j, .l,
},
index: u8 = 14,
random: Xoroshiro128,

pub fn init(seed: u64) Self {
    return Self{ .random = Xoroshiro128.init(seed) };
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
    self.random = Xoroshiro128.init(seed);
}

test "14-bag randomizer" {
    var fb = init(7216);

    var actual = std.AutoHashMap(PieceKind, i32).init(testing.allocator);
    defer actual.deinit();

    // Get first 28 pieces
    for (0..28) |_| {
        const piece = fb.next();
        const count = actual.get(piece) orelse 0;
        try actual.put(piece, count + 1);
    }

    // Should have 4 of each piece
    const expected = [_]PieceKind{ .i, .o, .t, .s, .z, .j, .l };
    for (expected) |piece| {
        const count = actual.get(piece) orelse 0;
        try expect(count == 4);
    }
}
