const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;
const testing = std.testing;
const expect = testing.expect;

const root = @import("../main.zig");
const PieceType = root.pieces.PieceType;

const Bag = root.bags.Bag;
const shuffle = root.bags.shuffle;

const Self = @This();

pieces: [7]PieceType = .{
    .I,
    .O,
    .T,
    .S,
    .Z,
    .J,
    .L,
},
index: u8 = 7,
random: Xoroshiro128,

/// Draws from a bag of 7 pieces without replacement. The bag is refilled with one of each piece.
pub fn init() Self {
    // Probably random enough?
    var seed: u128 = @bitCast(std.time.nanoTimestamp());
    seed *%= 0x6cfc7228c1e15b4883c70617;
    seed +%= 0x1155e8e3c0b3fe3963e841510f42e8e;
    seed ^= seed >> 64;

    return Self{ .random = Xoroshiro128.init(@truncate(seed)) };
}

pub fn next(ptr: *anyopaque) PieceType {
    const self: *Self = @ptrCast(@alignCast(ptr));
    if (self.index >= 7) {
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

test "7-bag randomiser" {
    var b = init().bag();

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
