//! Represents the state of a game of Tetris. Handles the logic of moving
//! pieces and clearing lines, but not for handling game overs (i.e., block out
//! or top out).

const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const expect = std.testing.expect;

const root = @import("main.zig");
const TSpin = root.TSpin;
const ClearInfo = root.ClearInfo;

const BoardMask = root.bit_masks.BoardMask;
const PieceMask = root.bit_masks.PieceMask;

const Bag = root.bags.Bag;

const pieces = root.pieces;
const Piece = pieces.Piece;
const PieceType = pieces.PieceType;
const Position = pieces.Position;
const Facing = pieces.Facing;
const Rotation = root.kicks.Rotation;

const Self = @This();
const KickFn = fn (Piece, Rotation) []const Position;

playfield: BoardMask = BoardMask{},
pos: Position,
current: Piece,
hold_type: ?PieceType = null,
// We could use a ring buffer for next, but advancing the next pieces shouldn't
// occur too often so the performance impact would be minimal.
next_types: []PieceType,

bag: Bag,
kicksFn: *const KickFn,

b2b: u16 = 0,
combo: u16 = 0,

pub fn init(allocator: Allocator, next_len: usize, bag: Bag, kicksFn: *const KickFn) !Self {
    assert(next_len > 0);

    var game = Self{
        .pos = undefined,
        .current = undefined,
        .next_types = try allocator.alloc(PieceType, next_len),
        .bag = bag,
        .kicksFn = kicksFn,
    };
    for (game.next_types) |*piece| {
        piece.* = game.bag.next();
    }
    game.nextPiece();
    return game;
}

/// The allocator passed in must be the same one used to allocate the game.
pub fn deinit(self: Self, allocator: Allocator) void {
    allocator.free(self.next_types);
}

/// The cannonical B2B is one less than the stored value.
pub fn canonicalB2B(self: Self) ?u16 {
    if (self.b2b == 0) {
        return null;
    }
    return self.b2b - 1;
}

pub fn setCanonicalB2B(self: Self, value: ?u16) void {
    if (value) |v| {
        self.b2b = v + 1;
    } else {
        self.b2b = 0;
    }
}

/// The cannonical combo is one less than the stored value.
pub fn canonicalcombo(self: Self) ?u16 {
    if (self.combo == 0) {
        return null;
    }
    return self.combo - 1;
}

pub fn setCanonicalcombo(self: Self, value: ?u16) void {
    if (value) |v| {
        self.combo = v + 1;
    } else {
        self.combo = 0;
    }
}

inline fn collides(self: Self, piece: Piece, pos: Position) bool {
    return self.playfield.collides(piece.mask(), pos);
}

inline fn onGround(self: Self) bool {
    const pos = Position{ .x = self.pos.x, .y = self.pos.y - 1 };
    return self.collides(self.current, pos);
}

/// Replaces the current piece with one of the specified piece type and places
/// it at the top of the playfield.
pub fn spawn(self: *Self, piece: PieceType) void {
    self.current = Piece{ .facing = .Up, .type = piece };

    // Try to drop immediately if possible
    self.pos = piece.startPos();
    if (!self.onGround()) {
        self.pos.y -= 1;
    }
}

/// Advances the current piece to the next piece in queue.
pub fn nextPiece(self: *Self) void {
    self.spawn(self.next_types[0]);
    std.mem.copyForwards(PieceType, self.next_types, self.next_types[1..]);
    self.next_types[self.next_types.len - 1] = self.bag.next();
}

/// Holds the current piece. If no piece is held, the next piece is spawned.
pub fn hold(self: *Self) void {
    const current_type = self.current.type;
    if (self.hold_type) |h| {
        self.spawn(h);
    } else {
        self.nextPiece();
    }
    self.hold_type = current_type;
}

/// Tries to slide as far as possible. Returns the number of cells moved.
pub fn slide(self: *Self, dx: i8) u8 {
    const d: i8 = if (dx > 0) 1 else -1;
    const steps: u8 = @abs(dx);
    for (0..steps) |i| {
        self.pos.x += d;
        if (self.playfield.collides(self.current.mask(), self.pos)) {
            self.pos.x -= d;
            return @intCast(i);
        }
    }
    return steps;
}

