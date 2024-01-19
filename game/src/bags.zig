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

test {
    testing.refAllDecls(@This());
}
