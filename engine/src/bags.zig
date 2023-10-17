const std = @import("std");
const Xoroshiro128 = std.rand.Xoroshiro128;
const testing = std.testing;
const PieceType = @import("pieces.zig").PieceType;

pub const SevenBag = @import("bags/SevenBag.zig");
pub const FourteenBag = @import("bags/FourteenBag.zig");
pub const NoBag = @import("bags/NoBag.zig");
pub const NBag = @import("bags/n_bag.zig").NBag;

/// An interface for bags that generate a random sequence of pieces.
pub const Bag = struct {
    bag: *anyopaque,
    nextFn: *const fn (*anyopaque) PieceType,

    pub fn next(self: *Bag) PieceType {
        return self.nextFn(self.bag);
    }
};

/// Helper function for bag implementaions. Returns a random u64 based on the time.
pub fn sourceRandom() u64 {
    // Probably random enough?
    var seed: u128 = @bitCast(std.time.nanoTimestamp());
    seed *%= 0x6cfc7228c1e15b4883c70617;
    seed +%= 0x1155e8e3c0b3fe3963e841510f42e8e;
    seed ^= seed >> 64;
    return @truncate(seed);
}

/// Helper function for bag implementaions. Fisher-Yates shuffle.
pub fn shuffle(pieces: []PieceType, random: *Xoroshiro128) void {
    var i: usize = pieces.len - 1;
    while (i >= 1) : (i -= 1) {
        const swap_index = random.next() % (i + 1);
        std.mem.swap(PieceType, &pieces[i], &pieces[swap_index]);
    }
}

test {
    testing.refAllDecls(@This());
}
