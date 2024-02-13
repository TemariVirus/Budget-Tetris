const std = @import("std");
const Allocator = std.mem.Allocator;
const fs = std.fs;
const json = std.json;
const mem = std.mem;

const nterm = @import("nterm");
const View = nterm.View;

const engine = @import("engine");

const root = @import("main.zig");
const LeagueData = root.LeagueData;
const UserData = root.UserData;
const GameReplay = @import("GameReplay.zig");

const EXPORT_FILE = "data.csv";

pub const FRAMERATE = 60;
const G = 0.02;
const G_MARGIN = 7200;
const G_INCREASE = 0.0035;
const LOCKRESETS = 15;
const LOCKTIME = 30;

const MatchJson = struct {
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

pub const DataRow = struct {
    /// The game ID (players in the same match get different IDs)
    game_id: u32,
    /// The subframe where the placement occurred
    subframe: u32,
    /// The pieces in the playfied; N = none, G = garbage
    playfield: [400]u8,
    /// The x coordinate of the piece
    x: u4,
    /// The y coordinate of the piece
    y: u6,
    /// The orientation of the placed piece; N = north, E = east, S = south, W = west
    r: [1]u8,
    /// The placed piece
    placed: [1]u8,
    /// The held piece; N = none
    hold: [1]u8,
    /// The next pieces
    next: [14]u8,
    /// The number of lines cleared
    cleared: u3,
    /// The number of lins with garbage cleared
    garbage_cleared: u3,
    /// The amount of garbage sent before garbage blocking
    attack: u16,
    /// The kind of T-spin performed; N = none, M = mini, F = full
    t_spin: [1]u8,
    /// The length of the back-to-back chain
    btb: u32,
    /// The length of the combo chain
    combo: u32,
    /// The amount of garbage that would be received without garbage blocking
    immediate_garbage: u16,
    /// The total amount of incoming garbage
    incoming_garbage: u16,
    /// The player's Tetra League rating
    rating: f32,
    /// The player's Glicko-2 rating
    glicko: ?f32,
    /// The player's Glicko-2 rating deviation
    glicko_rd: ?f32,
};

const ReplayStats = struct {
    game_id: u32 = 0,
    total: u32 = 0,
    bad_version: u32 = 0,
    bad_sdf: u32 = 0,
    no_glicko: u32 = 0,
    wrong_state: u32 = 0,
    passed: u32 = 0,
    rows: u64 = 0,
};

// Constructs a dataset from the replays and exports to csv format
pub fn main() !void {
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    const replays_dir = try fs.cwd().openDir(root.REPLAY_DIR, .{
        .iterate = true,
    });
    var replays = replays_dir.iterate();

    var user_data = try getUserData(allocator);
    defer {
        var keys = user_data.keyIterator();
        while (keys.next()) |key| {
            allocator.free(key.*);
        }
        user_data.deinit();
    }

    const data_file = try fs.cwd().createFile(EXPORT_FILE, .{});
    defer data_file.close();

    var bw = std.io.bufferedWriter(data_file.writer());
    const writer = bw.writer();

    try writer.writeAll("game_id,subframe,playfield,x,y,r,placed,hold,next,cleared,garbage_cleared,attack,t_spin,btb,combo,immediate_garbage,incoming_garbage,rating,glicko,glicko_rd\n");

    var stats = ReplayStats{};
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
            .{ .ignore_unknown_fields = true },
        );
        const matches = parsed.value.data;
        defer parsed.deinit();

        for (matches) |m| {
            try replayMatch(allocator, m, writer, &stats, user_data);
        }
    }
    try bw.flush();

    std.debug.print("Total: {}\n", .{stats.total});
    std.debug.print("Bad version: {}\n", .{stats.bad_version});
    std.debug.print("Bad SDF: {}\n", .{stats.bad_sdf});
    std.debug.print("No Glicko: {}\n", .{stats.no_glicko});
    std.debug.print("Wrong state: {}\n", .{stats.wrong_state});
    std.debug.print("Passed: {}\n", .{stats.passed});
    std.debug.print("Rows: {}\n", .{stats.rows});
}

