const std = @import("std");
const time = std.time;
const windows = std.os.windows;

const root = @import("root.zig");
const bags = root.bags;
const kicks = root.kicks;
const input = nterm.input;
const nterm = @import("nterm");

const Game = root.Game;
const GameState = root.GameState;
const RingQueue = @import("ring_queue.zig").RingQueue;
const View = nterm.View;

// TODO: check that view is updated when current frame updates

// 2 * 8 is close to 15.625, so other programs should be affacted minimally.
// Also, 1000 / 8 = 125 is close to 120Hz
const WIN_TIMER_PERIOD = 8;
const INPUT_RATE = 120;
const FRAMERATE = 60;
const FPS_TIMING_WINDOW = 60;

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

    fn softDrop() void {
        game.softDrop();
    }

    fn hardDrop() void {
        game.hardDrop();
    }

    fn hold() void {
        game.hold();
    }

    fn softDropStart() void {
        game.keys_pressed += 1;
        if (!game.state.onGround()) {
            game.move_count += 1;
        }
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

    pub fn trigger(self: *PeriodicTrigger) bool {
        const now = time.nanoTimestamp();
        const elapsed = now - self.last;
        if (elapsed < self.period) {
            return false;
        }

        self.last += self.period;
        return true;
    }
};

pub fn main() !void {
    // TODO: Explore performance of other allocators
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    // https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
    if (timeBeginPeriod(WIN_TIMER_PERIOD) != .TIMERR_NOERROR) {
        return error.PeriodOutOfRange;
    }
    defer _ = timeEndPeriod(WIN_TIMER_PERIOD);

    // Add 2 to create a 1-wide empty boarder on the left and right.
    try nterm.init(allocator, Game.DISPLAY_W + 2, Game.DISPLAY_H);
    defer nterm.deinit();

    var b = bags.SevenBag.init();
    const bag = b.bag();
    var player = try GameState.init(allocator, 6, bag, kicks.srsPlus);
    defer player.deinit(allocator);

    const player_view = View.init(1, 0, Game.DISPLAY_W, Game.DISPLAY_H);
    var player_game = Game.init(
        "You",
        player,
        .{
            .b2b = &.{ 0, 1 },
            .clears = .{ 0, 0, 1, 2, 4 },
            .combo = &.{ 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 },
            .perfect_clear = .{ 10, 10, 10, 10 },
            .t_spin = .{ 0, 2, 4, 6 },
        },
        player_view,
        &.{ .PPS, .APP, .VsScore },
    );
    try setupPlayerInput(&player_game);

    const start = time.nanoTimestamp();
    const fps_view = View.init(1, 0, 15, 1);
    var frame_times = try RingQueue(u64).init(allocator, FPS_TIMING_WINDOW);
    try frame_times.enqueue(0);
    defer frame_times.deinit(allocator);

    var input_timer = PeriodicTrigger.init(time.ns_per_s / INPUT_RATE);
    var render_timer = PeriodicTrigger.init(time.ns_per_s / FRAMERATE);
    while (true) {
        var triggered = false;

        if (input_timer.trigger()) {
            input.tick();
            triggered = true;
        }
        if (render_timer.trigger()) {
            const old_time = frame_times.peekIndex(0).?;
            const new_time: u64 = @intCast(time.nanoTimestamp() - start);
            const fps = @as(f32, @floatFromInt(frame_times.len())) / @as(f32, @floatFromInt(new_time - old_time)) * time.ns_per_s;
            if (frame_times.isFull()) {
                _ = frame_times.dequeue() orelse unreachable;
            }
            try frame_times.enqueue(new_time);
            try fps_view.printAt(0, 0, .White, .Black, "{d:.2}FPS", .{fps});

            player_game.tick();
            try player_game.draw();
            nterm.render() catch |err| {
                if (err == error.NotInitialized) {
                    return;
                }
                return err;
            };
            triggered = true;
        }

        if (!triggered) {
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

    _ = try input.addKeyTrigger(.Down, 0, null, MoveFuncs.softDropStart);
    _ = try input.addKeyTrigger(.Down, 0, time.ns_per_s / FRAMERATE / 2, MoveFuncs.softDrop);
    _ = try input.addKeyTrigger(.Space, 0, null, MoveFuncs.hardDrop);
}
