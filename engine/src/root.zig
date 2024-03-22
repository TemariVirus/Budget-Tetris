const std = @import("std");
const testing = std.testing;
const nterm = @import("nterm");
const Allocator = std.mem.Allocator;

pub const attack = @import("attack.zig");
pub const bags = @import("bags.zig");
pub const bit_masks = @import("bit_masks.zig");
pub const kicks = @import("kicks.zig");
pub const pieces = @import("pieces.zig");
pub const sound = @import("sound.zig");

pub const Match = @import("Match.zig").Match;
pub const Player = @import("Player.zig").Player;
pub const GameState = @import("GameState.zig").GameState;
pub const PeriodicTrigger = @import("PeriodicTrigger.zig");

// TODO: Load settings to config file
pub const GameSettings = struct {
    pub const Stat = enum {
        /// Attack Per Line.
        apl,
        /// Attack Per Minute.
        apm,
        /// Attack Per Piece.
        app,
        /// Finesse.
        finesse,
        /// Keys Pressed.
        keys,
        /// Keys Per Piece.
        kpp,
        /// Level. Calculated as `(lines / 10) + 1`.
        level,
        /// Lines cleared.
        lines,
        /// Pieces Per Second.
        pps,
        /// Lines of garbage received.
        received,
        /// Score.
        score,
        /// Lines of garbage sent; Attack.
        sent,
        /// Time elaspsed since start.
        time,
        /// VS Score. Calculated as `100 * (sent + garbage cleared) / seconds`.
        vs_score,
    };

    // Gravity assumes 60 FPS
    g: f32 = 0.025 * 60,
    /// Gravity for soft drops.
    soft_g: f32 = 400 * 60,
    /// Whether to use lockout.
    use_lockout: bool = false,
    /// The number of moves before a piece autolocks.
    autolock_grace: u8 = 15,
    /// The delay before locking a piece, in milliseconds.
    lock_delay: u32 = 500,
    /// The length of the clear animation, in milliseconds.
    clear_delay: u32 = 0,
    /// The delay before erasing clear info, in milliseconds.
    clear_erase_dalay: u32 = 1000,
    /// The delay before garbage is sent, in milliseconds.
    garbage_delay: u32 = 500,
    /// The max amount of garbage that can be received at once.
    garbage_cap: u16 = 8,
    /// The number of next pieces to show.
    show_next_count: u3 = 6,
    /// The stats to display.
    display_stats: [3]Stat = .{ .pps, .app, .vs_score },
    /// The attack table to use.
    attack_table: attack.AttackTable = .{
        .b2b = &.{ 0, 0, 1 },
        .clears = .{ 0, 0, 1, 2, 4 },
        .combo = &.{ 0, 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 },
        .perfect_clear = .{ 10, 10, 10, 10 },
        .t_spin = .{ 0, 2, 4, 6 },
    },
    /// The target mode to use.
    target_mode: attack.TargetMode = .random_but_self,
};

test {
    testing.refAllDecls(@This());
}
