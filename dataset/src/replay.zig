const std = @import("std");
const Allocator = std.mem.Allocator;
const json = std.json;
const mem = std.mem;

const nterm = @import("nterm");
const View = nterm.View;

const engine = @import("engine");

const root = @import("main.zig");
const MongoId = root.MongoId;
const GameReplay = @import("GameReplay.zig");

pub const FRAMERATE = 60;
const G = 0.02;
const G_MARGIN = 7200;
const G_INCREASE = 0.0035;
const LOCKRESETS = 15;
const LOCKTIME = 30;

const MatchJson = struct {
    board: [2]struct { id: MongoId },
    replays: [2]struct {
        frames: u32,
        events: []const EventJson,
    },
};

pub const EventJson = struct {
    const EventJsonTag = enum {
        start,
        full,
        ige,
        keydown,
        keyup,
        end,
        strategy,
        target,
    };

    frame: u32,
    type: EventJsonTag,
    data: json.Value,
};

pub fn main() !void {
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    const replays_dir = try std.fs.cwd().openDir("raw_replays", .{
        .iterate = true,
    });
    var replays = replays_dir.iterate();

    var total: u64 = 0;
    var wrong_state: u64 = 0;
    var bad_version: u64 = 0;
    var bad_dcd: u64 = 0;
    var bad_sdf: u64 = 0;
    while (try replays.next()) |replay_file| {
        if (replay_file.kind != .file) {
            continue;
        }

        std.debug.print("Parsing: {s}\n", .{replay_file.name});
        const replay_json = try replays_dir.readFileAlloc(allocator, replay_file.name, std.math.maxInt(usize));
        defer allocator.free(replay_json);

        const parsed = try json.parseFromSlice(
            struct { data: []const MatchJson },
            allocator,
            replay_json,
            .{
                .ignore_unknown_fields = true,
            },
        );
        const matches = parsed.value.data;
        defer parsed.deinit();

        for (matches) |m| {
            replayMatch(allocator, m) catch |err| switch (err) {
                error.noFullEvent => continue,
                error.unsupportedVersion => bad_version += 1,
                error.nonZeroDCD => bad_dcd += 1,
                error.nonInstantSoftDrop => bad_sdf += 1,
                error.WrongEndState => wrong_state += 1,
                else => return err,
            };
            total += 1;
        }
    }

    std.debug.print("Total: {}\n", .{total});
    std.debug.print("Bad version: {}\n", .{bad_version});
    std.debug.print("Bad DCD: {}\n", .{bad_dcd});
    std.debug.print("Bad SDF: {}\n", .{bad_sdf});
    std.debug.print("Wrong state: {}\n", .{wrong_state});
    std.debug.print("Passed: {}\n", .{total - bad_version - bad_dcd - bad_sdf - wrong_state});
}

fn replayMatch(allocator: Allocator, match: MatchJson) !void {
    var settings = engine.Settings{
        .g = getGravity(0),
        .autolock_grace = LOCKRESETS + 1,
        .lock_delay = (LOCKTIME * 1000 / FRAMERATE) + 1,
        .show_next_count = 5, // Fixed for Tetra League
        .display_stats = &.{ .PPS, .APM, .Sent },
    };

    var replay1 = try GameReplay.init(
        allocator,
        match.replays[0].frames,
        match.replays[0].events,
        View.init(0, 0, 0, 0),
        &settings,
    );
    defer replay1.deinit();

    var replay2 = try GameReplay.init(
        allocator,
        match.replays[1].frames,
        match.replays[1].events,
        View.init(0, 0, 0, 0),
        &settings,
    );
    defer replay2.deinit();

    replay1.opponent = &replay2;
    replay2.opponent = &replay1;

    var frame: u32 = 0;
    while (true) : (frame += 1) {
        settings.g = getGravity(frame);
        const next1 = try replay1.nextFrame(frame);
        const next2 = try replay2.nextFrame(frame);
        if (!next1 and !next2) {
            break;
        }
    }

    try checkEndState(allocator, replay1, match.replays[0].events);
    try checkEndState(allocator, replay2, match.replays[1].events);
}

fn getGravity(subframe: u32) f32 {
    const frames_after_margin: f64 = @floatFromInt((subframe / 10) -| G_MARGIN);
    const g = G + (G_INCREASE * frames_after_margin / FRAMERATE);
    return @floatCast(g * FRAMERATE);
}

fn checkEndState(allocator: Allocator, replay: GameReplay, events: []const EventJson) !void {
    const end_event = events[events.len - 1];
    if (end_event.type != .end) {
        return error.InvalidEndEvent;
    }

    const parsed_expected = try json.parseFromValue(struct {
        @"export": struct {
            game: struct {
                board: [40][10]?[]const u8,
            },
        },
    }, allocator, end_event.data, .{
        .ignore_unknown_fields = true,
    });
    defer parsed_expected.deinit();

    const expected = parsed_expected.value.@"export".game.board;
    const actual = replay.game.playfield_colors;

    for (0..40) |y| {
        const expected_y = 39 - y;
        for (0..10) |x| {
            const expected_color: nterm.Color = blk: {
                const cell = expected[expected_y][x] orelse break :blk .Black;
                break :blk if (mem.eql(u8, cell, "i"))
                    .BrightCyan
                else if (mem.eql(u8, cell, "o"))
                    .BrightYellow
                else if (mem.eql(u8, cell, "t"))
                    .BrightMagenta
                else if (mem.eql(u8, cell, "s"))
                    .BrightGreen
                else if (mem.eql(u8, cell, "z"))
                    .Red
                else if (mem.eql(u8, cell, "l"))
                    .Yellow
                else if (mem.eql(u8, cell, "j"))
                    .Blue
                else if (mem.eql(u8, cell, "gb"))
                    .White
                else
                    return error.UnknownColor;
            };
            if (expected_color != actual.get(x, y)) {
                return error.WrongEndState;
            }
        }
    }
}
