const std = @import("std");
const time = std.time;
const winmm = std.os.windows.winmm;

const engine = @import("engine");
const input = @import("input.zig");
const terminal = @import("terminal.zig");

const win_timer_period = 8;

const MoveFuncs = struct {
    var game: *engine.GameState = undefined;

    fn left() void {
        _ = game.slide(-1);
    }

    fn leftAll() void {
        _ = game.slide(-10);
    }

    fn right() void {
        _ = game.slide(1);
    }

    fn rightAll() void {
        _ = game.slide(10);
    }

    fn drop() void {
        _ = game.drop(1);
    }

    fn rotateCw() void {
        _ = game.rotate(.Cw);
    }

    fn rotateDouble() void {
        _ = game.rotate(.Double);
    }

    fn rotateCCw() void {
        _ = game.rotate(.CCw);
    }

    fn place() void {
        _ = game.dropToGround();
        _ = game.lock(false);
        game.nextPiece();
    }

    fn hold() void {
        game.hold();
    }
};

const PeriodicTrigger = struct {
    period: u64,
    last: i128,

    pub fn init(period: u64) PeriodicTrigger {
        return .{
            .period = period,
            .last = time.nanoTimestamp(),
        };
    }

    /// Provides no guarantees about whether it will trigger before or after the
    /// period has elapsed.
    pub fn trigger(self: *PeriodicTrigger) bool {
        const now = time.nanoTimestamp();
        const elapsed = now - self.last;
        // Sleeping tends to cause delayed triggers, compensate by triggering a
        // millisecond early.
        if (elapsed + 1 * time.ns_per_ms < self.period) {
            return false;
        }

        self.last = now;
        return true;
    }
};

pub fn main() !void {
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    // https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
    if (winmm.timeBeginPeriod(win_timer_period) != winmm.TIMERR_NOERROR) {
        return error.PeriodOutOfRange;
    }
    defer _ = winmm.timeEndPeriod(win_timer_period);

    try input.init(allocator);
    defer input.deinit();

    const stdout_file = std.io.getStdOut().writer();
    var bw = std.io.bufferedWriter(stdout_file);
    const stdout = bw.writer();

    var b = engine.bags.SevenBag.init();
    var bag = b.bag();
    var player = try engine.GameState.init(allocator, 6, bag, engine.kicks.srsPlus);
    defer player.deinit(allocator);

    try setupPlayerInput(&player);
    var timer = PeriodicTrigger.init(time.ns_per_s / 25);
    while (true) {
        if (!timer.trigger()) {
            time.sleep(1 * time.ns_per_ms);
            continue;
        }

        input.tick();
        try stdout.print("\n{}", .{player});
        try bw.flush();
    }
}

fn setupPlayerInput(player: *engine.GameState) !void {
    MoveFuncs.game = player;
    _ = try input.addKeyTrigger(.C, 0, null, MoveFuncs.hold);

    _ = try input.addKeyTrigger(.Left, 0, null, MoveFuncs.left);
    _ = try input.addKeyTrigger(.Right, 0, null, MoveFuncs.right);

    _ = try input.addKeyTrigger(.Up, 0, null, MoveFuncs.rotateCw);
    _ = try input.addKeyTrigger(.X, 0, null, MoveFuncs.rotateCw);
    _ = try input.addKeyTrigger(.Z, 0, null, MoveFuncs.rotateCCw);
    _ = try input.addKeyTrigger(.A, 0, null, MoveFuncs.rotateDouble);

    _ = try input.addKeyTrigger(
        .Left,
        90 * time.ns_per_ms,
        15 * time.ns_per_ms,
        MoveFuncs.leftAll,
    );
    _ = try input.addKeyTrigger(
        .Right,
        90 * time.ns_per_ms,
        15 * time.ns_per_ms,
        MoveFuncs.rightAll,
    );

    _ = try input.addKeyTrigger(.Down, 0, 1 * time.ns_per_ms, MoveFuncs.drop);
    _ = try input.addKeyTrigger(.Space, 0, null, MoveFuncs.place);
}

// TODO:
// - Input polling frequency
//     Keyboard debounce time is typically ~30ms. (https://stackoverflow.com/a/8348948)
//     The fastest switches have a response time of ~0.7ms (https://steelseries.com/blog/worlds-fastest-mechanical-switch-105)
//     However, the fastest humans can only produce ~24.9 keystrokes a second, (https://www.tomshardware.com/news/world-typing-record-mythicalrocket-293wpm)
//     which translates to ~40ms per keystroke.
//     So, polling input at a standard 60hz (16.6ms) should be more than fast enough.
