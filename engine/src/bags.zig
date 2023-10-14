const std = @import("std");
const Random = std.rand.Xoroshiro128;
const PieceKind = @import("pieces.zig").PieceKind;

/// A 7-bag randomiser. Support for other randomisers is planned.
pub const Bag = struct {
    pieces: [7]PieceKind = .{
        .IUp,
        .OUp,
        .TUp,
        .SUp,
        .ZUp,
        .JUp,
        .LUp,
    },
    index: u8 = 7,
    random: Random,

    pub fn setSeed(self: *Bag, seed: u64) void {
        self.random.seed(seed);
    }

    pub fn next(self: *Bag) PieceKind {
        if (self.index >= 7) {
            shuffle(self);
            self.index = 0;
        }

        defer self.index += 1;
        return self.pieces[self.index];
    }
};

pub fn sevenBag() Bag {
    // Probably random enough?
    var seed: u128 = @bitCast(std.time.nanoTimestamp());
    seed *%= 0x6cfc7228c1e15b4883c70617;
    seed +%= 0x1155e8e3c0b3fe3963e841510f42e8e;
    seed ^= seed >> 64;

    return Bag{ .random = Random.init(@truncate(seed)) };
}

fn shuffle(bag: *Bag) void {
    var i: usize = bag.pieces.len - 1;
    while (i >= 1) : (i -= 1) {
        const swap_index = bag.random.next() % (i + 1);
        std.mem.swap(PieceKind, &bag.pieces[i], &bag.pieces[swap_index]);
    }
}
