const std = @import("std");
const terminal = @import("terminal.zig");
const engine = @import("engine");

pub fn main() !void {
    const stdout_file = std.io.getStdOut().writer();
    var bw = std.io.bufferedWriter(stdout_file);
    const stdout = bw.writer();

    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    defer _ = gpa.deinit();

    var allocator = gpa.allocator();

    var b = engine.bags.SevenBag.init();
    var bag = b.bag();
    var player = try engine.GameState.init(allocator, 6, bag, engine.kicks.srsPlus);
    defer player.deinit(allocator);

    while (true) {
        try stdout.print("\n" ** 10 ++ "{}", .{player});
        try bw.flush();

        std.time.sleep(33_000_000);
    }
}

fn place(p: *engine.GameState) void {
    _ = p.dropToGround();
    _ = p.place(false);
    p.nextPiece();
}

// TODO:
// - Input polling frequency
//     Keyboard debounce time is typically ~30ms. (https://stackoverflow.com/a/8348948)
//     The fastest switches have a response time of ~0.7ms (https://steelseries.com/blog/worlds-fastest-mechanical-switch-105)
//     However, the fastest humans can only produce ~24.9 keystrokes a second, (https://www.tomshardware.com/news/world-typing-record-mythicalrocket-293wpm)
//     which translates to ~40ms per keystroke.
//     So, polling input at a standard 60hz (16.6ms) should be more than fast enough.
