const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const json = std.json;
const math = std.math;
const mem = std.mem;

const nterm = @import("nterm");
const View = nterm.View;
const Color = nterm.Color;

const engine = @import("engine");
const BoardMask = engine.bit_masks.BoardMask;
const ClearInfo = engine.attack.ClearInfo;
const Game = engine.Game(Bag, engine.kicks.srsPlus);
const PieceKind = engine.pieces.PieceKind;
const Settings = engine.Settings;

const LeagueData = @import("main.zig").LeagueData;
const replay = @import("replay.zig");
const EventJson = replay.EventJson;
const DataRow = replay.DataRow;

const Self = @This();
const GarbageQueue = std.ArrayListUnmanaged(GarbageEvent);

allocator: Allocator,
frames: u32,
events: []const Event,
game: Game,
arr: u8,
das: u8,
dcd: u8,
garbage_queue: GarbageQueue,
opponent: *Self = undefined,

event_i: usize = 0,
garbage_i: u16 = 0,
left_shift_subframe: ?u32 = null,
right_shift_subframe: ?u32 = null,
softdropping: bool = false,
highest_y: i8 = 0,
was_on_ground: bool = undefined,

id: u32,
rating: ?f32 = null,
glicko: ?f32 = null,
glicko_rd: ?f32 = null,
data: std.ArrayListUnmanaged(DataRow),

const GARBAGE_CAP = 8;
const GARBAGE_SPEED = 20;
const GARBAGE_MARGIN = 10800;
const GARBAGE_INCREASE = 0.008;

// Verified to be the same as the original using 2,400,000,000 iterations
const Bag = struct {
    pieces: [7]PieceKind,
    index: u8 = 7,
    seed: u64,

    pub fn init(seed: i32) Bag {
        var bounded_seed = @rem(seed, 2147483647);
        if (bounded_seed <= 0) {
            bounded_seed += 2147483646;
        }

        return Bag{
            .pieces = undefined,
            .index = 7,
            .seed = @intCast(bounded_seed),
        };
    }

    pub fn next(self: *Bag) PieceKind {
        if (self.index >= self.pieces.len) {
            self.pieces = .{ .Z, .L, .O, .S, .I, .J, .T };
            self.shuffle();
            self.index = 0;
        }

        defer self.index += 1;
        return self.pieces[self.index];
    }

    fn shuffle(self: *Bag) void {
        var i = self.pieces.len;
        while (i > 1) {
            i -= 1;
            const swap: usize = blk: {
                self.seed = (self.seed * 16807) % 2147483647;
                const float = @as(f64, @floatFromInt(self.seed - 1)) / 2147483646.0;
                break :blk @intFromFloat(float * @as(f64, @floatFromInt(i + 1)));
            };
            const tmp = self.pieces[i];
            self.pieces[i] = self.pieces[swap];
            self.pieces[swap] = tmp;
        }
    }
};

const Options = struct {
    version: i32,
    seed: i32,
    // This seems to be the only reliable source of user info as the `board` field
    // in the event jsons may be reversed.
    username: []const u8,
    handling: struct {
        arr: f64,
        das: f64,
        dcd: f64,
        sdf: u8,
    },
};

const EventTag = enum {
    garbage,
    garbageConfirm,
    keyDown,
    keyUp,
};
const Event = union(EventTag) {
    garbage: GarbageEvent,
    garbageConfirm: GarbageEvent,
    keyDown: KeyEvent,
    keyUp: KeyEvent,

    pub fn fromEventJson(event_json: EventJson) ?Event {
        const data = event_json.data.object;
        switch (event_json.type) {
            .ige => {
                const event_data = (event_json.data.object.get("data") orelse
                    return null).object;
                const event_type = (event_data.get("type") orelse
                    return null).string;

                const interaction_data = (event_data.get("data") orelse
                    return null).object;
                if (!mem.eql(u8, interaction_data.get("type").?.string, "garbage")) {
                    return null;
                }

                const event = GarbageEvent{
                    .subframe = event_json.frame * 10,
                    .id = @intCast(interaction_data.get("iid").?.integer),
                    .hole = @intCast(interaction_data.get("column").?.integer),
                    .lines = @intCast(interaction_data.get("amt").?.integer),
                };
                return if (mem.eql(u8, event_type, "interaction"))
                    Event{ .garbage = event }
                else if (mem.eql(u8, event_type, "interaction_confirm"))
                    Event{ .garbageConfirm = event }
                else
                    null;
            },
            .keydown, .keyup => {
                const key = Key.fromString(data.get("key").?.string) orelse return null;
                const subframe_part: u32 = switch (data.get("subframe").?) {
                    .integer => |v| @intCast(v * 10),
                    .float => |v| @intFromFloat(@round(v * 10)),
                    else => unreachable,
                };
                const hoisted = if (data.get("hoisted")) |value| value.bool else false;

                const event = KeyEvent{
                    .key = key,
                    .subframe = event_json.frame * 10 + subframe_part,
                    .hoisted = hoisted,
                };
                return if (event_json.type == .keydown)
                    Event{ .keyDown = event }
                else
                    Event{ .keyUp = event };
            },
            else => return null,
        }
    }

    pub fn subframe(self: Event) u32 {
        return switch (self) {
            .garbage => self.garbage.subframe,
            .garbageConfirm => self.garbageConfirm.subframe,
            .keyDown => self.keyDown.subframe,
            .keyUp => self.keyUp.subframe,
        };
    }
};

