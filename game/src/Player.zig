// TODO: Handle lose condition and animation
const std = @import("std");
const assert = std.debug.assert;
const Allocator = std.mem.Allocator;

const nterm = @import("nterm");
const Color = nterm.Color;
const View = nterm.View;

const root = @import("root.zig");
const AttackTable = root.attack.AttackTable;
const ClearInfo = root.attack.ClearInfo;
const ColorArray = @import("ColorArray.zig");
const KickFn = root.kicks.KickFn;
const PieceKind = root.pieces.PieceKind;
const Piece = root.pieces.Piece;
const Settings = root.GameSettings;
const sound = root.sound;
const Stat = root.GameSettings.Stat;

const IncomingGarbage = packed struct {
    /// The x position of the hole in the garbage.
    hole: u4,
    /// The number of lines of garbage.
    lines: u16,
    /// The time in miliseconds at which the garbage should be added.
    time: u44,
};
const GarbageQueue = std.BoundedArray(IncomingGarbage, 25);

pub fn Player(comptime Bag: type, comptime kicks: KickFn) type {
    const GameState = root.GameState(Bag, kicks);

    return struct {
        const Self = @This();

        pub const DISPLAY_W = 44;
        pub const DISPLAY_H = 24;

        name: []const u8,
        state: GameState,
        last_clear_info: ClearInfo = .{
            .b2b = false,
            .cleared = 0,
            .pc = false,
            .t_spin = .None,
        },
        /// The number of nanoseconds since the game started when the last clear info was displayed.
        last_clear_time: u64 = 0,
        playfield_colors: ColorArray,
        garbage_queue: GarbageQueue,
        view: View,
        players: []Self,
        settings: Settings,

        already_held: bool = false,
        last_kick: i8 = -1,
        move_count: u8 = 0,
        /// The number of nanoseconds since the game started when the last move was made.
        last_move_time: u64 = 0,
        soft_dropping: bool = false,
        vel: f32 = 0.0,

        // TODO: Group into stats struct
        /// The number of nanoseconds since the game started.
        time: u64 = 0,
        lines_cleared: u32 = 0,
        garbage_cleared: u32 = 0,
        pieces_placed: u32 = 0,
        lines_sent: u32 = 0,
        lines_received: u32 = 0,
        score: u64 = 0,
        current_piece_keys: u32 = 0,
        keys_pressed: u32 = 0,
        finesse: u32 = 0,

        pub fn init(
            name: []const u8,
            bag: Bag,
            view: View,
            players: []Self,
            settings: Settings,
        ) Self {
            return Self{
                .name = name,
                .state = GameState.init(bag),
                .playfield_colors = ColorArray.init(),
                .garbage_queue = GarbageQueue{},
                .view = view,
                .players = players,
                .settings = settings,
            };
        }

        /// Holds the current piece, or does nothing if the piece has already been held.
        pub fn hold(self: *Self) void {
            self.current_piece_keys +|= 1;
            if (self.already_held) {
                return;
            }

            self.state.hold();
            self.already_held = true;
            self.last_kick = -1;
            self.move_count = 0;
            self.last_move_time = self.time;
            sound.playSfx(.hold) catch {};
        }

        pub fn moveLeft(self: *Self, das: bool) void {
            if (!das) {
                self.current_piece_keys +|= 1;
            }

            if (self.state.slide(-1) == 0) {
                return;
            }

            self.last_kick = -1;
            if (self.move_count < self.settings.autolock_grace) {
                if (!das) {
                    self.move_count +|= 1;
                }
                self.last_move_time = self.time;
            }

            sound.playSfx(.move) catch {};
        }

        /// Assumes that the move was caused by DAS and does not count as an extra keypress.
        pub fn moveLeftAll(self: *Self) void {
            if (self.state.slide(-10) == 0) {
                return;
            }

            self.last_kick = -1;
            if (self.move_count < self.settings.autolock_grace) {
                self.last_move_time = self.time;
            }

            sound.playSfx(.move) catch {};
        }

        pub fn moveRight(self: *Self, das: bool) void {
            if (!das) {
                self.current_piece_keys +|= 1;
            }

            if (self.state.slide(1) == 0) {
                return;
            }

            self.last_kick = -1;
            if (self.move_count < self.settings.autolock_grace) {
                if (!das) {
                    self.move_count +|= 1;
                }
                self.last_move_time = self.time;
            }

            sound.playSfx(.move) catch {};
        }

        /// Assumes that the move was caused by DAS and does not count as an extra keypress.
        pub fn moveRightAll(self: *Self) void {
            if (self.state.slide(10) == 0) {
                return;
            }

            self.last_kick = -1;
            if (self.move_count < self.settings.autolock_grace) {
                self.last_move_time = self.time;
            }

            sound.playSfx(.move) catch {};
        }

        pub fn rotateCw(self: *Self) void {
            self.current_piece_keys +|= 1;
            const kick = self.state.rotate(.QuarterCw);
            if (kick == -1) {
                return;
            }

            self.last_kick = kick;
            if (self.move_count < self.settings.autolock_grace) {
                self.move_count +|= 1;
                self.last_move_time = self.time;
            }

            sound.playSfx(.rotate) catch {};
        }

        pub fn rotateDouble(self: *Self) void {
            self.current_piece_keys +|= 1;
            const kick = self.state.rotate(.Half);
            if (kick == -1) {
                return;
            }

            self.last_kick = kick;
            if (self.move_count < self.settings.autolock_grace) {
                self.move_count +|= 1;
                self.last_move_time = self.time;
            }

            sound.playSfx(.rotate) catch {};
        }

        pub fn rotateCcw(self: *Self) void {
            self.current_piece_keys +|= 1;
            const kick = self.state.rotate(.QuarterCCw);
            if (kick == -1) {
                return;
            }

            self.last_kick = kick;
            if (self.move_count < self.settings.autolock_grace) {
                self.move_count +|= 1;
                self.last_move_time = self.time;
            }

            sound.playSfx(.rotate) catch {};
        }

        pub fn softDrop(self: *Self) void {
            if (self.soft_dropping) {
                return;
            }
            self.soft_dropping = true;
        }

        pub fn hardDrop(self: *Self) void {
            const dropped = self.state.dropToGround();
            self.score += @intCast(dropped * 2);
            if (dropped > 0) {
                self.last_kick = -1;
            }

            sound.playSfx(.hard_drop) catch {};
            self.place();
        }

        fn place(self: *Self) void {
            const clear_info = self.lockCurrent();
            self.handleGarbage(clear_info.info.cleared > 0, clear_info.attack);
            self.updateStats(clear_info.info, clear_info.score, clear_info.attack);
            self.clearLines();

            // Only overwrite the last clear info if there's something interesting to display
            if (clear_info.info.cleared > 0 or clear_info.info.pc or clear_info.info.t_spin != .None) {
                self.last_clear_info = clear_info.info;
                self.last_clear_time = self.time;
            }

            self.state.nextPiece();
            self.already_held = false;
            self.last_kick = -1;
            self.move_count = 0;
            self.last_move_time = self.time;
            self.soft_dropping = false;
            self.vel = 0.0;

            if (clear_info.info.pc) {
                sound.playSfx(.perfect_clear) catch {};
            } else if (clear_info.info.t_spin != .None) {
                sound.playSfx(.t_spin) catch {};
            } else if (clear_info.info.cleared > 0) {
                sound.playSfx(switch (clear_info.info.cleared) {
                    1 => .single_clear,
                    2 => .double_clear,
                    3 => .triple_clear,
                    4 => .tetris_clear,
                    else => unreachable,
                }) catch {};
            }
        }

        fn handleGarbage(self: *Self, cleared: bool, attack: u16) void {
            // Counter garbage
            var i: usize = 0;
            var remaining_attack = attack;
            while (remaining_attack > 0 and i < self.garbage_queue.len) : (i += 1) {
                const garbage = &self.garbage_queue.buffer[i];
                const countered = @min(remaining_attack, garbage.lines);
                remaining_attack -= countered;
                garbage.lines -= countered;
                if (garbage.lines > 0) {
                    break;
                }
            }

            // Send remaining attack
            // TODO: targetting modes
            self.players[1].queueGarbage(
                null,
                remaining_attack,
                self.time + self.settings.garbage_delay * std.time.ns_per_ms,
            );

            // Receive garbage
            if (!cleared) {
                var remaining_garbage = self.settings.garbage_cap;
                while (remaining_garbage > 0 and i < self.garbage_queue.len) : (i += 1) {
                    const garbage = &self.garbage_queue.buffer[i];
                    const received = @min(remaining_garbage, garbage.lines);
                    self.addGarbage(garbage.hole, received);
                    remaining_garbage -= received;
                    garbage.lines -= received;
                    if (garbage.lines > 0) {
                        break;
                    }
                }
            }

            for (0..self.garbage_queue.len - i) |j| {
                self.garbage_queue.buffer[j] = self.garbage_queue.buffer[i + j];
            }
            self.garbage_queue.len -= @intCast(i);
        }

        fn lockCurrent(self: *Self) struct { info: ClearInfo, score: u64, attack: u16 } {
            // Place piece in playfield colors
            const start: u8 = @max(0, self.state.pos.y);
            for (start..@intCast(self.state.pos.y + 4)) |y| {
                var row = self.state.current.mask().rows[@intCast(@as(isize, @intCast(y)) - self.state.pos.y)];
                row = if (self.state.pos.x > 0)
                    row >> @intCast(self.state.pos.x)
                else
                    row << @intCast(-self.state.pos.x);

                for (0..10) |x| {
                    if ((row >> @intCast(10 - x)) & 1 == 1) {
                        self.playfield_colors.set(x, y, self.state.current.kind.color());
                    }
                }
            }

            // Scoring values taken from Tetris.wiki
            // https://tetris.wiki/Scoring#Recent_guideline_compatible_games
            const info = self.state.lockCurrent(self.last_kick);
            var clear_score = ([_]u64{ 0, 100, 300, 500, 800 })[info.cleared];
            clear_score += switch (info.t_spin) {
                .Mini => 100,
                .Full => ([_]u64{ 400, 700, 900, 1100 })[info.cleared],
                .None => 0,
            };
            if (info.b2b) {
                clear_score += clear_score / 2;
            }
            if (info.pc) {
                clear_score += ([_]u64{ 800, 1200, 1800, 2000 })[info.cleared - 1];
                if (info.b2b and info.cleared == 4) {
                    clear_score += 1200;
                }
            }
            if (self.state.combo > 1) {
                clear_score += 50 * (self.state.combo - 1);
            }
            const attack = self.settings.attack_table.getAttack(info, self.state.b2b, self.state.combo);

            return .{
                .info = info,
                .score = clear_score,
                .attack = attack,
            };
        }

        fn updateStats(self: *Self, info: ClearInfo, score: u64, attack: u16) void {
            self.score += score * self.level();
            self.lines_cleared += info.cleared;
            self.pieces_placed += 1;
            self.lines_sent += attack;
            // TODO: Calculate finesse
            self.keys_pressed += self.current_piece_keys;
            self.current_piece_keys = 0;
        }

        fn clearLines(self: *Self) void {
            var clears: usize = 0;
            var i: usize = @max(0, self.state.pos.y);
            while (i + clears < ColorArray.HEIGHT) {
                self.playfield_colors.copyRow(i, i + clears);
                if (!self.playfield_colors.isRowFull(i)) {
                    i += 1;
                    continue;
                }

                clears += 1;
                if (self.playfield_colors.isRowGarbage(i)) {
                    self.garbage_cleared += 1;
                }
            }
            while (i < ColorArray.HEIGHT) : (i += 1) {
                self.playfield_colors.emptyRow(i);
            }
        }

        /// Queues garbage to be added to the playfield.
        pub fn queueGarbage(self: *Self, hole: ?u4, lines: u16, time: u64) void {
            const resolved_hole = hole orelse std.crypto.random.intRangeLessThan(u4, 0, 10);
            if (self.garbage_queue.len < self.garbage_queue.capacity()) {
                self.garbage_queue.appendAssumeCapacity(.{
                    .hole = resolved_hole,
                    .lines = lines,
                    .time = @intCast(time / std.time.ns_per_ms),
                });
            } else {
                // Add extra garbage to last item if we run out of space
                self.garbage_queue.buffer[self.garbage_queue.len - 1].lines +|= lines;
            }
        }

        /// Adds garbage to the bottom of the playfield. `hole` is the x position of the
        /// hole, and `lines` is the number of lines of garbage to add.
        pub fn addGarbage(self: *Self, hole: u4, lines: u16) void {
            if (lines == 0) {
                return;
            }

            self.lines_received += lines;
            self.state.addGarbage(hole, lines);

            var i: usize = ColorArray.HEIGHT;
            while (i > lines) {
                i -= 1;
                self.playfield_colors.copyRow(i, i - lines);
            }

            for (0..ColorArray.WIDTH) |x| {
                self.playfield_colors.set(x, 0, if (x == hole)
                    ColorArray.EMPTY_COLOR
                else
                    ColorArray.GARBAGE_COLOR);
            }
            for (1..lines) |y| {
                self.playfield_colors.copyRow(y, 0);
            }
        }

        /// Advances the game.
        pub fn tick(self: *Self, nanoseconds: u64) void {
            const now = self.time + nanoseconds;
            const g = self.settings.g + if (self.soft_dropping) self.settings.soft_g else 0.0;
            self.vel += g * @as(f32, @floatFromInt(nanoseconds)) / std.time.ns_per_s;

            // Handle autolocking
            if (self.state.onGround()) {
                self.vel = 0.0;

                if (self.move_count > self.settings.autolock_grace or
                    now -| self.last_move_time >= self.settings.lock_delay * std.time.ns_per_ms)
                {
                    self.place();
                    sound.playSfx(.landing) catch {};
                }
            }

            // Handle gravity
            const dropped = self.state.drop(@intFromFloat(@min(255, self.vel)));
            self.vel -= @floatFromInt(dropped);
            if (self.soft_dropping) {
                self.score += dropped;
            }
            if (dropped > 0) {
                self.last_kick = -1;
                self.last_move_time = now;
            }

            self.time = now;
            self.soft_dropping = false;
        }

        /// Returns the current level
        pub fn level(self: Self) u64 {
            return (self.lines_cleared / 10) + 1;
        }

        /// Returns the current Attack Per Line (APL)
        pub fn apl(self: Self) f32 {
            if (self.lines_sent == 0) {
                return 0.0;
            }
            return @as(f32, @floatFromInt(self.lines_sent)) / @as(f32, @floatFromInt(self.lines_cleared));
        }

        /// Returns the current Attack Per Minute (APM)
        pub fn apm(self: Self) f32 {
            if (self.lines_sent == 0.0) {
                return 0.0;
            }
            return @as(f32, @floatFromInt(self.lines_sent)) / @as(f32, @floatFromInt(self.time)) * std.time.ns_per_min;
        }

        /// Returns the current Attack Per Piece (APP)
        pub fn app(self: Self) f32 {
            if (self.lines_sent == 0) {
                return 0.0;
            }
            return @as(f32, @floatFromInt(self.lines_sent)) / @as(f32, @floatFromInt(self.pieces_placed));
        }

        /// Returns the current Keys Per Piece (KPP)
        pub fn kpp(self: Self) f32 {
            if (self.pieces_placed == 0) {
                return 0.0;
            }
            return @as(f32, @floatFromInt(self.keys_pressed)) / @as(f32, @floatFromInt(self.pieces_placed));
        }

        /// Returns the current Pieces Per Second (PPS)
        pub fn pps(self: Self) f32 {
            if (self.pieces_placed == 0) {
                return 0.0;
            }
            return @as(f32, @floatFromInt(self.pieces_placed)) / @as(f32, @floatFromInt(self.time)) * std.time.ns_per_s;
        }

        /// Returns the current VS Score
        pub fn vsScore(self: Self) f32 {
            const sent_cleared: f32 = @floatFromInt(self.lines_sent + self.garbage_cleared);
            if (sent_cleared == 0.0) {
                return 0.0;
            }
            return 100.0 * sent_cleared / @as(f32, @floatFromInt(self.time)) * std.time.ns_per_s;
        }

        /// Draws the game elements to the game's allocated view.
        pub fn draw(self: Self) !void {
            try self.drawNameLines();
            self.drawHold();
            self.drawScoreLevel();
            try self.drawClearInfo();
            self.drawMatrix();
            self.drawGarbageMeter();
            self.drawNext();
            for (self.settings.display_stats, 0..) |stat, i| {
                self.drawStat(stat, @intCast(i));
            }
        }

        fn drawNameLines(self: Self) !void {
            try self.view.printAligned(.Center, 0, .White, .Black, "{s}", .{self.name});
            try self.view.printAligned(.Center, 1, .White, .Black, "LINES - {d}", .{self.lines_cleared});
        }

        fn drawPiece(view: View, x: i8, y: i8, piece: Piece, solid: bool) void {
            const mask = piece.mask();
            const color = piece.kind.color();
            for (0..4) |dy| {
                for (0..4) |dx| {
                    if (!mask.get(dx, dy)) {
                        continue;
                    }
                    const x2 = x + @as(i8, @intCast(dx * 2));
                    const y2 = y - @as(i8, @intCast(dy));
                    if (x2 < 0 or y2 < 0 or x2 >= view.width or y2 >= view.height) {
                        continue;
                    }

                    if (solid) {
                        _ = view.writeText(@intCast(x2), @intCast(y2), .Black, color, "  ");
                    } else {
                        _ = view.writeText(@intCast(x2), @intCast(y2), color, .Black, "▒▒");
                    }
                }
            }
        }

        fn drawHold(self: Self) void {
            const LEFT = 0;
            const TOP = 2;
            const WIDTH = 10;
            const HEIGHT = 5;

            const hold_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
            hold_box.drawBox(0, 0, WIDTH, HEIGHT);
            _ = hold_box.writeText(3, 0, .White, .Black, "HOLD");
            if (self.state.hold_kind) |hold_kind| {
                const hold_piece = Piece{
                    .facing = .Up,
                    .kind = hold_kind,
                };
                const y: i8 = if (hold_kind == .I) 4 else 3;
                drawPiece(hold_box, 1, y, hold_piece, true);
            }
        }

        fn drawScoreLevel(self: Self) void {
            const LEFT = 0;
            const TOP = 8;
            const WIDTH = 10;
            const HEIGHT = 6;

            const score_level_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
            score_level_box.drawBox(0, 0, WIDTH, HEIGHT);
            _ = score_level_box.writeText(1, 1, .White, .Black, "SCORE");
            printGlitchyU64(score_level_box, 1, 2, self.score);
            _ = score_level_box.writeText(1, 3, .White, .Black, "LEVEL");
            printGlitchyU64(score_level_box, 1, 4, self.level());
        }

        fn drawClearInfo(self: Self) !void {
            const LEFT = 0;
            const TOP = 15;
            const WIDTH = 10;
            const HEIGHT = 5;
            if (self.time - self.last_clear_time >= self.settings.clear_erase_dalay * std.time.ns_per_ms) {
                return;
            }

            const clear_info_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
            if (self.last_clear_info.b2b) {
                clear_info_box.writeAligned(.Center, 0, .White, .Black, "B2B");
            }
            switch (self.last_clear_info.t_spin) {
                .None => {},
                .Mini => clear_info_box.writeAligned(.Center, 1, .White, .Black, "T-SPIN MINI"),
                .Full => clear_info_box.writeAligned(.Center, 1, .White, .Black, "T-SPIN"),
            }
            switch (self.last_clear_info.cleared) {
                1 => clear_info_box.writeAligned(.Center, 2, .White, .Black, "SINGLE"),
                2 => clear_info_box.writeAligned(.Center, 2, .White, .Black, "DOUBLE"),
                3 => clear_info_box.writeAligned(.Center, 2, .White, .Black, "TRIPLE"),
                4 => clear_info_box.writeAligned(.Center, 2, .White, .Black, "TETRIS"),
                else => {},
            }
            if (self.state.combo > 1) {
                try clear_info_box.printAligned(.Center, 3, .White, .Black, "{d} COMBO!", .{self.state.combo - 1});
            }
            if (self.last_clear_info.pc) {
                clear_info_box.writeAligned(.Center, 4, .White, .Black, "ALL CLEAR!");
            }
        }

        fn printGlitchyU64(view: View, x: u8, y: u8, value: u64) void {
            if (value < 100_000_000) {
                view.printAt(x, y, .White, .Black, "{d}", .{value});
            } else if (value < 0x1_0000_0000) {
                // Print in hexadecimal if the score is too large
                view.printAt(x, y, .White, .Black, "{x}", .{value});
            } else {
                // Map bytes directly to characters if the score is still too large,
                // because glitched text is cool
                var bytes = [_]u16{undefined} ** 8;
                for (0..8) |i| {
                    const byte: u8 = @truncate(value >> @intCast(i * 8));
                    // Make sure byte is printable
                    bytes[7 - i] = if (byte < 95)
                        @as(u16, byte) + 32
                    else
                        @as(u16, byte) + 66;
                }
                const start = @clz(value) / 8;
                view.printAt(x, y, .White, .Black, "{s}", .{std.unicode.fmtUtf16le(bytes[start..8])});
            }
        }

        fn drawMatrix(self: Self) void {
            const LEFT = 11;
            const TOP = 2;
            const WIDTH = 22;
            const HEIGHT = 22;

            const matrix_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
            const matrix_box_inner = matrix_box.sub(1, 1, WIDTH - 2, HEIGHT - 2);
            matrix_box.drawBox(0, 0, WIDTH, HEIGHT);
            for (0..20) |y| {
                for (0..10) |x| {
                    const color = self.playfield_colors.get(x, y);
                    _ = matrix_box_inner.writeText(@intCast(x * 2), @intCast(19 - y), .Black, color, "  ");
                }
            }

            // Ghost piece
            var state = self.state;
            const dropped = state.dropToGround();
            drawPiece(matrix_box_inner, state.pos.x * 2, 19 - state.pos.y, state.current, false);

            // Current piece
            state.pos.y += @intCast(dropped);
            drawPiece(matrix_box_inner, state.pos.x * 2, 19 - state.pos.y, state.current, true);
        }

        fn drawGarbageMeter(self: Self) void {
            const LEFT = 33;
            const TOP = 3;
            const WIDTH = 1;
            const HEIGHT = 20;

            const view = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);

            var y: u16 = HEIGHT - 1;
            for (self.garbage_queue.slice()) |garbage| outer: {
                for (0..garbage.lines) |_| {
                    _ = view.writeText(
                        0,
                        y,
                        .Black,
                        if (@as(u64, garbage.time) * std.time.ns_per_ms <= self.time) .Red else .White,
                        "  ",
                    );

                    if (y == 0) {
                        break :outer;
                    }
                    y -= 1;
                }
            }
        }

        fn drawNext(self: Self) void {
            const LEFT = 34;
            const TOP = 2;
            const WIDTH = 10;

            if (self.settings.show_next_count == 0) {
                return;
            }

            const height = @as(u16, @intCast(self.settings.show_next_count)) * 3 + 1;
            const next_box = self.view.sub(LEFT, TOP, WIDTH, height);
            next_box.drawBox(0, 0, WIDTH, height);
            _ = next_box.writeText(3, 0, .White, .Black, "NEXT");

            for (0..self.settings.show_next_count) |i| {
                const piece = Piece{
                    .facing = .Up,
                    .kind = self.state.next_pieces[i],
                };
                const y: i8 = if (piece.kind == .I) 4 else 3;
                drawPiece(next_box, 1, y + @as(i8, @intCast(i * 3)), piece, true);
            }
        }

        fn drawStat(self: Self, stat: Stat, slot: u16) void {
            const top = 21 + slot;
            // Don't draw if stat slot is outside of view
            if (top >= self.view.height) {
                return;
            }

            const view = self.view.sub(0, top, 10, 1);
            switch (stat) {
                .APL => view.printAt(0, 0, .White, .Black, "APL: {d:.3}", .{self.apl()}),
                .APM => view.printAt(0, 0, .White, .Black, "APM: {d:.3}", .{self.apm()}),
                .APP => view.printAt(0, 0, .White, .Black, "APP: {d:.3}", .{self.app()}),
                .Finesse => view.printAt(0, 0, .White, .Black, "FIN: {d}", .{self.finesse}),
                .Keys => view.printAt(0, 0, .White, .Black, "KEYS: {d}", .{self.keys_pressed + self.current_piece_keys}),
                .KPP => view.printAt(0, 0, .White, .Black, "KPP: {d:.3}", .{self.kpp()}),
                .Level => view.printAt(0, 0, .White, .Black, "LEVEL: {d}", .{self.level()}),
                .Lines => view.printAt(0, 0, .White, .Black, "LINES: {d}", .{self.lines_cleared}),
                .PPS => view.printAt(0, 0, .White, .Black, "PPS: {d:.3}", .{self.pps()}),
                .Received => view.printAt(0, 0, .White, .Black, "REC: {d}", .{self.lines_received}),
                .Score => view.printAt(0, 0, .White, .Black, "SCORE: {d}", .{self.score}),
                .Sent => view.printAt(0, 0, .White, .Black, "SENT: {d}", .{self.lines_sent}),
                .Time => view.printAt(0, 0, .White, .Black, "TIME: {}", .{std.fmt.fmtDuration(self.time)}),
                .VsScore => view.printAt(0, 0, .White, .Black, "VS: {d:.4}", .{self.vsScore()}),
            }
        }
    };
}