/// Drops down dy cells, or stops partway if blocked.
/// Returns the number of cells moved.
pub fn drop(self: *Self, dy: u8) u8 {
    for (0..dy) |i| {
        self.pos.y -= 1;
        if (self.playfield.collides(self.current.mask(), self.pos)) {
            self.pos.y += 1;
            return @intCast(i);
        }
    }
    return dy;
}

/// Drops down until the current piece is touching the ground.
/// Returns the number of cells moved.
pub inline fn dropToGround(self: *Self) u8 {
    return self.drop(self.playfield.rows.len);
}

/// Tries to rotate the current piece.
/// Returns whether the rotation was successful.
pub fn rotate(self: *Self, rotation: Rotation) bool {
    const old_piece = self.current;

    self.current.facing = self.current.facing.rotate(rotation);
    if (!self.collides(self.current, self.pos)) {
        return true;
    }

    for (self.kicksFn(old_piece, rotation)) |kick| {
        const kicked_pos = self.pos.add(kick);
        if (!self.collides(self.current, kicked_pos)) {
            self.pos = kicked_pos;
            return true;
        }
    }

    self.current = old_piece;
    return false;
}

/// Locks the current piece at the current position,
/// and clears lines if possible. Returns information about the clear.
pub fn lock(self: *Self, rotated_last: bool) ClearInfo {
    const t_spin = self.tSpinType(rotated_last);

    self.playfield.place(self.current.mask(), self.pos);
    const cleared = self.clearLines();

    const is_clear = cleared > 0;
    const is_hard_clear = (cleared == 4) or (t_spin == .Full and is_clear);

    if (is_hard_clear) {
        self.b2b += 1;
    } else if (is_clear and t_spin == .None) {
        self.b2b = 0;
    }

    return ClearInfo{
        .b2b = is_hard_clear and self.b2b > 1,
        .cleared = cleared,
        .pc = self.playfield.rows[0] == BoardMask.empty_row,
        .t_spin = t_spin,
    };
}

/// Clears all filled lines in the playfield.
/// Returns the number of lines cleared.
fn clearLines(self: *Self) u8 {
    var cleared: u8 = 0;
    var i: u8 = 0;
    while (i + cleared < self.playfield.rows.len) {
        self.playfield.rows[i] = self.playfield.rows[i + cleared];
        if (self.playfield.rows[i + cleared] == BoardMask.full_row) {
            cleared += 1;
        } else {
            i += 1;
        }
    }
    while (i < self.playfield.rows.len) : (i += 1) {
        self.playfield.rows[i] = BoardMask.empty_row;
    }
    return cleared;
}

/// Uses the 3 corner rule to detect T-spins. At least 3 of the 4 corners
/// immediately adjacent to the T piece's center must be filled for a placement
/// to be considered a T-spin. Line clears are not necessary. Walls are counted
/// as filled blocks.
/// If both corners in "front" of the T piece are filled, it is a normal
/// T-spin. If only 1 corner is filled, it is a T-spin mini.
fn tSpinType(self: *Self, rotated_last: bool) TSpin {
    const all = comptime pieces.parsePiece(
        \\...
        \\#.#
        \\...
        \\#.#
    ).rows;
    const no_br = comptime pieces.parsePiece(
        \\...
        \\#.#
        \\...
        \\#..
    ).rows;
    const no_bl = comptime pieces.parsePiece(
        \\...
        \\#.#
        \\...
        \\..#
    ).rows;
    const no_tl = comptime pieces.parsePiece(
        \\...
        \\..#
        \\...
        \\#.#
    ).rows;
    const no_tr = comptime pieces.parsePiece(
        \\...
        \\#..
        \\...
        \\#.#
    ).rows;

    if (!rotated_last or self.current.type != .T) {
        return .None;
    }

    const corners = blk: {
        var c: [2]u16 = undefined;
        const x = self.pos.x;
        const y = self.pos.y;
        if (y == -1) {
            c[0] = all[0];
        } else {
            c[0] = if (x == -1)
                self.playfield.rows[@intCast(y)] >> 1
            else
                self.playfield.rows[@intCast(y)] << @intCast(x);
            c[0] &= all[0];
        }

        c[1] = if (x == -1)
            self.playfield.rows[@intCast(y + 2)] >> 1
        else
            self.playfield.rows[@intCast(y + 2)] << @intCast(x);
        c[1] &= all[2];

        break :blk c;
    };

    if (corners[0] == all[0] and corners[1] == all[2]) {
        return .Full;
    }
    if (corners[0] == no_br[0] and corners[1] == no_br[2]) {
        return switch (self.current.facing) {
            .Up, .Left => .Full,
            .Right, .Down => .Mini,
        };
    }
    if (corners[0] == no_bl[0] and corners[1] == no_bl[2]) {
        return switch (self.current.facing) {
            .Up, .Right => .Full,
            .Down, .Left => .Mini,
        };
    }
    if (corners[0] == no_tl[0] and corners[1] == no_tl[2]) {
        return switch (self.current.facing) {
            .Right, .Down => .Full,
            .Up, .Left => .Mini,
        };
    }
    if (corners[0] == no_tr[0] and corners[1] == no_tr[2]) {
        return switch (self.current.facing) {
            .Down, .Left => .Full,
            .Up, .Right => .Mini,
        };
    }
    return .None;
}