const GarbageEvent = struct {
    subframe: u32,
    id: u16,
    hole: u4,
    lines: u8,
};

const Key = enum {
    hold,
    moveLeft,
    moveRight,
    rotateCW,
    rotateCCW,
    rotate180,
    softDrop,
    hardDrop,

    pub fn fromString(str: []const u8) ?Key {
        return if (mem.eql(u8, str, "hold"))
            .hold
        else if (mem.eql(u8, str, "moveLeft"))
            .moveLeft
        else if (mem.eql(u8, str, "moveRight"))
            .moveRight
        else if (mem.eql(u8, str, "rotateCW"))
            .rotateCW
        else if (mem.eql(u8, str, "rotateCCW"))
            .rotateCCW
        else if (mem.eql(u8, str, "rotate180"))
            .rotate180
        else if (mem.eql(u8, str, "softDrop"))
            .softDrop
        else if (mem.eql(u8, str, "hardDrop"))
            .hardDrop
        else
            null;
    }
};
const KeyEvent = struct {
    key: Key,
    subframe: u32,
    hoisted: bool,
};

const InitError = error{
    noFullEvent,
    unsupportedVersion,
    nonInstantSoftDrop,
};
pub fn init(
    allocator: Allocator,
    id: u32,
    frames: u32,
    event_jsons: []const EventJson,
    settings: *const Settings,
    user_data: std.StringHashMap(LeagueData),
) !Self {
    var i: usize = 0;
    while (i < event_jsons.len) : (i += 1) {
        if (event_jsons[i].type == .full) {
            break;
        }
    } else {
        return InitError.noFullEvent;
    }

    const options_json = event_jsons[i].data.object.get("options").?;

    const parsed_options = try json.parseFromValue(Options, allocator, options_json, .{
        .ignore_unknown_fields = true,
    });
    const options = parsed_options.value;
    defer parsed_options.deinit();

    // Replay only works with specific settings
    if (options.version != 16) {
        return InitError.unsupportedVersion;
    }
    // We can try anyway even if these settings are different, there's a chance
    // the replay will still be correct
    // if (options.handling.sdf != 41.0) {
    //     return InitError.nonInstantSoftDrop;
    // }

    var self = Self{
        .allocator = allocator,
        .frames = frames,
        .events = undefined,
        .game = Game.init(
            allocator,
            "",
            Bag.init(options.seed),
            .{ .left = 0, .top = 0, .width = 0, .height = 0 },
            400.0 * replay.FRAMERATE,
            settings,
        ),
        .arr = @intFromFloat(@round(options.handling.arr * 10)),
        .das = @intFromFloat(@round(options.handling.das * 10)),
        .dcd = @intFromFloat(@round(options.handling.dcd * 10)),
        .garbage_queue = GarbageQueue{},

        .id = id,
        .data = std.ArrayListUnmanaged(DataRow){},
    };

    if (user_data.get(options.username)) |league_data| {
        self.rating = league_data.rating;
        self.glicko = league_data.glicko;
        self.glicko_rd = league_data.rd;
    }

    var events = std.ArrayListUnmanaged(Event){};
    for (event_jsons) |event| {
        if (Event.fromEventJson(event)) |e| {
            try events.append(allocator, e);
        }
    }
    self.events = try events.toOwnedSlice(allocator);

    return self;
}

