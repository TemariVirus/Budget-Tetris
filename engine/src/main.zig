const std = @import("std");
const testing = std.testing;
const Allocator = std.mem.Allocator;

pub const bags = @import("bags.zig");
pub const bit_masks = @import("bit_masks.zig");
pub const input = @import("input.zig");
pub const kicks = @import("kicks.zig");
pub const pieces = @import("pieces.zig");
pub const terminal = @import("terminal.zig");

pub const Game = @import("Game.zig");
pub const GameState = @import("GameState.zig");

pub const TSpin = enum {
    None,
    Mini,
    Full,
};

pub const ClearInfo = struct {
    b2b: bool,
    cleared: u8,
    pc: bool,
    t_spin: TSpin,
};

pub fn init(allocator: Allocator, width: u16, height: u16) !void {
    try terminal.init(allocator, width, height);
    try input.init(allocator);
}

pub fn deinit() void {
    terminal.deinit();
    input.deinit();
}

test {
    testing.refAllDecls(@This());
}
