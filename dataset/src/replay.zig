const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const json = std.json;
const mem = std.mem;

const nterm = @import("nterm");
const input = nterm.input;
const View = nterm.View;

const engine = @import("engine");
const ClearInfo = engine.attack.ClearInfo;

const root = @import("main.zig");
const MongoId = root.MongoId;
const GameReplay = @import("GameReplay.zig");

pub const FRAMERATE = 60;
const G = 0.02;
const G_MARGIN = 7200;
const G_INCREASE = 0.0035;
const SDF = 41.0;
const LOCKRESETS = 15;
const LOCKTIME = 30;

const file = @embedFile("65a67097c6a65cf8d457ac1b.json");
var render_next = false;

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

extern "winmm" fn timeBeginPeriod(uPeriod: std.os.windows.UINT) callconv(std.os.windows.WINAPI) std.os.windows.UINT;

// Works with the 19 Jan 2024 version of Tetr.io's Tetra League replays
pub fn main() !void {
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    const parsed = try json.parseFromSlice(
        struct { data: []const MatchJson },
        allocator,
        file,
        .{
            .ignore_unknown_fields = true,
        },
    );
    const matches = parsed.value.data;
    defer parsed.deinit();

    _ = timeBeginPeriod(1);
    try nterm.init(allocator, 1, 180, 24);
    defer nterm.deinit();

    try input.init(allocator);
    defer input.deinit();

    const key_trigger1 = try input.addKeyTrigger(.Space, 500 * std.time.ns_per_ms, std.time.ns_per_s / (10 * FRAMERATE), playNextFrame);
    const key_trigger2 = try input.addKeyTrigger(.Space, 0, null, playNextFrame);
    defer input.removeKeyTrigger(key_trigger1);
    defer input.removeKeyTrigger(key_trigger2);

    try replayMatch(allocator, matches[0]);
}

fn playNextFrame() void {
    render_next = true;
}

fn replayMatch(allocator: Allocator, match: MatchJson) !void {
    const start = std.time.nanoTimestamp();

    var settings = engine.Settings{
        .g = getGravity(0),
        .soft_g = SDF,
        .autolock_grace = LOCKRESETS,
        .lock_delay = LOCKTIME * 1000 / FRAMERATE,
        .show_next_count = 5, // Fixed for Tetra League
        .display_stats = &.{ .PPS, .APM, .Sent },
    };

    var replay1 = try GameReplay.init(
        allocator,
        (match.replays[0].frames + 1) * 10,
        match.replays[0].events,
        View.init(1, 0, 44, 24),
        &settings,
    );
    defer replay1.deinit();

    var replay2 = try GameReplay.init(
        allocator,
        (match.replays[1].frames + 1) * 10,
        match.replays[1].events,
        View.init(46, 0, 44, 24),
        &settings,
    );
    defer replay2.deinit();

    replay1.opponent = &replay2;
    replay2.opponent = &replay1;

    var subframe: u32 = 0;
    while (true) : (subframe += 1) {
        settings.g = getGravity(subframe);
        const next1 = try replay1.nextSubframe(subframe);
        const next2 = try replay2.nextSubframe(subframe);
        if (!next1 and !next2) {
            break;
        }

        if (subframe % 10 != 0) {
            continue;
        }
        try replay1.game.draw();
        try replay2.game.draw();
        try nterm.view().printAt(1, 0, .White, .Black, "Subframe: {}", .{subframe / 10});
        try nterm.render();

        // while (!render_next) {
        //     input.tick();
        //     std.time.sleep(std.time.ns_per_ms);
        // }
        // render_next = false;
    }

    try checkEndState(allocator, replay1, match.replays[0].events);
    try checkEndState(allocator, replay2, match.replays[1].events);

    const time_taken: u64 = @intCast(std.time.nanoTimestamp() - start);
    std.debug.print("game processed in {}\n", .{std.fmt.fmtDuration(time_taken)});
    std.time.sleep(5 * std.time.ns_per_s);
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