pub fn deinit(self: *Self) void {
    self.allocator.free(self.events);
    self.game.deinit();
    self.garbage_queue.deinit(self.allocator);
    self.data.deinit(self.allocator);
}

pub fn nextFrame(self: *Self, frame: u32) !bool {
    const subframe = frame * 10;
    while (self.event_i < self.events.len and self.events[self.event_i].subframe() < subframe) : (self.event_i += 1) {
        try self.nextSubframes(self.events[self.event_i].subframe());
        switch (self.events[self.event_i]) {
            .garbage => |event| self.handleGarbage(event),
            .garbageConfirm => |event| self.handleGarbageConfirm(event),
            .keyDown => |event| try self.handleKeyDown(event),
            .keyUp => |event| self.handleKeyUp(event),
        }
    }
    try self.nextSubframes(subframe);
    return frame <= self.frames;
}

fn nextSubframes(self: *Self, subframe: u32) !void {
    const now = gameTime(subframe);
    if (now <= self.game.time) {
        return;
    }
    self.was_on_ground = self.game.state.onGround();

    // Handle autolocking
    if (self.game.state.onGround()) {
        if (self.game.move_count >= self.game.settings.autolock_grace or
            now -| self.game.last_move_time >= self.game.settings.lock_delay * std.time.ns_per_ms)
        {
            try self.place(subframe);
        }
    } else if (self.was_on_ground) {
        // 5 frames worth of gravity when moving off ground
        self.game.vel = @max(self.game.vel, 5 * self.game.settings.g / replay.FRAMERATE);
    }

    const old_x = self.game.state.pos.x;
    if (self.left_shift_subframe) |_| {
        while (subframe >= self.left_shift_subframe.?) : (self.left_shift_subframe.? += self.arr) {
            if (self.arr == 0) {
                self.game.moveLeftAll();
                break;
            }
            self.game.moveLeft(true);
        }
    }
    if (self.right_shift_subframe) |_| {
        while (subframe >= self.right_shift_subframe.?) : (self.right_shift_subframe.? += self.arr) {
            if (self.arr == 0) {
                self.game.moveRightAll();
                break;
            }
            self.game.moveRight(true);
        }
    }
    const moved = @abs(self.game.state.pos.x - old_x);
    self.game.move_count += moved;

    // Don't add to move count if the piece is still at the top
    const next_y = self.game.state.pos.y - @as(i8, if (self.game.vel >= 1.0) 1 else 0);
    if (next_y >= self.highest_y) {
        self.game.move_count = 0;
    }
    self.highest_y = @max(self.highest_y, next_y);

    if (self.softdropping) {
        self.game.softDrop();
    }
    self.game.tick(now - self.game.time);
}

fn gameTime(subframe: u32) u64 {
    return std.time.ns_per_s * @as(u64, subframe) / (10 * replay.FRAMERATE);
}

fn handleGarbage(self: *Self, event: GarbageEvent) void {
    for (self.garbage_queue.items) |*garbage| {
        if (garbage.id == event.id) {
            // We know there is garbage now, but haven't confirmed when it will arrive
            garbage.subframe = std.math.maxInt(u32) - 1;
            break;
        }
    }
}

fn handleGarbageConfirm(self: *Self, event: GarbageEvent) void {
    for (self.garbage_queue.items) |*garbage| {
        if (garbage.id == event.id) {
            garbage.subframe = event.subframe + GARBAGE_SPEED * 10;
            garbage.hole = event.hole;
            break;
        }
    }
}

fn handleKeyDown(self: *Self, event: KeyEvent) !void {
    switch (event.key) {
        .hold, .rotateCW, .rotateCCW, .rotate180 => self.applyDCD(event.subframe, false),
        else => {},
    }

    const force_lock = self.game.state.onGround() and
        self.game.move_count + 1 == self.game.settings.autolock_grace;
    switch (event.key) {
        .hold => {
            if (self.game.already_held) {
                return;
            }
            self.game.hold();
            self.adjustSpawn();
        },
        .moveLeft => {
            if (force_lock) {
                try self.place(event.subframe);
            }
            self.game.moveLeft(false);

            self.right_shift_subframe = null;
            self.left_shift_subframe = event.subframe;
            if (!event.hoisted) {
                self.left_shift_subframe.? += self.das;
            }
        },
        .moveRight => {
            if (force_lock) {
                try self.place(event.subframe);
            }
            self.game.moveRight(false);

            self.left_shift_subframe = null;
            self.right_shift_subframe = event.subframe;
            if (!event.hoisted) {
                self.right_shift_subframe.? += self.das;
            }
        },
        .rotateCW => {
            if (force_lock) {
                try self.place(event.subframe);
            }
            self.game.rotateCw();
        },
        .rotateCCW => {
            if (force_lock) {
                try self.place(event.subframe);
            }
            self.game.rotateCcw();
        },
        .rotate180 => {
            if (force_lock) {
                try self.place(event.subframe);
            }
            self.game.rotateDouble();
        },
        .softDrop => {
            self.softdropping = true;
            self.game.softDrop();
        },
        .hardDrop => try self.place(event.subframe),
    }
}

