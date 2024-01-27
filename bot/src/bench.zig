const std = @import("std");
const time = std.time;

const engine = @import("engine");
const SevenBag = engine.bags.SevenBag;
const GameState = engine.GameState(SevenBag, engine.kicks.srsPlus);

const pc = @import("root.zig").pc;

pub fn main() !void {
    try pcBenchmark();
}

// There are 241,315,200 possible 4-line PCs from an empty board with a 7-bag
// randomiser, so creating a table of all of them is actually feasible.
// Current mean: 2.489s
// Current max: 15.457s
pub fn pcBenchmark() !void {
    const RUN_COUNT = 50;

    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    var total_time: u64 = 0;
    var max_time: u64 = 0;

    for (0..RUN_COUNT) |seed| {
        const gamestate = GameState.init(SevenBag.init(seed));

        const start = time.nanoTimestamp();
        const solution = try pc.findPc(allocator, gamestate, 4, 11);
        const time_taken: u64 = @intCast(time.nanoTimestamp() - start);
        total_time += time_taken;
        max_time = @max(max_time, time_taken);

        std.debug.print("Seed: {} | Time taken: {}\n", .{ seed, std.fmt.fmtDuration(time_taken) });
        allocator.free(solution);
    }

    std.debug.print("Mean: {}\n", .{std.fmt.fmtDuration(total_time / RUN_COUNT)});
    std.debug.print("Max: {}\n", .{std.fmt.fmtDuration(max_time)});
}
