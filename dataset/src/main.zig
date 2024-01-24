const std = @import("std");
const Allocator = std.mem.Allocator;
const HttpClient = std.http.Client;
const json = std.json;
const sleep = std.time.sleep;

// Reference: https://tetr.io/about/api/#endpoints
const TETRIO_API = "https://ch.tetr.io/api/";
// Reference: https://inoue.szy.lol/api/
const INOUE_REPLAY_API = "https://inoue.szy.lol/api/replay/";
const REPLAY_DIR = "raw_replays/";

pub const MongoId = [24]u8;

pub const UserData = struct {
    _id: MongoId,
    username: []const u8,
    league: struct {
        rating: f64,
        glicko: ?f64,
    },
};

fn TetrioResponse(comptime T: type) type {
    return struct {
        success: bool,
        @"error": ?[]const u8 = null,
        data: ?T = null,
    };
}

pub fn main() !void {
    try fetchReplays(500);
}

fn fetchReplays(user_count: usize) !void {
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    var client = HttpClient{ .allocator = allocator };
    defer client.deinit();

    const users = try getLeagueTop(allocator, &client, user_count);
    defer {
        for (users) |user| {
            allocator.free(user.username);
        }
        allocator.free(users);
    }

    // Save users to file
    var file = try std.fs.cwd().createFile("users.json", .{});
    defer file.close();
    try json.stringify(users, .{
        .whitespace = .indent_4,
    }, file.writer());

    for (users, 1..) |user, i| {
        std.debug.print("Getting replays of {s} | rating: {d:.2} ({}/{})\n", .{
            user.username,
            user.league.rating,
            i,
            users.len,
        });
        const replays = try getUserReplays(allocator, &client, user._id);
        defer allocator.free(replays);

        for (replays, 1..) |replay_id, j| {
            var filename = [_]u8{undefined} ** (REPLAY_DIR.len + replay_id.len + 5);
            @memcpy(filename[0..REPLAY_DIR.len], REPLAY_DIR);
            @memcpy(filename[REPLAY_DIR.len .. replay_id.len + REPLAY_DIR.len], &replay_id);
            @memcpy(filename[replay_id.len + REPLAY_DIR.len ..], ".json");

            if (std.fs.cwd().access(&filename, .{})) {
                std.debug.print("Replay already downloaded. Skiping...\n", .{});
                continue;
            } else |err| {
                if (err != error.FileNotFound) {
                    std.debug.print("Error while accessing file: {}\n", .{err});
                    continue;
                }
            }

            std.debug.print("Getting replay {s} ({}/{})\n", .{
                replay_id,
                j,
                replays.len,
            });
            try saveReplay(allocator, &client, replay_id, &filename);
        }
    }
}

fn randomId() ![32]u8 {
    var bytes = [_]u8{undefined} ** 16;
    std.crypto.random.bytes(&bytes);

    var uuid = [_]u8{undefined} ** 32;
    for (0..32) |i| {
        const b = bytes[i / 2];
        const value = if (i % 2 == 0)
            b & 0x0F
        else
            b >> 4;
        uuid[i] = if (value < 10)
            '0' + value
        else
            'a' - 10 + value;
    }

    return uuid;
}