fn applyDCD(self: *Self, subframe: u32, force: bool) void {
    const pos = self.game.state.pos;
    if (self.left_shift_subframe) |_| {
        if (force or self.game.state.collides(
            self.game.state.current,
            .{ .x = pos.x - 1, .y = pos.y },
        )) {
            self.left_shift_subframe = @max(self.left_shift_subframe.?, subframe + self.dcd);
        }
    }
    if (self.right_shift_subframe) |_| {
        if (force or self.game.state.collides(
            self.game.state.current,
            .{ .x = pos.x + 1, .y = pos.y },
        )) {
            self.right_shift_subframe = @max(self.right_shift_subframe.?, subframe + self.dcd);
        }
    }
}

fn adjustSpawn(self: *Self) void {
    self.game.state.pos = self.game.state.current.kind.startPos();
    self.game.state.pos.y += 1;
    self.game.vel = 1.0 - (self.game.settings.g / replay.FRAMERATE);
    self.highest_y = self.game.state.pos.y;
}

fn place(self: *Self, subframe: u32) !void {
    if (self.game.state.dropToGround() > 0) {
        self.game.last_kick = -1;
    }

    var state_copy = self.game.state;
    const clear_info = state_copy.lockCurrent(self.game.last_kick);
    try self.addRow(clear_info, subframe);

    self.game.hardDrop();
    self.adjustSpawn();
    self.applyDCD(subframe, true);

    // Tetr.io counts T-spin minis as difficult clears as well
    if (clear_info.cleared > 0 and clear_info.t_spin == .Mini) {
        self.game.state.b2b += 1;
    }

    const attack = getTetrioAttack(clear_info, self.game.state.b2b, self.game.state.combo, subframe);
    try self.blockAndAttack(attack);
    if (clear_info.pc) {
        try self.blockAndAttack(multiplyGarbage(10.0, subframe));
    }

    // Receive garbage
    if (clear_info.cleared > 0) {
        return;
    }
    var lines_left: u16 = GARBAGE_CAP;
    while (self.garbage_queue.items.len > 0) {
        const garbage = self.garbage_queue.items[0];
        if (garbage.subframe > subframe) {
            break;
        }

        const received = @min(garbage.lines, lines_left);
        lines_left -= received;
        self.game.addGarbage(garbage.hole, received);
        if (garbage.lines == received) {
            _ = self.garbage_queue.orderedRemove(0);
        } else {
            self.garbage_queue.items[0].lines -= received;
            break;
        }
    }
}

fn blockAndAttack(self: *Self, original_attack: u16) !void {
    // Garbage blocking
    var attack = original_attack;
    var actual_attack = original_attack;
    while (actual_attack > 0 and self.garbage_queue.items.len > 0) {
        const garbage_event = self.garbage_queue.items[0];
        const blocked = @min(garbage_event.lines, actual_attack);

        // Only block gabage events that have been received
        if (garbage_event.subframe != std.math.maxInt(u32)) {
            attack -= blocked;
        }

        actual_attack -= blocked;
        if (garbage_event.lines == blocked) {
            _ = self.garbage_queue.orderedRemove(0);
        } else {
            self.garbage_queue.items[0].lines -= blocked;
            break;
        }
    }

    // Send garbage
    if (attack > 0) {
        // We don't know that the attack was blocked yet due to network latency, so this counter
        // will increment
        self.garbage_i += 1;
    }
    if (actual_attack > 0) {
        try self.opponent.garbage_queue.append(self.opponent.allocator, .{
            // Don't set the subframe yet because of rollback, the proper subframe will be set by
            // a garbageConfirm event later
            .subframe = std.math.maxInt(u32),
            .id = self.garbage_i,
            .hole = undefined,
            .lines = @intCast(actual_attack),
        });
    }
}

