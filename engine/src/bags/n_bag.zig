const std = @import("std");
const Random = std.rand.Random;
const Xoroshiro128 = std.rand.Xoroshiro128;

const testing = std.testing;
const expect = testing.expect;

const root = @import("../main.zig");
const PieceKind = root.pieces.PieceKind;

const Bag = root.bags.Bag;
const sourceRandom = root.bags.sourceRandom;

/// Draws from a bag of N pieces without replacement.
/// The bag is refilled with all pieces evenly.
pub fn NBag(comptime N: u16) type {
    return struct {
        const Self = @This();

        pieces: [N]PieceKind = undefined,
        index: u16 = N,
        random: Xoroshiro128,

        pub fn init() Self {
            const seed = sourceRandom();
            return Self{ .random = Xoroshiro128.init(seed) };
        }

        fn refill(self: *Self, random: Random) void {
            var pieces: [7]PieceKind = .{ .I, .O, .T, .S, .Z, .J, .L };
            random.shuffle(PieceKind, &pieces);
            for (0..self.pieces.len) |i| {
                self.pieces[i] = pieces[i % 7];
            }
        }

        pub fn next(ptr: *anyopaque) PieceKind {
            const self: *Self = @ptrCast(@alignCast(ptr));
            if (self.index >= self.pieces.len) {
                const random = self.random.random();
                self.refill(random);
                random.shuffle(PieceKind, &self.pieces);
                self.index = 0;
            }

            defer self.index += 1;
            return self.pieces[self.index];
        }

        pub fn setSeed(ptr: *anyopaque, seed: u64) void {
            const self: *Self = @ptrCast(@alignCast(ptr));
            self.index = N;
            self.random = Xoroshiro128.init(seed);
        }

        pub fn bag(self: *Self) Bag {
            return Bag{
                .bag = self,
                .next_fn = Self.next,
                .set_seed_fn = Self.setSeed,
            };
        }
    };
}

test "N-bag (100) randomizer" {
    var nb = NBag(100).init();
    var b = nb.bag();

    var actual = std.AutoHashMap(PieceKind, i32).init(testing.allocator);
    defer actual.deinit();

    // Exhaust the bag
    for (0..100) |_| {
        const piece = b.next();
        const count = actual.get(piece) orelse 0;
        try actual.put(piece, count + 1);
    }

    // Should have 14 or 15 of each piece
    const expected = [_]PieceKind{ .I, .O, .T, .S, .Z, .J, .L };
    for (expected) |piece| {
        const count = actual.get(piece) orelse 0;
        try expect(count == 14 or count == 15);
    }
}
