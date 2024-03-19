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

/// Stolen from the standard library's `std.rand.SplitMix64`, but with `fill`
/// and `random` functions.
pub const SplitMix64 = struct {
    s: u64,

    pub fn init(seed: u64) SplitMix64 {
        return SplitMix64{ .s = seed };
    }

    pub fn random(self: *SplitMix64) std.Random {
        return std.Random.init(self, fill);
    }

    pub fn next(self: *SplitMix64) u64 {
        self.s +%= 0x9e3779b97f4a7c15;

        var z = self.s;
        z = (z ^ (z >> 30)) *% 0xbf58476d1ce4e5b9;
        z = (z ^ (z >> 27)) *% 0x94d049bb133111eb;
        return z ^ (z >> 31);
    }

    pub fn fill(self: *SplitMix64, buf: []u8) void {
        var i: usize = 0;
        const aligned_len = buf.len - (buf.len & 7);

        // Complete 8 byte segments.
        while (i < aligned_len) : (i += 8) {
            var n = self.next();
            comptime var j: usize = 0;
            inline while (j < 8) : (j += 1) {
                buf[i + j] = @as(u8, @truncate(n));
                n >>= 8;
            }
        }

        // Remaining. (cuts the stream)
        if (i != buf.len) {
            var n = self.next();
            while (i < buf.len) : (i += 1) {
                buf[i] = @as(u8, @truncate(n));
                n >>= 8;
            }
        }
    }
};

test {
    testing.refAllDecls(@This());
}
