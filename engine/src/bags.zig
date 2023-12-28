const std = @import("std");
const Random = std.rand.Random;
const testing = std.testing;
const PieceKind = @import("pieces.zig").PieceKind;

pub const SevenBag = @import("bags/SevenBag.zig");
pub const FourteenBag = @import("bags/FourteenBag.zig");
pub const NoBag = @import("bags/NoBag.zig");
pub const NBag = @import("bags/n_bag.zig").NBag;

/// An interface for bags that generate a random sequence of pieces.
pub const Bag = struct {
    bag: *anyopaque,
    // TODO: try comptime functions
    next_fn: *const fn (*anyopaque) PieceKind,
    set_seed_fn: *const fn (*anyopaque, u64) void,

    /// Returns the next piece in the bag and advances the bag.
    pub fn next(self: *Bag) PieceKind {
        return self.next_fn(self.bag);
    }

    /// Sets the new seed for the random number generator. The bag will be
    /// refilled, if applicable.
    pub fn setSeed(self: *Bag, seed: u64) void {
        return self.set_seed_fn(self.bag, seed);
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
pub fn shuffle(pieces: []PieceKind, random: Random) void {
    random.shuffle(PieceKind, pieces);
}

test {
    testing.refAllDecls(@This());
}
