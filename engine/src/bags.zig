const std = @import("std");
const Random = std.rand.Random;
const testing = std.testing;
const PieceType = @import("pieces.zig").PieceType;

pub const SevenBag = @import("bags/SevenBag.zig");
pub const FourteenBag = @import("bags/FourteenBag.zig");
pub const NoBag = @import("bags/NoBag.zig");
pub const NBag = @import("bags/n_bag.zig").NBag;

/// An interface for bags that generate a random sequence of pieces.
pub const Bag = struct {
    bag: *anyopaque,
    next_fn: *const fn (*anyopaque) PieceType,

    pub fn next(self: *Bag) PieceType {
        return self.next_fn(self.bag);
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

/// Helper function for bag implementaions.
pub fn shuffle(pieces: []PieceType, random: Random) void {
    random.shuffle(PieceType, pieces);
}

test {
    testing.refAllDecls(@This());
}
