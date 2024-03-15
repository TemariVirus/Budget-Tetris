const std = @import("std");
const time = std.time;

const engine = @import("engine");
const SevenBag = engine.bags.SevenBag;
const GameState = engine.GameState(SevenBag, engine.kicks.srsPlus);

const root = @import("root.zig");
const NN = root.neat.NN;
const pc = root.pc;

pub fn main() !void {
    std.debug.print(
        \\
        \\------------------
        \\   PC Benchmark
        \\------------------
        \\
    , .{});
    try pcBenchmark();
    std.debug.print(
        \\
        \\------------------
        \\   NN Benchmark
        \\------------------
        \\
    , .{});
    try nnBenchmark();
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

// Current mean: 50ns
// Current iters/s: 19754158
pub fn nnBenchmark() !void {
    const RUN_COUNT = 500_000_000;

    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    const nn = try NN.load(allocator, "NNs/Qoshae.json");
    defer nn.deinit(allocator);

    const start = std.time.nanoTimestamp();
    for (0..RUN_COUNT) |_| {
        _ = nn.predict([_]f32{ 5.2, 1.0, 3.0, 9.0, 11.0, 5.0, 2.0, -0.97 });
    }
    const time_taken: u64 = @intCast(std.time.nanoTimestamp() - start);

    std.debug.print("Mean: {}\n", .{std.fmt.fmtDuration(time_taken / RUN_COUNT)});
    std.debug.print("Iters/s: {}\n", .{std.time.ns_per_s * RUN_COUNT / time_taken});
}
