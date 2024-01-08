const std = @import("std");
const Allocator = std.mem.Allocator;
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

const FRAMERATE = 4;
const FPS_TIMING_WINDOW = 60;

pub fn main() !void {
    // All allocators appear to perform the same for `pc.findPc()`
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    // Add 2 to create a 1-wide empty boarder on the left and right.
    try nterm.init(allocator, FPS_TIMING_WINDOW, Game.DISPLAY_W + 2, Game.DISPLAY_H);
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
    var pc_queue = std.ArrayList([]Placement).init(allocator);
    defer pc_queue.deinit();

    const pc_thread = try std.Thread.spawn(.{
        .allocator = allocator,
    }, pcThread, .{ allocator, gamestate, &pc_queue });
    defer pc_thread.join();

    const fps_view = View.init(1, 0, 15, 1);

    var render_timer = PeriodicTrigger.init(time.ns_per_s / FRAMERATE);
    while (true) {
        if (render_timer.trigger()) {
            try fps_view.printAt(0, 0, .White, .Black, "{d:.2}FPS", .{nterm.fps()});

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

fn placePcPiece(allocator: Allocator, game: *Game, queue: *std.ArrayList([]Placement), placement_i: *usize) void {
    if (queue.items.len == 0) {
        return;
    }
    const placements = queue.items[0];

    const placement = placements[placement_i.*];
    if (placement.piece.kind != game.state.current.kind) {
        game.hold();
    }
    game.state.pos = placement.pos;
    game.state.current = placement.piece;
    game.hardDrop();
    placement_i.* += 1;

    if (placement_i.* == placements.len) {
        allocator.free(queue.orderedRemove(0));
        placement_i.* = 0;
    }
}

fn pcThread(allocator: Allocator, state: GameState, queue: *std.ArrayList([]Placement)) !void {
    var game = state;

    while (true) {
        const placements = try pc.findPc(allocator, game, 0, 16);
        for (placements) |placement| {
            if (game.current.kind != placement.piece.kind) {
                game.hold();
            }
            game.current = placement.piece;
            game.pos = placement.pos;
            _ = game.lockCurrent(false);
            game.nextPiece();
        }

        try queue.append(placements);
    }
}
