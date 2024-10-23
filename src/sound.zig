const std = @import("std");
const Allocator = std.mem.Allocator;

pub const Sfx = @import("engine").player.Sfx;

const sounds_dir = "sound/";

pub var muted = false;
pub var volume: f32 = 1.0;

pub fn init(allocator: Allocator) !void {
    _ = allocator; // autofix
    try setVolume(volume);
}

pub fn deinit() void {}

pub fn playSfx(sfx: Sfx) void {
    _ = sfx; // autofix
    if (muted) {
        return;
    }
}

pub fn setMuted(value: bool) !void {
    muted = value;
    try setVolume(volume);
}

pub fn setVolume(new_volume: f32) !void {
    volume = @min(@max(new_volume, 0.0), 1.0);
}

pub fn pause() !void {}

pub fn unpause() !void {}
