const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;

const root = @import("main.zig");
const Move = root.Move;
const ClearInfo = root.ClearInfo;

const BoardMask = root.bit_masks.BoardMask;
const PieceMask = root.bit_masks.PieceMask;

const Bag = root.bags.Bag;

const Piece = root.pieces.Piece;
const PieceType = root.pieces.PieceType;
const Position = root.pieces.Position;
const Facing = root.pieces.Facing;
const Rotation = root.kicks.Rotation;

const Self = @This();
const KickFn = fn (Piece, Rotation) []const Position;

playfield: BoardMask = BoardMask{},
pos: Position,
current: Piece,
hold: ?PieceType = null,
held: bool = false,
// We could use a ring buffer for next, but advancing the next pieces shouldn't
// occur too often so the performance impact would be minimal.
next: []PieceType,

allow180: bool,
bag: Bag,
kicksFn: *const KickFn,

b2b: ?u16 = null,
combo: ?u16 = 0,

pub fn init(allocator: Allocator, next_len: usize, allow180: bool, bag: Bag, kicksFn: *const KickFn) !Self {
    assert(next_len > 0);

    var game = Self{
        .pos = undefined,
        .current = undefined,
        .next = try allocator.alloc(PieceType, next_len),
        .allow180 = allow180,
        .bag = bag,
        .kicksFn = kicksFn,
    };
    for (game.next) |*piece| {
        piece.* = game.bag.next();
    }
    game.nextPiece();
    return game;
}

/// The allocator passed in must be the same one used to allocate the game.
pub fn deinit(self: Self, allocator: Allocator) void {
    allocator.free(self.next);
}

inline fn onGround(self: Self) bool {
    const pos = Position{ .x = self.pos.x, .y = self.pos.y - 1 };
    return self.playfield.collides(self.current.mask(), pos);
}

fn spawn(self: *Self, piece: PieceType) void {
    self.current = Piece{ .facing = .Up, .type = piece };

    // Try to drop immediately if possible
    self.pos = piece.startPos();
    if (!self.onGround()) {
        self.pos.y -= 1;
    }
}

fn nextPiece(self: *Self) void {
    self.spawn(self.next[0]);
    std.mem.copyForwards(PieceType, self.next, self.next[1..]);
    self.next[self.next.len - 1] = self.bag.next();
}

/// Returns a boolean indicating if the move was successful.
pub fn handleMove(self: *Self, move: Move) bool {
    return switch (move) {
        .Left, .DASLeft => self.slide(-1),
        .Right, .DASRight => self.slide(1),
        .Cw => unreachable,
        .ACw => unreachable,
        .Double => unreachable,
        .Hold => ret: {
            if (self.held) {
                break :ret false;
            }
            self.held = true;

            const current_type = self.current.type;
            if (self.hold) |hold| {
                self.spawn(hold);
            } else {
                self.nextPiece();
            }
            self.hold = current_type;
            break :ret true;
        },
        .Drop => ret: {
            if (!self.onGround()) {
                self.pos.y -= 1;
                break :ret true;
            }
            break :ret false;
        },
    };
}

fn slide(self: *Self, dx: i8) bool {
    self.pos.x += dx;
    if (self.playfield.collides(self.current.mask(), self.pos)) {
        self.pos.x -= dx;
        return false;
    }
    return true;
}

pub fn place(self: *Self) ClearInfo {
    _ = self;
    unreachable;
    // return ClearInfo{
    //     .b2b = false,
    //     .cleared = 0,
    //     .pc = self.playfield[0] == 0,
    //     .pos = self.pos,
    //     .trashSent = 0,
    //     .tSpin = .None,
    // };
}

/// For debugging
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
    if (self.next.len >= 7) {
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
    if (self.hold == null or i == 2) {
        _ = try writer.write("║        ║ ");
        return;
    }

    _ = try writer.write("║");
    const mask = (Piece{ .facing = .Up, .type = self.hold.? }).mask();
    const y = 2 - i;
    for (0..4) |x| {
        _ = try writer.write(if (mask.get(x, y)) "██" else "  ");
    }
    _ = try writer.write("║ ");
}

fn drawPlayfieldRow(self: Self, writer: anytype, i: usize) !void {
    _ = try writer.write("║");
    const y = 19 - i;
    const mask_y: usize = @as(u8, @bitCast(@as(i8, @truncate(@as(isize, @bitCast(y)))) - self.pos.y));
    for (0..10) |x| {
        if (self.playfield.get(x, y)) {
            // Playfield
            _ = try writer.write("██");
        } else if (mask_y >= 0 and mask_y < 4 and (self.current.mask().rows[mask_y] >> @truncate(@as(usize, @bitCast(10 - @as(isize, @bitCast(x)) + self.pos.x)))) & 1 == 1) {
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
    if (next_idx >= self.next.len) {
        return;
    }

    const next_row = i % 3;
    if (next_row == 2) {
        if (next_idx == self.next.len - 1) {
            _ = try writer.write("╚════════╝");
        } else {
            _ = try writer.write("║        ║");
        }
        return;
    }

    _ = try writer.write("║");
    var mask = (Piece{ .facing = .Up, .type = self.next[next_idx] }).mask();

    const y = @as(usize, if (self.next[next_idx] == .I) 3 else 2) - next_row;
    for (0..4) |x| {
        _ = try writer.write(if (mask.get(x, y)) "██" else "  ");
    }
    _ = try writer.write("║");
}
