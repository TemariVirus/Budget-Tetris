const std = @import("std");
const Random = std.rand.Random;
const Xoroshiro128 = std.rand.Xoroshiro128;

const testing = std.testing;
const expect = testing.expect;

const root = @import("../root.zig");
const PieceKind = root.pieces.PieceKind;

const Bag = root.bags.Bag;

/// Draws from a bag of N pieces without replacement. The bag is refilled with
/// all pieces evenly. If `N` is not a multiple of 7, the excess pieces will be
/// drawn randomly from a 7-bag.
pub fn NBag(comptime N: usize) type {
    return struct {
        const Self = @This();

        pieces: [N]PieceKind = undefined,
        index: usize = N,
        random: Xoroshiro128,

        pub fn init(seed: u64) Self {
            return Self{ .random = Xoroshiro128.init(seed) };
        }

        fn refill(self: *Self, random: Random) void {
            var pieces: [7]PieceKind = .{ .I, .O, .T, .S, .Z, .J, .L };
            random.shuffle(PieceKind, &pieces);
            for (0..self.pieces.len) |i| {
                self.pieces[i] = pieces[i % 7];
            }
            random.shuffle(PieceKind, &self.pieces);
        }

        pub fn next(ptr: *Self) PieceKind {
            const self: *Self = @ptrCast(@alignCast(ptr));
            if (self.index >= self.pieces.len) {
                const random = self.random.random();
                self.refill(random);
                self.index = 0;
            }

            defer self.index += 1;
            return self.pieces[self.index];
        }

        pub fn setSeed(ptr: *Self, seed: u64) void {
            const self: *Self = @ptrCast(@alignCast(ptr));
            self.index = N;
            self.random = Xoroshiro128.init(seed);
        }

        pub fn bag(self: Self) Bag {
            _ = self;
            @compileError("TODO: implement");
        }
    };
}

test "N-bag (100) randomizer" {
    var nb = NBag(100).init(42);

    var actual = std.AutoHashMap(PieceKind, i32).init(testing.allocator);
    defer actual.deinit();

    // Exhaust the bag
    for (0..100) |_| {
        const piece = nb.next();
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
