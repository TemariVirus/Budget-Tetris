const std = @import("std");
const testing = std.testing;
const nterm = @import("nterm");
const Allocator = std.mem.Allocator;

pub const attack = @import("attack.zig");
pub const bags = @import("bags.zig");
pub const bit_masks = @import("bit_masks.zig");
pub const kicks = @import("kicks.zig");
pub const pieces = @import("pieces.zig");
pub const tbp = @import("tbp.zig");

pub const Game = @import("Game.zig").Game;
pub const GameState = @import("GameState.zig").GameState;
pub const PeriodicTrigger = @import("PeriodicTrigger.zig");

// TODO: Load settings to config file
pub const Settings = struct {
    pub const Stat = enum {
        /// Attack Per Line.
        APL,
        /// Attack Per Minute.
        APM,
        /// Attack Per Piece.
        APP,
        /// Finesse.
        Finesse,
        /// Keys Pressed.
        Keys,
        /// Keys Per Piece.
        KPP,
        /// Level. Calculated as `(Lines / 10) + 1`.
        Level,
        /// Lines cleared.
        Lines,
        /// Pieces Per Second.
        PPS,
        /// Lines of garbage received.
        Received,
        /// Score.
        Score,
        /// Lines of garbage sent; Attack.
        Sent,
        /// Time elaspsed since start.
        Time,
        /// VS Score. Calculated as `100 * (Attack + Garbage cleared) / Seconds`.
        VsScore,
    };

    g: f32 = 0.025 * 60, // Multiply by framerate before passing to Game
    soft_g: f32 = 40.0,
    autolock_grace: u8 = 15,
    lock_delay: u32 = 500,
    clear_erase_dalay: u32 = 1000,
    show_next_count: u3 = 6,
    display_stats: []const Stat,
    attack_table: attack.AttackTable = .{
        .b2b = &.{ 0, 0, 1 },
        .clears = .{ 0, 0, 1, 2, 4 },
        .combo = &.{ 0, 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 },
        .perfect_clear = .{ 10, 10, 10, 10 },
        .t_spin = .{ 0, 2, 4, 6 },
    },
};

test {
    testing.refAllDecls(@This());
}
