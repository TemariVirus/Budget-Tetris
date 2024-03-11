//! Represents the state of a game of Tetris. Handles the logic of moving
//! pieces and clearing lines, but not for handling game overs (i.e., block out
//! or top out), player input, or rendering.

const std = @import("std");
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;
const expect = std.testing.expect;

const root = @import("root.zig");
const TSpin = root.attack.TSpin;
const ClearInfo = root.attack.ClearInfo;

const BoardMask = root.bit_masks.BoardMask;
const PieceMask = root.bit_masks.PieceMask;

const pieces = root.pieces;
const Piece = pieces.Piece;
const PieceKind = pieces.PieceKind;
const Position = pieces.Position;
const Facing = pieces.Facing;

const KickFn = root.kicks.KickFn;
const Rotation = root.kicks.Rotation;

pub fn GameState(comptime BagImpl: type, comptime kicks: KickFn) type {
    return struct {
        playfield: BoardMask = BoardMask{},
        pos: Position = undefined,
        current: Piece = undefined,
        hold_kind: ?PieceKind = null,
        /// At most 7 next pieces can be displayed, so don't store more. If more
        /// lookaheads are required, use a copy of the current bag.
        next_pieces: [7]PieceKind = undefined,
        bag: Bag,
        b2b: u32 = 0,
        combo: u32 = 0,

        const Self = @This();
        const Bag = root.bags.Bag(BagImpl);

        pub fn init(bag: Bag) Self {
            var game = Self{ .bag = bag };
            for (&game.next_pieces) |*piece| {
                piece.* = game.bag.next();
            }
            game.nextPiece();
            return game;
        }

        /// Returns `true` if the given piece at the specified position would
        /// collide with the playfield.
        pub fn collides(self: Self, piece: Piece, pos: Position) bool {
            return self.playfield.collides(piece.mask(), pos);
        }

        /// Returns `true` if the current piece is touching the ground.
        pub fn onGround(self: Self) bool {
            const pos = Position{ .x = self.pos.x, .y = self.pos.y - 1 };
            return self.collides(self.current, pos);
        }

        /// Replaces the current piece with one of the specified piece kind and places
        /// it at the top of the playfield.
        pub fn spawn(self: *Self, piece: PieceKind) void {
            self.current = Piece{ .facing = .up, .kind = piece };

            // Try to drop immediately if possible
            self.pos = piece.startPos();
            if (!self.onGround()) {
                self.pos.y -= 1;
            }
        }

        /// Advances the current piece to the next piece in queue.
        pub fn nextPiece(self: *Self) void {
            self.spawn(self.next_pieces[0]);
            for (0..self.next_pieces.len - 1) |i| {
                self.next_pieces[i] = self.next_pieces[i + 1];
            }
            self.next_pieces[self.next_pieces.len - 1] = self.bag.next();
        }

        /// Holds the current piece. If no piece is held, the next piece is spawned.
        pub fn hold(self: *Self) void {
            const current_kind = self.current.kind;
            if (self.hold_kind) |h| {
                self.spawn(h);
            } else {
                self.nextPiece();
            }
            self.hold_kind = current_kind;
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
        pub fn dropToGround(self: *Self) u8 {
            return self.drop(self.playfield.rows.len);
        }

        /// Tries to rotate the current piece. Returns the index of the kick used. `-1`
        /// indicates that the rotation failed.
        pub fn rotate(self: *Self, rotation: Rotation) i8 {
            // O piece cannot rotate
            if (self.current.kind == .o) {
                return -1;
            }

            // TODO: Add setting to disable half rotations
            const old_piece = self.current;

            // Implicit (0, 0) kick
            self.current.facing = self.current.facing.rotate(rotation);
            if (!self.collides(self.current, self.pos)) {
                return 0;
            }

            for (kicks(old_piece, rotation), 1..) |kick, i| {
                const kicked_pos = self.pos.add(kick);
                if (!self.collides(self.current, kicked_pos)) {
                    self.pos = kicked_pos;
                    return @intCast(i);
                }
            }

            self.current = old_piece;
            return -1;
        }

        /// Locks the current piece at the current position,
        /// and clears lines if possible. Returns information about the clear.
        pub fn lockCurrent(self: *Self, last_kick: i8) ClearInfo {
            const t_spin = self.tSpinType(last_kick);

            self.playfield.place(self.current.mask(), self.pos);
            const cleared = self.clearLines();

            const is_clear = cleared > 0;
            const is_hard_clear = (cleared == 4 or t_spin == .full) and is_clear;

            if (is_hard_clear) {
                self.b2b += 1;
            } else if (is_clear and t_spin == .none) {
                self.b2b = 0;
            }

            if (is_clear) {
                self.combo += 1;
            } else {
                self.combo = 0;
            }

            return ClearInfo{
                .b2b = is_hard_clear and self.b2b > 1,
                .cleared = cleared,
                // If bottom row is empty, every row above must also be empty.
                .pc = is_clear and self.playfield.rows[0] == BoardMask.EMPTY_ROW,
                .t_spin = t_spin,
            };
        }

        /// Clears all filled lines in the playfield.
        /// Returns the number of lines cleared.
        pub fn clearLines(self: *Self) u3 {
            var cleared: u3 = 0;
            var i: usize = @max(0, self.pos.y);
            while (i + cleared < self.playfield.rows.len) {
                self.playfield.rows[i] = self.playfield.rows[i + cleared];
                if (self.playfield.rows[i] == BoardMask.FULL_ROW) {
                    cleared += 1;
                } else {
                    i += 1;
                }
            }
            while (i < self.playfield.rows.len) : (i += 1) {
                self.playfield.rows[i] = BoardMask.EMPTY_ROW;
            }
            return cleared;
        }

        const tspin_table = [16][4]TSpin{
            [_]TSpin{.none} ** 4, // 0000
            [_]TSpin{.none} ** 4, // 0001
            [_]TSpin{.none} ** 4, // 0010
            [_]TSpin{.none} ** 4, // 0011
            [_]TSpin{.none} ** 4, // 0100
            [_]TSpin{.none} ** 4, // 0101
            [_]TSpin{.none} ** 4, // 0110
            [_]TSpin{ .mini, .full, .full, .mini }, // 0111; No top left corner
            [_]TSpin{.none} ** 4, // 1000
            [_]TSpin{.none} ** 4, // 1001
            [_]TSpin{.none} ** 4, // 1010
            [_]TSpin{ .full, .full, .mini, .mini }, // 1011; No bottom left corner
            [_]TSpin{.none} ** 4, // 1100
            [_]TSpin{ .mini, .mini, .full, .full }, // 1101; No top right corner
            [_]TSpin{ .full, .mini, .mini, .full }, // 1110; No bottom right corner
            [_]TSpin{.full} ** 4, // 1111; All corners
        };
        /// Uses the 3 corner rule to detect T-spins. At least 3 of the 4 corners
        /// immediately adjacent to the T piece's center must be filled for a placement
        /// to be considered a T-spin. Line clears are not necessary. Walls are counted
        /// as filled blocks. A negative value of `last_kick` indicates that the last
        /// move was not a rotation.
        ///
        /// If both corners in "front" of the T piece are filled, it is a normal
        /// T-spin. If only 1 corner is filled, it is a T-spin mini, unless the last
        /// kick used was the SRS (1, -2) kick (the one used for TSTs).
        pub fn tSpinType(self: *Self, last_kick: i8) TSpin {
            if (self.current.kind != .t or last_kick < 0) {
                return .none;
            }

            const x = self.pos.x;
            const y = self.pos.y;

            const bottom = if (y < 0)
                0b101 // Solid floor
            else
                0b101 & self.playfield.rows[@intCast(y)] >> @intCast(8 - x);
            const top = 0b101 & self.playfield.rows[@intCast(y + 2)] >> @intCast(8 - x);
            const corners = bottom | (top << 1);

            // Special case for SRS (1, -2) kick
            if (last_kick == 4 and @popCount(corners) == 3) {
                return .full;
            }

            return tspin_table[corners][@intFromEnum(self.current.facing)];
        }

        /// Adds garbage to the bottom of the playfield. `hole` is the x position of the
        /// hole, and `lines` is the number of lines of garbage to add.
        pub fn addGarbage(self: *Self, hole: u4, lines: u16) void {
            if (lines == 0) {
                return;
            }

            var i: usize = BoardMask.HEIGHT;
            while (i > lines) {
                i -= 1;
                self.playfield.rows[i] = self.playfield.rows[i - lines];
            }

            self.playfield.rows[0] = BoardMask.FULL_ROW;
            self.playfield.set(hole, 0, false);
            for (1..@min(BoardMask.HEIGHT, lines)) |y| {
                self.playfield.rows[y] = self.playfield.rows[0];
            }
        }

        /// Restarts the game with the given seed. If `seed` is `null`, the seed is
        /// unchanged.
        pub fn restart(self: *Self, seed: ?u64) void {
            if (seed) |s| {
                self.bag.setSeed(s);
            }
            self.* = init(self.bag);
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
            _ = try writer.write(" ╚════════╝\n");
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
            if (self.hold_kind == null or i == 2) {
                _ = try writer.write("║        ║ ");
                return;
            }

            _ = try writer.write("║");
            const mask = (Piece{ .facing = .Up, .kind = self.hold_kind.? }).mask();
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
            const next_row = i % 3;

            if (next_row == 2) {
                _ = try writer.write("║        ║");
                return;
            }

            _ = try writer.write("║");
            var mask = (Piece{ .facing = .Up, .kind = self.next_pieces[next_idx] }).mask();

            const y = @as(usize, if (self.next_pieces[next_idx] == .I) 3 else 2) - next_row;
            for (0..4) |x| {
                _ = try writer.write(if (mask.get(x, y)) "██" else "  ");
            }
            _ = try writer.write("║");
        }
    };
}

test "DT cannon" {
    var game = GameState(root.bags.SevenBag, root.kicks.srsPlus)
        .init(.{ .context = root.bags.SevenBag.init(69) });

    // J piece
    game.hold();
    var kick = game.rotate(.quarter_ccw);
    try expect(kick != -1);
    try expect(game.slide(1) == 1);
    try expect(game.dropToGround() == 18);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // L piece
    kick = game.rotate(.quarter_cw);
    try expect(kick != -1);
    try expect(game.slide(3) == 3);
    try expect(game.dropToGround() == 18);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // T piece
    kick = -1;
    try expect(game.dropToGround() == 16);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // S piece
    kick = game.rotate(.quarter_cw);
    try expect(kick != -1);
    try expect(game.slide(10) == 4);
    try expect(game.dropToGround() == 18);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // O piece
    kick = -1;
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 19);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // I piece
    kick = game.rotate(.quarter_cw);
    try expect(kick != -1);
    try expect(game.slide(1) == 1);
    try expect(game.dropToGround() == 17);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // S piece
    kick = game.rotate(.quarter_cw);
    try expect(kick != -1);
    try expect(game.dropToGround() == 14);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // O piece
    kick = -1;
    try expect(game.slide(3) == 3);
    try expect(game.dropToGround() == 16);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // J piece
    kick = game.rotate(.quarter_cw);
    try expect(kick != -1);
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 16);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // Z piece
    game.hold();
    try expect(game.rotate(.quarter_ccw) != -1);
    try expect(game.slide(-1) == 1);
    try expect(game.dropToGround() == 15);
    try expect(game.rotate(.quarter_ccw) != -1);
    try expect(game.rotate(.half) != -1);
    try expect(game.rotate(.quarter_ccw) == -1);
    try expect(game.dropToGround() == 1);
    kick = game.rotate(.quarter_ccw);
    try expect(kick != -1);
    try expect(game.slide(10) == 1);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // Z piece
    game.hold();
    kick = game.rotate(.quarter_cw);
    try expect(kick != -1);
    try expect(game.slide(2) == 2);
    try expect(game.dropToGround() == 14);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // L piece
    kick = game.rotate(.quarter_ccw);
    try expect(kick != -1);
    try expect(game.slide(-1) == 1);
    try expect(game.dropToGround() == 14);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // I piece
    kick = game.rotate(.quarter_cw);
    try expect(kick != -1);
    try expect(game.slide(10) == 4);
    try expect(game.dropToGround() == 15);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 0,
        .pc = false,
        .t_spin = .none,
    }));
    game.nextPiece();

    // T piece
    game.hold();
    try expect(game.rotate(.quarter_cw) != -1);
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 13);
    try expect(game.rotate(.quarter_ccw) != -1);
    try expect(game.rotate(.quarter_ccw) != -1);
    try expect(game.rotate(.quarter_ccw) == -1);
    try expect(game.dropToGround() == 1);
    kick = game.rotate(.quarter_ccw);
    try expect(kick != -1);
    try expect(game.dropToGround() == 0);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = false,
        .cleared = 2,
        .pc = false,
        .t_spin = .full,
    }));
    game.nextPiece();

    // T piece
    game.hold();
    try expect(game.rotate(.quarter_cw) != -1);
    try expect(game.slide(-10) == 4);
    try expect(game.dropToGround() == 15);
    try expect(game.rotate(.quarter_ccw) != -1);
    kick = game.rotate(.quarter_ccw);
    try expect(kick != -1);
    try expect(game.rotate(.quarter_ccw) == -1);
    try expect(std.meta.eql(game.lockCurrent(kick), ClearInfo{
        .b2b = true,
        .cleared = 3,
        .pc = false,
        .t_spin = .full,
    }));
    game.nextPiece();

    const end_playfield = BoardMask{
        .rows = [_]u16{
            0b11111_0001111101_1,
            0b11111_0011100100_1,
        } ++ [_]u16{BoardMask.EMPTY_ROW} ** 38,
    };
    for (0..end_playfield.rows.len) |i| {
        try expect(game.playfield.rows[i] == end_playfield.rows[i]);
    }
}
