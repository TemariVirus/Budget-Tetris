const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;

const View = @import("nterm").View;

const root = @import("root.zig");
const KickFn = root.kicks.KickFn;
const Settings = root.GameSettings;

pub fn Match(comptime BagImpl: type, comptime kicks: KickFn) type {
    return struct {
        // Players and bots are likely to maintain references to the player objects, so we
        // they must NOT be moved after initialisation.
        players: []Player,

        const Self = @This();
        pub const Player = root.Player(BagImpl, kicks);

        pub fn init(allocator: Allocator, player_count: usize, bag: BagImpl, default_settings: Settings) !Self {
            assert(player_count > 0);

            const size = optimalSize(player_count);
            const players = try allocator.alloc(Player, player_count);
            for (players, 0..) |*player, i| {
                const row = i / size.width;
                const col = i % size.width;
                player.* = Player.init(
                    "",
                    .{ .context = bag },
                    View{
                        .left = @intCast(1 + (Player.DISPLAY_W + 1) * col),
                        .top = @intCast((Player.DISPLAY_H + 1) * row),
                        .width = Player.DISPLAY_W,
                        .height = Player.DISPLAY_H,
                    },
                    default_settings,
                );
            }

            return Self{ .players = players };
        }

        fn optimalSize(player_count: usize) struct { width: u16, height: u16 } {
            const PLAYER_ASPECT = @as(f32, @floatFromInt(Player.DISPLAY_W + 1)) /
                @as(f32, @floatFromInt(Player.DISPLAY_H + 1)) / 2.0;
            const DESIRED_ASPECT: f32 = (16.0 / 9.0) / PLAYER_ASPECT;

            var width: usize = 1;
            var height: usize = 1;
            while (width * height < player_count) {
                if (@as(f32, @floatFromInt(width)) <
                    @as(f32, @floatFromInt(height)) * DESIRED_ASPECT)
                {
                    width += 1;
                } else {
                    height += 1;
                }
            }

            return .{ .width = @intCast(width), .height = @intCast(height) };
        }

        pub fn deinit(self: Self, allocator: Allocator) void {
            allocator.free(self.players);
        }

        pub fn tick(self: Self, nanoseconds: u64) void {
            for (self.players, 0..) |*player, i| {
                player.tick(nanoseconds, i, self.players);
            }
        }

        pub fn restart(self: Self) void {
            const seed = std.crypto.random.int(u64);
            for (self.players) |*player| {
                player.restart(seed);
            }
        }

        pub fn draw(self: Self) !void {
            for (self.players) |player| {
                try player.draw();
            }
        }
    };
}
