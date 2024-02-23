//! Implements structures for the Tetris Bot Protocol.
//! See https://github.com/tetris-bot-protocol/tbp-spec.

const std = @import("std");
const json = std.json;
const Allocator = std.mem.Allocator;
const Parsed = json.Parsed;
const assert = std.debug.assert;
const eql = std.mem.eql;

const root = @import("root.zig");
const BoardMask = root.bit_masks.BoardMask;
const Facing = root.pieces.Facing;
const GameState = root.GameState(SevenBag, kicks.srs);
const kicks = root.kicks;
const Piece = root.pieces.Piece;
const PieceKind = root.pieces.PieceKind;
const Position = root.pieces.Position;
const SevenBag = root.bags.SevenBag;
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

    pub fn parse(allocator: Allocator, s: []const u8) !Parsed(BotInfo) {
        return try json.parseFromSlice(BotInfo, allocator, s, .{
            .ignore_unknown_fields = true,
        });
    }

    pub fn post(self: BotInfo, writer: anytype) !void {
        for (self.features) |feature| {
            assert(feature != .needed_for_serialization_and_should_not_be_used);
        }

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
    needed_for_serialization_and_should_not_be_used,
};

pub const BotReady = struct {
    pub fn post(writer: anytype) !void {
        try json.stringify(.{ .type = "ready" }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const BotSuggestion = struct {
    moves: []const BotMove,
    move_info: ?BotMoveInfo = null,

    pub fn parse(allocator: Allocator, s: []const u8) !Parsed(BotSuggestion) {
        return try json.parseFromSlice(BotSuggestion, allocator, s, .{
            .ignore_unknown_fields = true,
        });
    }

    pub fn post(self: BotSuggestion, writer: anytype) !void {
        try json.stringify(.{
            .type = "suggestion",
            .moves = self.moves,
            .move_info = self.move_info,
        }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const BotMove = struct {
    location: BotMoveLocation,
    spin: BotMoveSpin,

    pub fn fromEngine(piece: Piece, pos: Position, spin: TSpin) BotMove {
        return .{
            .location = BotMoveLocation.fromEngine(piece, pos),
            .spin = BotMoveSpin.fromEngine(spin),
        };
    }

    pub fn toEngine(self: BotMove) struct { piece: Piece, pos: Position, spin: TSpin } {
        const piece_pos = self.location.toEngine();
        const spin = BotMoveSpin.toEngine(self.spin);

        return .{
            .piece = piece_pos[0],
            .pos = piece_pos[1],
            .spin = spin,
        };
    }
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
            .up => .north,
            .right => .east,
            .down => .south,
            .left => .west,
        };
    }

    pub fn toEngine(self: BotMoveOrientation) Facing {
        return switch (self) {
            .north => .up,
            .east => .right,
            .south => .down,
            .west => .left,
        };
    }
};

pub const BotMoveSpin = enum {
    none,
    mini,
    full,

    pub fn fromEngine(tspin: TSpin) BotMoveSpin {
        return switch (tspin) {
            .none => .none,
            .mini => .mini,
            .full => .full,
        };
    }

    pub fn toEngine(self: BotMoveSpin) TSpin {
        return switch (self) {
            .none => .none,
            .mini => .mini,
            .full => .full,
        };
    }
};

pub const BotMoveInfo = struct {
    nodes: ?f64 = null,
    nps: ?f64 = null,
    depth: ?f64 = null,
    extra: ?[]const u8 = null,
};

pub const GameRules = struct {
    pub fn post(self: GameRules, writer: anytype) !void {
        _ = self;
        try json.stringify(.{
            .type = "rules",
        }, .{}, writer);
        try writer.writeAll("\n");
    }

    pub fn parse(allocator: Allocator, s: []const u8) !Parsed(GameRules) {
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

    pub fn parse(allocator: Allocator, s: []const u8) !Parsed(GameStart) {
        return try json.parseFromSlice(GameStart, allocator, s, .{
            .ignore_unknown_fields = true,
        });
    }

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

    pub fn toGamestate(self: GameStart) GameState {
        var playfield = BoardMask{};
        for (self.board, 0..) |row, y| {
            for (row, 0..) |cell, x| {
                if (cell == null) {
                    continue;
                }
                playfield.set(x, y, true);
            }
        }

        var gamestate = GameState{
            .playfield = playfield,
            .pos = self.queue[0].startPos(),
            .current = .{
                .facing = .up,
                .kind = self.queue[0],
            },
            .hold_kind = self.hold,
            .next_pieces = undefined,

            .bag = SevenBag.init(0),
            .b2b = undefined,
            .combo = undefined,
        };
        for (self.queue[1..], 0..) |kind, i| {
            gamestate.next_pieces[i] = kind;
        }
        gamestate.combo = self.combo;
        gamestate.b2b = if (self.back_to_back) 1 else 0;

        return gamestate;
    }
};

pub const BoardCell = enum {
    i,
    o,
    t,
    s,
    z,
    l,
    j,
    g,
};

pub const GameSuggest = struct {
    pub fn post(writer: anytype) !void {
        try json.stringify(.{ .type = "suggest" }, .{}, writer);
        try writer.writeAll("\n");
    }
};

pub const GamePlay = struct {
    move: BotMove,

    pub fn parse(allocator: Allocator, s: []const u8) !Parsed(GamePlay) {
        return try json.parseFromSlice(GamePlay, allocator, s, .{
            .ignore_unknown_fields = true,
        });
    }
};

pub const GameNewPiece = struct {
    piece: PieceKind,

    pub fn parse(allocator: Allocator, s: []const u8) !Parsed(GameNewPiece) {
        return try json.parseFromSlice(GameNewPiece, allocator, s, .{
            .ignore_unknown_fields = true,
        });
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
