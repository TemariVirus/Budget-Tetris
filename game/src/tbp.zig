//! Implements structures for the Tetris Bot Protocol.
//! See https://github.com/tetris-bot-protocol/tbp-spec.

const std = @import("std");
const json = std.json;
const Allocator = std.mem.Allocator;
const eql = std.mem.eql;

const root = @import("root.zig");
const BoardMask = root.bit_masks.BoardMask;
const Facing = root.pieces.Facing;
const Piece = root.pieces.Piece;
const PieceKind = root.pieces.PieceKind;
const Position = root.pieces.Position;
const TSpin = root.attack.TSpin;

pub const MessageType = enum {
    info,
    rules,
    ready,
    start,
    suggest,
    suggestion,
    play,
    new_piece,
    stop,
    quit,
};

pub const BotInfo = struct {
    name: []const u8,
    version: []const u8,
    author: []const u8,
    features: []const BotFeature,

    pub fn post(self: BotInfo, writer: anytype) !void {
        try json.stringify(.{
            .type = "info",
            .name = self.name,
            .version = self.version,
            .author = self.author,
            .features = self.features,
        }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const BotFeature = enum {
    NeededForSerializationAndShouldNotBeUsed,
};

pub const BotReady = struct {
    pub fn post(writer: anytype) !void {
        try json.stringify(.{ .type = "ready" }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const BotSuggestion = struct {
    moves: []BotMove,

    pub fn post(self: BotSuggestion, writer: anytype) !void {
        try json.stringify(.{
            .type = "suggestion",
            .moves = self.moves,
        }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const BotMove = struct {
    location: BotMoveLocation,
    spin: BotMoveSpin,
};

pub const BotMoveLocation = struct {
    type: PieceKind,
    orientation: BotMoveOrientation,
    x: u4,
    y: u6,

    pub fn fromEngine(piece: Piece, pos: Position) BotMoveLocation {
        const orientation = BotMoveOrientation.fromEngine(piece.facing);
        const canonical_pos = piece.canonicalPosition(pos);

        return .{
            .type = piece.kind,
            .orientation = orientation,
            .x = canonical_pos.x,
            .y = canonical_pos.y,
        };
    }

    pub fn toEngine(self: BotMoveLocation) struct { Piece, Position } {
        const facing = BotMoveOrientation.toEngine(self.orientation);
        const piece = Piece{
            .facing = facing,
            .kind = self.type,
        };
        const pos = Piece.fromCanonicalPosition(piece, .{
            .x = self.x,
            .y = self.y,
        });

        return .{ piece, pos };
    }
};

pub const BotMoveOrientation = enum {
    north,
    east,
    south,
    west,

    pub fn fromEngine(facing: Facing) BotMoveOrientation {
        return switch (facing) {
            .Up => .north,
            .Right => .east,
            .Down => .south,
            .Left => .west,
        };
    }

    pub fn toEngine(self: BotMoveOrientation) Facing {
        return switch (self) {
            .north => .Up,
            .east => .Right,
            .south => .Down,
            .west => .Left,
        };
    }
};

pub const BotMoveSpin = enum {
    none,
    mini,
    full,

    pub fn fromEngine(tspin: TSpin) BotMoveSpin {
        return switch (tspin) {
            .None => .none,
            .Mini => .mini,
            .Full => .full,
        };
    }

    pub fn toEngine(self: BotMoveSpin) TSpin {
        return switch (self) {
            .none => .None,
            .mini => .Mini,
            .full => .Full,
        };
    }
};

pub const GameRules = struct {
    // randomizer: ?GameRandomizer, // TODO

    pub fn post(self: GameRules, writer: anytype) !void {
        _ = self;
        try json.stringify(.{
            .type = "rules",
            // .randomizer = self.randomizer, // TODO
        }, .{}, writer);
        try writer.writeAll("\n");
    }

    pub fn parse(allocator: Allocator, s: []const u8) !GameRules {
        return try json.parseFromSlice(GameRules, allocator, s, .{
            .ignore_unknown_fields = true,
        });
    }
};

pub const GameStart = struct {
    hold: ?PieceKind,
    queue: []const PieceKind,
    combo: u32,
    back_to_back: bool,
    board: [40][10]?BoardCell,

    pub fn post(self: GameStart, writer: anytype) !void {
        try json.stringify(.{
            .type = "start",
            .hold = self.hold,
            .queue = self.queue,
            .combo = self.combo,
            .back_to_back = self.back_to_back,
            .board = self.board,
        }, .{}, writer);
        try writer.writeAll("\n");
    }

    pub fn parse(allocator: Allocator, s: []const u8) !GameStart {
        return try json.parseFromSlice(GameStart, allocator, s, .{
            .ignore_unknown_fields = true,
        });
    }

    pub fn boardMask(self: GameStart) BoardMask {
        _ = self; // autofix
        @compileError("TODO");
    }
};

pub const BoardCell = enum {
    I,
    O,
    T,
    S,
    Z,
    L,
    J,
    G,
};

pub const GameSuggest = struct {
    pub fn post(writer: anytype) !void {
        try json.stringify(.{ .type = "suggest" }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const GameStop = struct {
    pub fn post(writer: anytype) !void {
        try json.stringify(.{ .type = "stop" }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const GameQuit = struct {
    pub fn post(writer: anytype) !void {
        try json.stringify(.{ .type = "quit" }, .{}, writer);
        try writer.writeAll("\n");
    }
};

const MessageTypeContainer = struct {
    type: []const u8,
};

pub fn messageType(allocator: Allocator, s: []const u8) ?MessageType {
    const parsed = json.parseFromSlice(
        MessageTypeContainer,
        allocator,
        s,
        .{ .ignore_unknown_fields = true },
    ) catch return null;
    defer parsed.deinit();

    inline for (@typeInfo(MessageType).Enum.fields) |f| {
        if (eql(u8, parsed.value.type, f.name)) {
            return @enumFromInt(f.value);
        }
    }
    return null;
}