// For debugging
pub fn format(self: Self, comptime fmt: []const u8, options: std.fmt.FormatOptions, writer: anytype) !void {
    _ = fmt;
    _ = options;

    _ = try writer.write("╔══HOLD══╗ ╔════════════════════╗ ╔══NEXT══╗\n");
    for (0..20) |i| {
        try self.drawHoldRow(writer, i);
        try self.drawPlayfieldRow(writer, i);
        try self.drawNextRow(writer, i);
        _ = try writer.write("\n");
    }
    _ = try writer.write("           ╚════════════════════╝");
    if (self.next_types.len >= 7) {
        _ = try writer.write(" ╚════════╝");
    }
    _ = try writer.write("\n");
}

fn drawHoldRow(self: Self, writer: anytype, i: usize) !void {
    if (i == 3) {
        _ = try writer.write("╚════════╝ ");
        return;
    }
    if (i > 3) {
        _ = try writer.write("           ");
        return;
    }
    if (self.hold_type == null or i == 2) {
        _ = try writer.write("║        ║ ");
        return;
    }

    _ = try writer.write("║");
    const mask = (Piece{ .facing = .Up, .type = self.hold_type.? }).mask();
    const y = 2 - i;
    for (0..4) |x| {
        _ = try writer.write(if (mask.get(x, y)) "██" else "  ");
    }
    _ = try writer.write("║ ");
}

fn drawPlayfieldRow(self: Self, writer: anytype, i: usize) !void {
    _ = try writer.write("║");
    const y: u8 = @intCast(19 - i);
    const mask_y: u8 = y -% @as(u8, @bitCast(self.pos.y));
    for (0..10) |x| {
        const current_shift: i8 = 10 - @as(i8, @intCast(x)) + self.pos.x;
        const current_mask = self.current.mask();

        if (self.playfield.get(x, y)) {
            // Playfield
            _ = try writer.write("██");
        } else if (mask_y < 4 and current_shift >= 0 and current_shift < 16 and (current_mask.rows[mask_y] >> @intCast(current_shift)) & 1 == 1) {
            // Current piece
            _ = try writer.write("██");
        } else {
            _ = try writer.write("  ");
        }
    }
    _ = try writer.write("║ ");
}

fn drawNextRow(self: Self, writer: anytype, i: usize) !void {
    const next_idx = @divTrunc(i, 3);
    if (next_idx >= self.next_types.len) {
        return;
    }

    const next_row = i % 3;
    if (next_row == 2) {
        if (next_idx == self.next_types.len - 1) {
            _ = try writer.write("╚════════╝");
        } else {
            _ = try writer.write("║        ║");
        }
        return;
    }

    _ = try writer.write("║");
    var mask = (Piece{ .facing = .Up, .type = self.next_types[next_idx] }).mask();

    const y = @as(usize, if (self.next_types[next_idx] == .I) 3 else 2) - next_row;
    for (0..4) |x| {
        _ = try writer.write(if (mask.get(x, y)) "██" else "  ");
    }
    _ = try writer.write("║");
}