fn getLeagueTop(allocator: Allocator, client: *HttpClient, n: usize) ![]UserData {
    const endpoint = TETRIO_API ++ "users/lists/league";

    const req_id = try randomId();
    var headers = std.http.Headers.init(allocator);
    try headers.append("X-Session-ID", &req_id);
    defer headers.deinit();

    const result = try allocator.alloc(UserData, n);
    errdefer allocator.free(result);

    var i: u32 = 0;
    var min_rating: f64 = 25_000.0;
    while (i < n) {
        // Rate limit to 1 request per second
        sleep(std.time.ns_per_s);

        const limit = @min(n - i, 100);

        var url_builder = std.ArrayList(u8).init(allocator);
        defer url_builder.deinit();

        try url_builder.appendSlice(endpoint ++ "?after=");
        try url_builder.writer().print("{d:.0}", .{min_rating});
        try url_builder.appendSlice("&limit=");
        try url_builder.writer().print("{}", .{limit});

        var res = try client.fetch(allocator, .{
            .method = .GET,
            .headers = headers,
            .location = .{ .url = url_builder.items },
        });
        defer res.deinit();

        const parsed = try json.parseFromSlice(
            TetrioResponse(struct { users: []UserData }),
            allocator,
            res.body.?,
            .{
                .duplicate_field_behavior = .use_last,
                .ignore_unknown_fields = true,
            },
        );
        defer parsed.deinit();

        if (res.status != .ok) {
            continue;
        }
        if (!parsed.value.success) {
            std.debug.print("Error while getting player data: {s}", .{parsed.value.@"error".?});
            continue;
        }

        for (parsed.value.data.?.users, 0..) |user, j| {
            result[i + j] = user;
            result[i + j].username = try allocator.dupe(u8, user.username);
            min_rating = @min(min_rating, user.league.rating);
        }
        i += limit;
    }

    return result;
}

fn getUserReplays(allocator: Allocator, client: *HttpClient, user_id: MongoId) ![]MongoId {
    const endpoint_base = TETRIO_API ++ "streams/league_userrecent_";
    var endpoint = [_]u8{undefined} ** (endpoint_base.len + user_id.len);
    @memcpy(endpoint[0..endpoint_base.len], endpoint_base);
    @memcpy(endpoint[endpoint_base.len..], &user_id);

    while (true) {
        // Rate limit to 1 request per second
        sleep(std.time.ns_per_s);

        var res = try client.fetch(allocator, .{
            .method = .GET,
            .location = .{ .url = &endpoint },
        });
        defer res.deinit();

        const parsed = try json.parseFromSlice(
            TetrioResponse(struct { records: []struct { replayid: MongoId } }),
            allocator,
            res.body.?,
            .{
                .duplicate_field_behavior = .use_last,
                .ignore_unknown_fields = true,
            },
        );
        defer parsed.deinit();

        if (res.status != .ok) {
            continue;
        }
        if (!parsed.value.success) {
            std.debug.print("Error while getting player data: {s}", .{parsed.value.@"error".?});
            continue;
        }

        const records = parsed.value.data.?.records;
        const result = try allocator.alloc(MongoId, records.len);
        for (0..records.len) |i| {
            result[i] = records[i].replayid;
        }
        return result;
    }
}

// TODO: Convert replays to more consumable format
fn saveReplay(allocator: Allocator, client: *HttpClient, replay_id: MongoId, path: []const u8) !void {
    const endpoint_base = INOUE_REPLAY_API;
    var endpoint = [_]u8{undefined} ** (endpoint_base.len + replay_id.len);
    @memcpy(endpoint[0..endpoint_base.len], endpoint_base);
    @memcpy(endpoint[endpoint_base.len..], &replay_id);

    while (true) {
        // Rate limit to 1 request per second
        sleep(1 * std.time.ns_per_s);

        var res = try client.fetch(allocator, .{
            .method = .GET,
            .location = .{ .url = &endpoint },
        });
        defer res.deinit();

        if (res.status != .ok) {
            std.debug.print("Failed to get replay. Status code: {}\n", .{res.status});
            if (res.status == .too_many_requests) {
                std.debug.print("Warning: Received status code 429!\n", .{});
                sleep(5 * std.time.ns_per_s);
            }
            continue;
        }

        const res_body = try allocator.dupe(u8, res.body.?);
        defer allocator.free(res_body);

        // Inoue api returns empty array instead of empty object
        const original = "\"data\":[]";
        const replacement = "\"data\":{}";
        for (0..res_body.len - original.len + 1) |i| {
            if (std.mem.eql(u8, res_body[i .. i + original.len], original)) {
                @memcpy(res_body[i .. i + replacement.len], replacement);
            }
        }

        var file = try std.fs.cwd().createFile(path, .{});
        defer file.close();

        try file.writeAll(res_body);
        break;
    }
}
