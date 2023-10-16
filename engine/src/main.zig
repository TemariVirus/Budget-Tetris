const std = @import("std");
const testing = std.testing;

pub const bags = @import("bags.zig");
pub const bit_masks = @import("bit_masks.zig");
pub const Game = @import("Game.zig");
pub const kicks = @import("kicks.zig");
pub const pieces = @import("pieces.zig");

pub const Move = enum {
    Left,
    Right,
    DASLeft,
    DASRight,
    Cw,
    ACw,
    Double,
    Hold,
    Drop,
};

pub const TSpin = enum {
    None,
    Mini,
    Full,
};

pub const ClearInfo = struct {
    b2b: bool,
    clears: u8,
    pc: bool,
    pos: pieces.Position,
    trash: u16,
    tSpin: TSpin,
};

test {
    testing.refAllDecls(@This());
}
