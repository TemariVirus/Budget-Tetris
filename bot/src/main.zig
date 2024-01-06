const std = @import("std");
const Allocator = std.mem.Allocator;
const Semaphore = std.Thread.Semaphore;
const time = std.time;

const engine = @import("engine");
const Game = engine.Game;
const GameState = engine.GameState;
const kicks = engine.kicks;
const PeriodicTrigger = engine.PeriodicTrigger;
const SevenBag = engine.bags.SevenBag;

const nterm = @import("nterm");
const View = nterm.View;

const root = @import("root.zig");
const pc = root.pc;
const Placement = pc.Placement;
const RingQueue = @import("ring_queue.zig").RingQueue;

const FRAMERATE = 4;
const FPS_TIMING_WINDOW = 60;
const PC_QUEUE_LEN = 16;

var pc_semaphore = Semaphore{};

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
    var game = Game.init(
        "You",
        gamestate,
        6,
        player_view,
        &.{ .PPS, .APP, .VsScore },
    );

    var placement_i: usize = 0;
    var pc_queue = try RingQueue([]Placement).init(allocator, PC_QUEUE_LEN);
    defer pc_queue.deinit(allocator);

    const pc_thread = try std.Thread.spawn(.{
        .allocator = allocator,
    }, pcThread, .{ allocator, gamestate, &pc_queue });
    defer pc_thread.join();

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

            placePcPiece(allocator, &game, &pc_queue, &placement_i);
            try game.draw();
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

fn placePcPiece(allocator: Allocator, game: *Game, queue: *RingQueue([]Placement), placement_i: *usize) void {
    const placements = queue.peekIndex(0) orelse {
        Semaphore.post(&pc_semaphore);
        return;
    };

    const placement = placements[placement_i.*];
    if (placement.piece.kind != game.state.current.kind) {
        game.hold();
    }
    game.state.pos = placement.pos;
    game.state.current = placement.piece;
    game.hardDrop();
    placement_i.* += 1;

    if (placement_i.* == placements.len) {
        allocator.free(queue.dequeue() orelse unreachable);
        placement_i.* = 0;
        Semaphore.post(&pc_semaphore);
    }
}

fn pcThread(allocator: Allocator, state: GameState, queue: *RingQueue([]Placement)) !void {
    var game = state;

    while (true) {
        while (!queue.isFull()) {
            const placements = try pc.findPc(allocator, game, 0, 11);
            for (placements) |placement| {
                if (game.current.kind != placement.piece.kind) {
                    game.hold();
                }
                game.current = placement.piece;
                game.pos = placement.pos;
                _ = game.lockCurrent(false);
                game.nextPiece();
            }

            try queue.enqueue(placements);
        }
        Semaphore.wait(&pc_semaphore);
    }
}
