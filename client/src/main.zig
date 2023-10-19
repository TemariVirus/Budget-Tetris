const std = @import("std");
const engine = @import("engine");

pub fn main() !void {
    const stdout_file = std.io.getStdOut().writer();
    var bw = std.io.bufferedWriter(stdout_file);
    const stdout = bw.writer();

    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    defer _ = gpa.deinit();

    var allocator = gpa.allocator();

    var b = engine.bags.SevenBag.init();
    var bag = b.bag();
    var player = try engine.Game.init(allocator, 6, bag, engine.kicks.srsPlus);
    defer player.deinit(allocator);

    try stdout.print("\n" ** 10 ++ "{}", .{player});
    _ = player.hold();
    _ = player.place(false);
    _ = player.place(false);
    _ = player.place(false);
    _ = player.place(false);
    _ = player.place(false);
    _ = player.place(false);
    _ = player.place(false);
    _ = player.place(false);
    _ = player.place(false);
    try stdout.print("\n" ** 10 ++ "{}", .{player});

    try bw.flush();
}
