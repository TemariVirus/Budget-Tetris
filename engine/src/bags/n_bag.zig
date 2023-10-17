const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;
const testing = std.testing;
const expect = testing.expect;

const root = @import("../main.zig");
const PieceType = root.pieces.PieceType;

const Bag = root.bags.Bag;
const sourceRandom = root.bags.sourceRandom;
const shuffle = root.bags.shuffle;

/// Draws from a bag of N pieces without replacement.
/// The bag is refilled with all pieces evenly.
pub fn NBag(comptime N: u16) type {
    return struct {
        const Self = @This();

        pieces: [N]PieceType = undefined,
        index: u16 = N,
        random: Xoroshiro128,

        pub fn init() Self {
            const seed = sourceRandom();
            return Self{ .random = Xoroshiro128.init(seed) };
        }

        fn refill(self: *Self) void {
            var pieces: [7]PieceType = .{ .I, .O, .T, .S, .Z, .J, .L };
            shuffle(&pieces, &self.random);
            for (0..self.pieces.len) |i| {
                self.pieces[i] = pieces[i % 7];
            }
        }

        pub fn next(ptr: *anyopaque) PieceType {
            const self: *Self = @ptrCast(@alignCast(ptr));
            if (self.index >= self.pieces.len) {
                self.refill();
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
    };
}

test "N-bag (100) randomiser" {
    var nb = NBag(100).init();
    var b = nb.bag();

    var actual = std.AutoHashMap(PieceType, i32).init(testing.allocator);
    defer actual.deinit();

    // Exhaust the bag
    for (0..100) |_| {
        const piece = b.next();
        const count = actual.get(piece) orelse 0;
        try actual.put(piece, count + 1);
    }

    // Should have 14 or 15 of each piece
    const expected = [_]PieceType{ .I, .O, .T, .S, .Z, .J, .L };
    for (expected) |piece| {
        const count = actual.get(piece) orelse 0;
        try expect(count == 14 or count == 15);
    }
}
