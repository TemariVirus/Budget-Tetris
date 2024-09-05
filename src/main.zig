const std = @import("std");
const Allocator = std.mem.Allocator;
const time = std.time;
const windows = std.os.windows;

const nterm = @import("nterm");
const Colors = nterm.Colors;
const input = nterm.input;
const View = nterm.View;

const engine = @import("engine");
const BoardMask = engine.bit_masks.BoardMask;
const kicks = engine.kicks;
const Match = engine.Match(SevenBag);
const Player = Match.Player;
const PeriodicTrigger = engine.PeriodicTrigger;
const SevenBag = engine.bags.SevenBag;

const bot = @import("bot");
const Bot = bot.neat.Bot;
const NN = bot.neat.NN(8, 2);

const sound = @import("sound.zig");

// TODO: Check that view is updated when current frame updates
// TODO: Add title screen
// TODO: Load settings from config file

// 2 * 8 is close to 15.625, so other programs should be affacted minimally.
// Also, 1000 / 8 = 125 is close to 120Hz
const WIN_TIMER_PERIOD = 8;
const INPUT_RATE = 120;
const FRAMERATE = 60;
const FPS_TIMING_WINDOW = 60;

var paused = false;

// https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod
const MMRESULT = enum(windows.UINT) {
    TIMERR_NOERROR = 0,
    TIMERR_NOCANDO = 97,
};
extern "winmm" fn timeBeginPeriod(uPeriod: windows.UINT) callconv(windows.WINAPI) MMRESULT;
extern "winmm" fn timeEndPeriod(uPeriod: windows.UINT) callconv(windows.WINAPI) MMRESULT;

const MoveFuncs = struct {
    var match: *Match = undefined;

    fn left() void {
        if (paused) {
            return;
        }

        match.players[0].moveLeft(false);
    }

    fn leftAll() void {
        if (paused) {
            return;
        }

        match.players[0].moveLeftAll();
    }

    fn right() void {
        if (paused) {
            return;
        }

        match.players[0].moveRight(false);
    }

    fn rightAll() void {
        if (paused) {
            return;
        }

        match.players[0].moveRightAll();
    }

    fn rotateCw() void {
        if (paused) {
            return;
        }

        match.players[0].rotateCw();
    }

    fn rotateDouble() void {
        if (paused) {
            return;
        }

        match.players[0].rotateDouble();
    }

    fn rotateCCw() void {
        if (paused) {
            return;
        }

        match.players[0].rotateCcw();
    }

    fn softDrop() void {
        if (paused) {
            return;
        }

        match.players[0].softDrop();
    }

    fn hardDrop() void {
        if (paused) {
            return;
        }

        match.players[0].hardDrop(0, match.players);
    }

    fn hold() void {
        if (paused) {
            return;
        }

        match.players[0].hold();
    }

    fn softDropStart() void {
        if (paused) {
            return;
        }

        const player = &match.players[0];
        player.current_piece_keys += 1;
        if (!player.state.onGround()) {
            player.move_count += 1;
        }
    }

    fn restart() void {
        paused = false;
        match.restart();
    }
};

pub fn main() !void {
    // TODO: Explore performance of other allocators
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    // TODO: Load default volume from config file
    sound.volume = 0.3;

    try sound.init(allocator);
    defer sound.deinit();

    if (timeBeginPeriod(WIN_TIMER_PERIOD) != .TIMERR_NOERROR) {
        return error.PeriodOutOfRange;
    }
    defer _ = timeEndPeriod(WIN_TIMER_PERIOD);

    // Add 2 to create a 1-wide empty boarder on the left and right.
    try nterm.init(
        allocator,
        std.io.getStdOut(),
        FPS_TIMING_WINDOW,
        Player.DISPLAY_W * 2 + 3,
        Player.DISPLAY_H,
        null,
        null,
    );
    defer nterm.deinit();

    try input.init(allocator);
    defer input.deinit();

    _ = try input.addKeyTrigger(.M, 0, null, toggleMute);
    _ = try input.addKeyTrigger(.OemPlus, 0, null, volumeUp);
    _ = try input.addKeyTrigger(.OemMinus, 0, null, volumeDown);
    _ = try input.addKeyTrigger(.Escape, 0, null, togglePause);

    const settings = engine.GameSettings{};
    var match = try Match.init(
        kicks.srsPlus,
        sound.playSfx,
        allocator,
        2,
        SevenBag.init(std.crypto.random.int(u64)),
        settings,
    );
    defer match.deinit(allocator);
    try setupPlayerInput(&match);

    const bot_thread = try std.Thread.spawn(.{
        .allocator = allocator,
    }, botThread, .{ allocator, &match, 1 });
    defer bot_thread.join();

    const fps_view = View{ .left = 1, .top = 0, .width = 15, .height = 1 };
    var input_timer = PeriodicTrigger.init(time.ns_per_s / INPUT_RATE);
    var render_timer = PeriodicTrigger.init(time.ns_per_s / FRAMERATE);
    while (true) {
        var triggered = false;

        if (input_timer.trigger()) |dt| {
            input.tick();
            if (!paused) {
                match.tick(dt);
            }
            triggered = true;
        }
        if (render_timer.trigger()) |_| {
            match.draw();
            fps_view.printAt(0, 0, Colors.WHITE, null, "{d:.2}FPS", .{nterm.fps()});
            if (paused) {
                nterm.view().writeAligned(.center, nterm.canvasSize().height / 2, Colors.WHITE, null, "Paused");
            }
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

fn toggleMute() void {
    sound.setMuted(!sound.muted) catch {};
}

fn volumeUp() void {
    sound.setVolume(sound.volume + 0.05) catch {};
}

fn volumeDown() void {
    sound.setVolume(sound.volume - 0.05) catch {};
}

fn togglePause() void {
    paused = !paused;
}

fn setupPlayerInput(match: *Match) !void {
    MoveFuncs.match = match;
    match.players[0].name = "You";

    _ = try input.addKeyTrigger(.R, 0, null, MoveFuncs.restart);

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
    _ = try input.addKeyTrigger(.Down, 0, time.ns_per_s / INPUT_RATE, MoveFuncs.softDrop);
    _ = try input.addKeyTrigger(.Space, 0, null, MoveFuncs.hardDrop);
}

fn botThread(allocator: Allocator, match: *Match, index: usize) !void {
    const nn = try NN.load(allocator, "NNs/Soqyme.json");
    defer nn.deinit(allocator);

    const player = &match.players[index];
    var b = Bot.init(nn, 1.0 / 3.0, player.settings.attack_table);

    // TODO: Handle restarts properly
    while (true) {
        const placement = b.findMoves(player.state);
        if (placement.piece.kind != player.state.current.kind) {
            player.hold();
        }
        player.state.pos = placement.pos;
        player.state.current = placement.piece;
        player.hardDrop(index, match.players);

        // TODO: Print bot stats
        // bot_stats_view.printAt(0, 0, Colors.WHITE, null, "Nodes: {d}", .{bot.node_count});
        // bot_stats_view.printAt(0, 1, Colors.WHITE, null, "Depth: {d}", .{bot.current_depth});
        // bot_stats_view.printAt(0, 2, Colors.WHITE, null, "Tresh: {d}", .{bot.move_tresh});
    }
}
