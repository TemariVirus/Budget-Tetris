const std = @import("std");
const time = std.time;
const windows = std.os.windows;

const engine = @import("engine");
const bags = engine.bags;
const kicks = engine.kicks;
const input = nterm.input;
const nterm = @import("nterm");

const Game = engine.Game;
const GameState = engine.GameState;
const View = nterm.View;

// TODO: check that b2b stuff is accurate, not activated by B2B no clear?
// TODO: check that view is updated when current frame updates

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

    fn rotateCw() void {
        game.rotateCw();
    }

    fn rotateDouble() void {
        game.rotateDouble();
    }

    fn rotateCCw() void {
        game.rotateCcw();
    }

    fn drop() void {
        game.softDrop();
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
    pub fn trigger(self: *PeriodicTrigger) ?u64 {
        const now = time.nanoTimestamp();
        const elapsed = now - self.last;
        // Sleeping tends to cause delayed triggers, compensate by triggering a
        // millisecond early.
        if (elapsed + time.ns_per_ms < self.period) {
            return null;
        }

        self.last = now;
        return @as(u64, @intCast(elapsed));
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

    // Add 2 to create a 1-wide empty boarder on the left and right.
    try nterm.init(allocator, Game.DISPLAY_W + 2, Game.DISPLAY_H);
    defer nterm.deinit();

    var b = bags.SevenBag.init();
    const bag = b.bag();
    var player = try GameState.init(allocator, 6, bag, kicks.srsPlus);
    defer player.deinit(allocator);

    const player_view = View.init(1, 0, Game.DISPLAY_W, Game.DISPLAY_H);
    var player_game = Game.init("You", player, player_view);
    try setupPlayerInput(&player_game);

    var timer = PeriodicTrigger.init(time.ns_per_s / 60);
    while (true) {
        if (timer.trigger()) |elasped| {
            input.tick();
            player_game.tick(elasped);
            player_game.drawToScreen();
            try nterm.render();
        } else {
            time.sleep(1 * time.ns_per_ms);
        }
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
