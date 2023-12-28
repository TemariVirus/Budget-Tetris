const std = @import("std");
const testing = std.testing;
const nterm = @import("nterm");
const Allocator = std.mem.Allocator;

pub const bags = @import("bags.zig");
pub const bit_masks = @import("bit_masks.zig");
pub const kicks = @import("kicks.zig");
pub const pieces = @import("pieces.zig");

pub const Game = @import("Game.zig");
pub const GameState = @import("GameState.zig");

pub const TSpin = enum {
    None,
    Mini,
    Full,
};

pub const ClearInfo = struct {
    b2b: bool,
    cleared: u3,
    pc: bool,
    t_spin: TSpin,
};

test {
    testing.refAllDecls(@This());
}
