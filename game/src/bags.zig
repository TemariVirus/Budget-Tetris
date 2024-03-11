//! A bag generates the sequence of pieces used in Tetris games.
//! A bag must be a struct that has the following 2 functions:
//! - `pub fn next(self: *Self) PieceKind`
//! - `pub fn setSeed(self: *Self, seed: u64) void`
//!
//! This module provides several example implementations.

const std = @import("std");
const testing = std.testing;

const PieceKind = @import("root.zig").pieces.PieceKind;

pub const SevenBag = @import("bags/SevenBag.zig");
pub const FourteenBag = @import("bags/FourteenBag.zig");
pub const NoBag = @import("bags/NoBag.zig");
pub const NBag = @import("bags/n_bag.zig").NBag;

pub fn Bag(comptime T: type) type {
    return struct {
        context: T,

        const Self = @This();

        pub fn init(seed: u64) Self {
            return Self{ .context = T.init(seed) };
        }

        /// Returns the next piece in the bag.
        pub fn next(self: *Self) PieceKind {
            return self.context.next();
        }

        /// Sets the seed of the bag. The current bag will be discarded and refilled.
        pub fn setSeed(self: *Self, seed: u64) void {
            self.context.setSeed(seed);
        }
    };
}

test {
    testing.refAllDecls(@This());
}
