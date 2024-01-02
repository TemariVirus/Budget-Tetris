const std = @import("std");
const engine = @import("engine");
const nterm = @import("nterm");
const root = @import("root.zig");
const time = std.time;

const Allocator = std.mem.Allocator;

const Game = engine.Game;
const GameState = engine.GameState;
const kicks = engine.kicks;
const PeriodicTrigger = engine.PeriodicTrigger;
const PieceKind = engine.pieces.PieceKind;
const SevenBag = engine.bags.SevenBag;

const View = nterm.View;

const pc = root.pc;
const Placement = pc.Placement;
const RingQueue = @import("ring_queue.zig").RingQueue;

const FRAMERATE = 60;
const FPS_TIMING_WINDOW = 60;

pub fn main() !void {
    // TODO: Explore performance of other allocators
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    // Add 2 to create a 1-wide empty boarder on the left and right.
    try nterm.init(allocator, Game.DISPLAY_W + 2, Game.DISPLAY_H);
    defer nterm.deinit();

    const bag = SevenBag.init(0);
    const gamestate = GameState.init(bag, kicks.srsPlus);
    const player_view = View.init(1, 0, Game.DISPLAY_W, Game.DISPLAY_H);
    var player_game = Game.init(
        "You",
        gamestate,
        6,
        player_view,
        &.{ .PPS, .APP, .VsScore },
    );

    const start = time.nanoTimestamp();
    const fps_view = View.init(1, 0, 15, 1);
    var frame_times = try RingQueue(u64).init(allocator, FPS_TIMING_WINDOW);
    try frame_times.enqueue(0);
    defer frame_times.deinit(allocator);

    var render_timer = PeriodicTrigger.init(time.ns_per_s / FRAMERATE);
    while (true) {
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
        } else {
            time.sleep(1 * time.ns_per_ms);
        }
    }
}

fn pcHelper(allocator: Allocator, gamestate: GameState, comptime n_pieces: comptime_int) ![]Placement {
    var pieces = [_]PieceKind{undefined} ** n_pieces;
    pieces[0] = gamestate.current.kind;
    for (gamestate.next_pieces, 1..) |piece, i| {
        pieces[i] = piece;
    }
    var bag_copy = gamestate.bag;
    for (1 + gamestate.next_pieces.len..pieces.len) |i| {
        pieces[i] = bag_copy.next();
    }

    return try pc.findPc(allocator, gamestate, &pieces);
}