fn getUserData(allocator: Allocator) !std.StringHashMap(LeagueData) {
    const user_data_json = try fs.cwd().readFileAlloc(allocator, root.USERS_FILE, std.math.maxInt(usize));
    defer allocator.free(user_data_json);

    const parsed = try json.parseFromSlice(
        []const UserData,
        allocator,
        user_data_json,
        .{
            .ignore_unknown_fields = true,
        },
    );
    defer parsed.deinit();

    var user_data = std.StringHashMap(LeagueData).init(allocator);
    for (parsed.value) |u| {
        if (user_data.contains(u.username)) {
            continue;
        }
        try user_data.put(try allocator.dupe(u8, u.username), u.league);
    }
    return user_data;
}

fn replayMatch(
    allocator: Allocator,
    match: MatchJson,
    writer: anytype,
    stats: *ReplayStats,
    user_data: std.StringHashMap(LeagueData),
) !void {
    var settings = engine.Settings{
        .g = getGravity(0),
        .autolock_grace = LOCKRESETS + 1,
        .lock_delay = (LOCKTIME * 1000 / FRAMERATE) + 1,
        .show_next_count = 5, // Fixed for Tetra League
        .display_stats = &.{ .PPS, .APM, .Sent },
    };
    stats.game_id += 2;

    var replays: [2]GameReplay = undefined;
    for (0..replays.len) |i| {
        stats.total += 1;

        replays[i] = GameReplay.init(
            allocator,
            stats.game_id - @as(u32, @intCast(i)),
            match.replays[i].frames,
            match.replays[i].events,
            &settings,
            user_data,
        ) catch |err| {
            if (i == 1) {
                replays[0].deinit();
            }
            switch (err) {
                error.noFullEvent => stats.total -= 1,
                error.unsupportedVersion => stats.bad_version += 1,
                error.nonInstantSoftDrop => stats.bad_sdf += 1,
                else => return err,
            }
            return;
        };
    }
    defer replays[0].deinit();
    defer replays[1].deinit();

    replays[0].opponent = &replays[1];
    replays[1].opponent = &replays[0];

    var frame: u32 = 0;
    while (true) : (frame += 1) {
        settings.g = getGravity(frame);
        const next1 = try replays[0].nextFrame(frame);
        const next2 = try replays[1].nextFrame(frame);
        if (!next1 and !next2) {
            break;
        }
    }

    var i: usize = 2;
    while (i > 0) {
        i -= 1;
        // This player is not one of the ones we want
        if (replays[i].rating == null) {
            continue;
        }
        if (replays[i].glicko == null or replays[i].glicko_rd == null) {
            stats.no_glicko += 1;
        }

        if (try checkEndState(allocator, replays[i], match.replays[i].events)) {
            try writeData(writer, replays[i].data.items);
            stats.passed += 1;
            stats.rows += replays[i].data.items.len;
        } else {
            stats.wrong_state += 1;
        }
    }
}

fn getGravity(subframe: u32) f32 {
    const frames_after_margin: f64 = @floatFromInt((subframe / 10) -| G_MARGIN);
    const g = G + (G_INCREASE * frames_after_margin / FRAMERATE);
    return @floatCast(g * FRAMERATE);
}

fn checkEndState(allocator: Allocator, replay: GameReplay, events: []const EventJson) !bool {
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
                return false;
            }
        }
    }

    return true;
}

fn writeData(writer: anytype, data: []const DataRow) !void {
    for (data) |row| {
        try writer.print("{},{},", .{
            row.game_id,
            row.subframe,
        });

        // Truncate playfield to remove trailing empty cells
        var field_end = row.playfield.len;
        while (field_end > 0) : (field_end -= 1) {
            if (row.playfield[field_end - 1] != 'N') {
                break;
            }
        }
        try writer.print("{s},", .{
            row.playfield[0..field_end],
        });

        try writer.print("{},{},{s},{s},{s},{s},{},{},{},{s},{},{},{},{},{d},{?d},", .{
            row.x,
            row.y,
            row.r,
            row.placed,
            row.hold,
            row.next,
            row.cleared,
            row.garbage_cleared,
            row.attack,
            row.t_spin,
            row.btb,
            row.combo,
            row.immediate_garbage,
            row.incoming_garbage,
            row.rating,
            row.glicko,
        });
        try writer.print("{?d}\n", .{
            row.glicko_rd,
        });
    }
}
