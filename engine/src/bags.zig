const std = @import("std");
const Random = std.rand.Xoroshiro128;
const PieceType = @import("pieces.zig").PieceType;

const testing = std.testing;
const expect = testing.expect;

/// A 7-bag randomiser. Support for other randomisers is planned.
pub const Bag = struct {
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
    random: Random,

    pub fn setSeed(self: *Bag, seed: u64) void {
        self.random.seed(seed);
    }

    pub fn next(self: *Bag) PieceType {
        if (self.index >= 7) {
            shuffle(self);
            self.index = 0;
        }

        defer self.index += 1;
        return self.pieces[self.index];
    }

    // Will be useful when the bag is made generic.
    pub fn clone(self: Bag) Bag {
        return self;
    }
};

fn shuffle(bag: *Bag) void {
    var i: usize = bag.pieces.len - 1;
    while (i >= 1) : (i -= 1) {
        const swap_index = bag.random.next() % (i + 1);
        std.mem.swap(PieceType, &bag.pieces[i], &bag.pieces[swap_index]);
    }
}

pub fn sevenBag() Bag {
    // Probably random enough?
    var seed: u128 = @bitCast(std.time.nanoTimestamp());
    seed *%= 0x6cfc7228c1e15b4883c70617;
    seed +%= 0x1155e8e3c0b3fe3963e841510f42e8e;
    seed ^= seed >> 64;

    return Bag{ .random = Random.init(@truncate(seed)) };
}

test "7-bag randomiser" {
    var bag = sevenBag();

    var actual = std.AutoHashMap(PieceType, i32).init(testing.allocator);
    defer actual.deinit();

    // Get first 21 pieces
    for (0..21) |_| {
        const piece = bag.next();
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