fn getTetrioAttack(info: ClearInfo, b2b: u32, combo: u32, subframe: u32) u16 {
    var attack: f64 = if (info.t_spin == .Full)
        @floatFromInt(info.cleared * 2)
    else
        ([_]f64{ 0, 0, 1, 2, 4 })[info.cleared];

    if (info.cleared > 0 and b2b > 1) {
        const canonical_b2b: f64 = @floatFromInt(b2b - 1);
        attack += std.math.floor(1 + std.math.log1p(canonical_b2b * 0.8));
        // Not sure why the modulo was in the source code, might be a bug
        if (b2b > 2) {
            attack += (1 + @rem(std.math.log1p(canonical_b2b * 0.8), 1)) / 3;
        }
    }

    if (combo > 1) {
        const canonical_combo: f64 = @floatFromInt(combo - 1);
        attack *= 1 + (0.25 * canonical_combo);
        if (combo > 2) {
            attack = @max(std.math.log1p(canonical_combo * 1.25), attack);
        }
    }

    return multiplyGarbage(attack, subframe);
}

fn multiplyGarbage(attack: f64, subframe: u32) u16 {
    const frames_after_margin: f64 = @floatFromInt(subframe / 10 -| GARBAGE_MARGIN);
    const garbage_multiplier = 1.0 + (GARBAGE_INCREASE * frames_after_margin / replay.FRAMERATE);
    return @intFromFloat(attack * garbage_multiplier);
}

fn handleKeyUp(self: *Self, event: KeyEvent) void {
    switch (event.key) {
        .moveLeft => {
            if (self.left_shift_subframe) |t| {
                if (event.subframe > t) {
                    self.game.moveLeftAll();
                }
            }
            self.left_shift_subframe = null;
        },
        .moveRight => {
            if (self.right_shift_subframe) |t| {
                if (event.subframe > t) {
                    self.game.moveRightAll();
                }
            }
            self.right_shift_subframe = null;
        },
        .softDrop => {
            self.game.softDrop();
            self.softdropping = false;
        },
        else => {},
    }
}

fn addRow(self: *Self, info: ClearInfo, subframe: u32) !void {
    if (self.rating == null) {
        return;
    }

    var playfield: [400]u8 = undefined;
    for (0..BoardMask.HEIGHT) |y| {
        for (0..BoardMask.WIDTH) |x| {
            playfield[y * BoardMask.WIDTH + x] = colorToString(self.game.playfield_colors.get(x, y));
        }
    }

    const pos = self.game.state.current.canonicalPosition(self.game.state.pos);
    var next: [6]u8 = undefined;
    for (0..next.len) |i| {
        next[i] = colorToString(self.game.state.next_pieces[i].color());
    }
    var incoming: u16 = 0;
    for (self.garbage_queue.items) |garbage| {
        if (garbage.subframe == math.maxInt(u32)) {
            break;
        }
        incoming += garbage.lines;
    }

    try self.data.append(self.allocator, .{
        .game_id = self.id,
        .subframe = subframe,
        .playfield = playfield,
        .x = pos.x,
        .y = pos.y,
        .current = [_]u8{colorToString(self.game.state.current.kind.color())},
        .hold = [_]u8{colorToString(if (self.game.state.hold_kind) |h| h.color() else null)},
        .next = next,
        .attack = getTetrioAttack(info, self.game.state.b2b, self.game.state.combo, subframe),
        .t_spin = [_]u8{tSpinToString(info.t_spin)},
        .btb = self.game.state.b2b,
        .combo = self.game.state.combo,
        .incoming_garbage = incoming,
        .rating = self.rating.?,
        .glicko = self.glicko,
        .glicko_rd = self.glicko_rd,
    });
}

fn colorToString(color: ?Color) u8 {
    if (color == null) {
        return 'N';
    }
    return switch (color.?) {
        .Black => 'N',
        .BrightCyan => 'I',
        .BrightYellow => 'O',
        .BrightMagenta => 'T',
        .BrightGreen => 'S',
        .Red => 'Z',
        .Yellow => 'J',
        .Blue => 'L',
        .White => 'G',
        else => unreachable,
    };
}

fn tSpinToString(kind: engine.attack.TSpin) u8 {
    return switch (kind) {
        .None => 'N',
        .Mini => 'M',
        .Full => 'F',
    };
}
