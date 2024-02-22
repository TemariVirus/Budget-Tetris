const std = @import("std");
const time = std.time;
const windows = std.os.windows;

const root = @import("root.zig");
const kicks = root.kicks;
const sound = root.sound;
const nterm = @import("nterm");
const input = nterm.input;

const BoardMask = root.bit_masks.BoardMask;
const Match = root.Match(SevenBag, kicks.srsPlus);
const Player = root.Player(SevenBag, kicks.srsPlus);
const PeriodicTrigger = root.PeriodicTrigger;
const SevenBag = root.bags.SevenBag;
const View = nterm.View;

// TODO: check that view is updated when current frame updates
// TODO: Add title screen

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
    var player: *Player = undefined;

    fn left() void {
        player.moveLeft(false);
    }

    fn leftAll() void {
        player.moveLeftAll();
    }

    fn right() void {
        player.moveRight(false);
    }

    fn rightAll() void {
        player.moveRightAll();
    }

    fn rotateCw() void {
        player.rotateCw();
    }

    fn rotateDouble() void {
        player.rotateDouble();
    }

    fn rotateCCw() void {
        player.rotateCcw();
    }

    fn softDrop() void {
        player.softDrop();
    }

    fn hardDrop() void {
        player.hardDrop();
    }

    fn hold() void {
        player.hold();
    }

    fn softDropStart() void {
        player.current_piece_keys += 1;
        if (!player.state.onGround()) {
            player.move_count += 1;
        }
    }
};

pub fn main() !void {
    // TODO: Explore performance of other allocators
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    try sound.init(allocator);
    defer sound.deinit();

    // https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
    if (timeBeginPeriod(WIN_TIMER_PERIOD) != .TIMERR_NOERROR) {
        return error.PeriodOutOfRange;
    }
    defer _ = timeEndPeriod(WIN_TIMER_PERIOD);

    // Add 2 to create a 1-wide empty boarder on the left and right.
    try nterm.init(allocator, FPS_TIMING_WINDOW, Player.DISPLAY_W * 2 + 2, Player.DISPLAY_H);
    defer nterm.deinit();

    try input.init(allocator);
    defer input.deinit();

    const settings = root.Settings{};
    var match = try Match.init(allocator, 2, SevenBag.init(std.crypto.random.int(u64)), settings);
    try setupPlayerInput(&match.players[0]);

    const fps_view = View{ .left = 1, .top = 0, .width = 15, .height = 1 };
    var input_timer = PeriodicTrigger.init(time.ns_per_s / INPUT_RATE);
    var render_timer = PeriodicTrigger.init(time.ns_per_s / FRAMERATE);
    while (true) {
        var triggered = false;

        if (input_timer.trigger()) |_| {
            input.tick();
            triggered = true;
        }
        if (render_timer.trigger()) |dt| {
            fps_view.printAt(0, 0, .White, .Black, "{d:.2}FPS", .{nterm.fps()});

            match.tick(dt);
            try match.draw();
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

fn setupPlayerInput(player: *Player) !void {
    MoveFuncs.player = player;

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
