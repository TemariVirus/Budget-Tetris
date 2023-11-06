const std = @import("std");
const time = std.time;
const windows = std.os.windows;

const engine = @import("engine");
const bags = engine.bags;
const kicks = engine.kicks;
const input = @import("input.zig");
const terminal = @import("terminal.zig");

const Game = @import("Game.zig");
const GameState = engine.GameState;
const View = terminal.View;

// 2 * 8 is close to 15.625, so other programs should be affacted minimally.
// 8 is also a factor of 16, which is good for timing 60hz.
const win_timer_period = 8;

const MMRESULT = enum(windows.UINT) {
    TIMERR_NOERROR = 0,
    TIMERR_NOCANDO = 97,
};
extern "winmm" fn timeBeginPeriod(uPeriod: windows.UINT) callconv(windows.WINAPI) MMRESULT;
extern "winmm" fn timeEndPeriod(uPeriod: windows.UINT) callconv(windows.WINAPI) MMRESULT;

const MoveFuncs = struct {
    var game: *Game = undefined;

    fn left() void {
        game.moveLeft();
    }

    fn leftAll() void {
        game.moveLeftAll();
    }

    fn right() void {
        game.moveRight();
    }

    fn rightAll() void {
        game.moveRightAll();
    }

    fn drop() void {
        game.softDrop();
    }

    fn rotateCw() void {
        game.rotateCw();
    }

    fn rotateDouble() void {
        game.rotateDouble();
    }

    fn rotateCCw() void {
        game.rotateCcw();
    }

    fn place() void {
        game.hardDrop();
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
        if (elapsed + time.ns_per_ms < self.period) {
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
    if (timeBeginPeriod(win_timer_period) != .TIMERR_NOERROR) {
        return error.PeriodOutOfRange;
    }
    defer _ = timeEndPeriod(win_timer_period);

    try terminal.init(allocator, 44 + 2, 24);
    defer terminal.deinit();

    try input.init(allocator);
    defer input.deinit();

    var b = bags.SevenBag.init();
    var bag = b.bag();
    var player = try GameState.init(allocator, 6, bag, kicks.srsPlus);
    defer player.deinit(allocator);

    const view = View.init(0, 0, 44 + 2, 24);
    var player_game = Game.init("You", player, view);
    try setupPlayerInput(&player_game);

    var timer = PeriodicTrigger.init(time.ns_per_s / 60);
    while (true) {
        if (!timer.trigger()) {
            time.sleep(1 * time.ns_per_ms);
            continue;
        }

        input.tick();
        player_game.drawToScreen();
        try terminal.render();
    }
}

fn setupPlayerInput(player: *Game) !void {
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
// - Implement writer interface for view
//     fn writeAll(self: Self, bytes: []const u8) Error!void
//
