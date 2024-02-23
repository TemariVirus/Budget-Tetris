const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;

const View = @import("nterm").View;

const root = @import("root.zig");
const KickFn = root.kicks.KickFn;
const Settings = root.GameSettings;

fn BoundedArray(comptime T: type) type {
    return struct {
        items: []T,
        len: usize,

        const Self = @This();

        pub fn init(allocator: Allocator, len: usize) !Self {
            const items = try allocator.alloc(T, len);
            return Self{ .items = items, .len = len };
        }

        pub fn deinit(self: Self, allocator: Allocator) void {
            allocator.free(self.items);
        }

        pub fn swapRemove(self: *Self, index: usize) void {
            assert(index < self.len);
            self.len -= 1;
            std.mem.swap(T, &self.items[index], &self.items[self.len]);
        }

        pub fn slice(self: Self) []T {
            return self.items[0..self.len];
        }
    };
}

pub fn Match(comptime Bag: type, comptime kicks: KickFn) type {
    const Player = root.Player(Bag, kicks);

    return struct {
        alive_indices: BoundedArray(usize),
        players: []Player,

        const Self = @This();

        pub fn init(allocator: Allocator, player_count: usize, bag: Bag, default_settings: Settings) !Self {
            assert(player_count > 0);

            const alive_indices = try BoundedArray(usize).init(allocator, player_count);
            for (alive_indices.items, 0..) |*index, i| {
                index.* = i;
            }

            const size = optimalSize(player_count);
            const players = try allocator.alloc(Player, player_count);
            for (players, 0..) |*player, i| {
                const row = i / size.width;
                const col = i % size.width;
                player.* = Player.init(
                    "",
                    bag,
                    View{
                        .left = @intCast(1 + (Player.DISPLAY_W + 1) * col),
                        .top = @intCast((Player.DISPLAY_H + 1) * row),
                        .width = Player.DISPLAY_W,
                        .height = Player.DISPLAY_H,
                    },
                    default_settings,
                );
            }

            return Self{
                .alive_indices = alive_indices,
                .players = players,
            };
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
            self.alive_indices.deinit(allocator);
        }

        pub fn tick(self: *Self, nanoseconds: u64) void {
            var i: usize = 0;
            while (i < self.alive_indices.len) {
                const player = &self.players[self.alive_indices.items[i]];
                if (!player.alive) {
                    self.alive_indices.swapRemove(i);
                    continue;
                }

                player.tick(nanoseconds, i, self.alive_indices.slice(), self.players);

                if (!player.alive) {
                    self.alive_indices.swapRemove(i);
                } else {
                    i += 1;
                }
            }
        }

        pub fn draw(self: Self) !void {
            for (self.players) |player| {
                try player.draw();
            }
        }
    };
}
