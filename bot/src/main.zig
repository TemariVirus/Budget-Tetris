const std = @import("std");
const Allocator = std.mem.Allocator;
const time = std.time;

const engine = @import("engine");
const Player = engine.Player(SevenBag, kicks.srsPlus);
const GameState = engine.GameState(SevenBag, kicks.srsPlus);
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
    try nterm.init(allocator, FPS_TIMING_WINDOW, Player.DISPLAY_W + 2, Player.DISPLAY_H);
    defer nterm.deinit();

    const settings = engine.GameSettings{
        .g = 0,
    };
    const player_view = View{
        .left = 1,
        .top = 0,
        .width = Player.DISPLAY_W,
        .height = Player.DISPLAY_H,
    };
    var player = Player.init("You", SevenBag.init(0), player_view, &.{}, settings);

    var placement_i: usize = 0;
    var pc_queue = std.ArrayList([]Placement).init(allocator);
    defer pc_queue.deinit();

    const pc_thread = try std.Thread.spawn(.{
        .allocator = allocator,
    }, pcThread, .{ allocator, player.state, &pc_queue });
    defer pc_thread.join();

    const fps_view = View{
        .left = 1,
        .top = 0,
        .width = 15,
        .height = 1,
    };

    var render_timer = PeriodicTrigger.init(time.ns_per_s / FRAMERATE);
    while (true) {
        if (render_timer.trigger()) |dt| {
            fps_view.printAt(0, 0, .white, .black, "{d:.2}FPS", .{nterm.fps()});

            placePcPiece(allocator, &player, &pc_queue, &placement_i);
            player.tick(dt);
            try player.draw();
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

fn placePcPiece(allocator: Allocator, game: *Player, queue: *std.ArrayList([]Placement), placement_i: *usize) void {
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
            _ = game.lockCurrent(-1);
            game.nextPiece();
        }

        try queue.append(placements);
    }
}