// TODO: check clear info
test "DT cannon" {
    var allocator = std.testing.allocator;

    var b = root.bags.SevenBag.init();
    var bag = b.bag();
    bag.setSeed(69);
    var game = try init(allocator, 6, bag, root.kicks.srsPlus);
    defer game.deinit(allocator);

    // J piece
    game.hold();
    try expect(game.rotate(.CCw));
    try expect(game.slide(1) == 1);
    try expect(game.dropToGround() == 18);
    _ = game.lock(false);
    game.nextPiece();

    // L piece
    try expect(game.rotate(.Cw));
    try expect(game.slide(3) == 3);
    try expect(game.dropToGround() == 18);
    _ = game.lock(false);
    game.nextPiece();

    // T piece
    try expect(game.dropToGround() == 16);
    _ = game.lock(false);
    game.nextPiece();

    // S piece
    try expect(game.rotate(.Cw));
    try expect(game.slide(10) == 4);
    try expect(game.dropToGround() == 18);
    _ = game.lock(false);
    game.nextPiece();

    // O piece
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 19);
    _ = game.lock(false);
    game.nextPiece();

    // I piece
    try expect(game.rotate(.Cw));
    try expect(game.slide(1) == 1);
    try expect(game.dropToGround() == 17);
    _ = game.lock(false);
    game.nextPiece();

    // S piece
    try expect(game.rotate(.Cw));
    try expect(game.dropToGround() == 14);
    _ = game.lock(false);
    game.nextPiece();

    // O piece
    try expect(game.slide(3) == 3);
    try expect(game.dropToGround() == 16);
    _ = game.lock(false);
    game.nextPiece();

    // J piece
    try expect(game.rotate(.Cw));
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 16);
    _ = game.lock(false);
    game.nextPiece();

    // Z piece
    game.hold();
    try expect(game.rotate(.CCw));
    try expect(game.slide(-1) == 1);
    try expect(game.dropToGround() == 15);
    try expect(game.rotate(.CCw));
    try expect(game.rotate(.Double));
    try expect(!game.rotate(.CCw));
    try expect(game.dropToGround() == 1);
    try expect(game.rotate(.CCw));
    try expect(game.slide(10) == 1);
    _ = game.lock(false);
    game.nextPiece();

    // Z piece
    game.hold();
    try expect(game.rotate(.Cw));
    try expect(game.slide(2) == 2);
    try expect(game.dropToGround() == 14);
    _ = game.lock(false);
    game.nextPiece();

    // L piece
    try expect(game.rotate(.CCw));
    try expect(game.slide(-1) == 1);
    try expect(game.dropToGround() == 14);
    _ = game.lock(false);
    game.nextPiece();

    // I piece
    try expect(game.rotate(.Cw));
    try expect(game.slide(10) == 4);
    try expect(game.dropToGround() == 15);
    _ = game.lock(false);
    game.nextPiece();

    // T piece
    game.hold();
    try expect(game.rotate(.Cw));
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 13);
    try expect(game.rotate(.CCw));
    try expect(game.rotate(.CCw));
    try expect(!game.rotate(.CCw));
    try expect(game.dropToGround() == 1);
    try expect(game.rotate(.CCw));
    _ = game.lock(true);
    game.nextPiece();

    // T piece
    game.hold();
    try expect(game.rotate(.Cw));
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 15);
    try expect(game.rotate(.CCw));
    try expect(game.rotate(.CCw));
    try expect(!game.rotate(.CCw));
    _ = game.lock(true);
    game.nextPiece();

    const end_playfield = BoardMask{
        .rows = [_]u16{
            0b11111_0001111101_1,
            0b11111_0011100100_1,
        } ++ [_]u16{BoardMask.empty_row} ** 38,
    };
    for (0..end_playfield.rows.len) |i| {
        try expect(game.playfield.rows[i] == end_playfield.rows[i]);
    }
}
