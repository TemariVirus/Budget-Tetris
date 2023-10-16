const std = @import("std");
const Random = std.rand.Xoroshiro128;
const PieceType = @import("pieces.zig").PieceType;

pub const SevenBag = @import("bags/SevenBag.zig");

pub const Bag = struct {
    bag: *anyopaque,
    nextFn: *const fn (*anyopaque) PieceType,

    pub fn next(self: *Bag) PieceType {
        return self.nextFn(self.bag);
    }
};

/// Helper function for bag implementaions. Fisher-Yates shuffle.
pub fn shuffle(pieces: []PieceType, random: *Random) void {
    var i: usize = pieces.len - 1;
    while (i >= 1) : (i -= 1) {
        const swap_index = random.next() % (i + 1);
        std.mem.swap(PieceType, &pieces[i], &pieces[swap_index]);
    }
}
