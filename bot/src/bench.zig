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
// Old 4-line PC average: 20.69s
// Current 4-line PC average: 2.80s
pub fn pcBenchmark() !void {
    const RUN_COUNT = 50;

    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    const total_start = time.nanoTimestamp();
    var max_time: u64 = 0;

    for (0..RUN_COUNT) |seed| {
        const gamestate = GameState.init(SevenBag.init(seed));

        const start = time.nanoTimestamp();
        const solution = try pc.findPc(allocator, gamestate, 4, 11);
        const time_taken: u64 = @intCast(time.nanoTimestamp() - start);
        max_time = @max(max_time, time_taken);

        std.debug.print("Seed: {} | Time taken: {}\n", .{ seed, std.fmt.fmtDuration(time_taken) });
        allocator.free(solution);
    }

    const total_time: u64 = @intCast(time.nanoTimestamp() - total_start);
    std.debug.print("Average: {}\n", .{std.fmt.fmtDuration(total_time / RUN_COUNT)});
    std.debug.print("Max: {}\n", .{std.fmt.fmtDuration(max_time)});
}
