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
pub const tbp = @import("tbp.zig");

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
    soft_g: f32 = 400 * 60,
    // TODO: enforce
    use_lockout: bool = false,
    autolock_grace: u8 = 15,
    lock_delay: u32 = 500,
    clear_erase_dalay: u32 = 1000,
    garbage_delay: u32 = 500,
    garbage_cap: u16 = 8,
    show_next_count: u3 = 6,
    display_stats: [3]Stat = .{ .pps, .app, .vs_score },
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
