const std = @import("std");
const Allocator = std.mem.Allocator;

const bit_masks = @import("bit_masks.zig");
const BoardMask = bit_masks.BoardMask;
const PieceMask = bit_masks.PieceMask;

const bags = @import("bags.zig");
const Bag = bags.Bag;

const pieces = @import("pieces.zig");
const Piece = pieces.Piece;
const PieceType = pieces.PieceType;
const Position = pieces.Position;
const Facing = pieces.Facing;
const Rotation = @import("kicks.zig").Rotation;

const Self = @This();
const KickFn = fn (Piece, Rotation) []const Position;

playfield: BoardMask = BoardMask{},
position: Position,
current: Piece,
hold: ?PieceType,
// We could use a ring buffer for next, but advancing the next pieces shouldn't
// occur too often so the performance impact would be minimal.
next: []PieceType,

allow180: bool,
bag: Bag,
kicksFn: *const KickFn,

pub fn init(allocator: Allocator, next_len: usize, allow180: bool, bag: Bag, kicksFn: *const KickFn) !Self {
    var game = Self{
        .position = undefined,
        .current = undefined,
        .hold = null,
        .next = try allocator.alloc(PieceType, next_len),
        .allow180 = allow180,
        .bag = bag.clone(),
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

fn nextPiece(self: *Self) void {
    const next_piece = self.next[0];
    self.position = next_piece.startPos();
    self.current = Piece{ .facing = .Up, .type = next_piece };

    std.mem.copyForwards(PieceType, self.next, self.next[1..]);
    self.next[self.next.len - 1] = self.bag.next();
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
    const y = 1 - i;
    for (0..4) |x| {
        _ = try writer.write(if (mask.get(x, y)) "██" else "  ");
    }
    _ = try writer.write("║ ");
}

fn drawPlayfieldRow(self: Self, writer: anytype, i: usize) !void {
    _ = try writer.write("║");
    const y = 19 - i;
    for (0..10) |x| {
        _ = try writer.write(if (self.playfield.get(x, y)) "██" else "  ");
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
    if (self.next[next_idx] == .O) {
        mask.shr(1);
    }

    const y = 1 - next_row;
    for (0..4) |x| {
        _ = try writer.write(if (mask.get(x, y)) "██" else "  ");
    }
    _ = try writer.write("║");
}
