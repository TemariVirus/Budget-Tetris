// TODO: Add sound
// TODO: Store last clear type and time and print it
// TODO: Store stats and make configurable display
const std = @import("std");
const root = @import("root.zig");
const nterm = @import("nterm");

const ClearInfo = root.attack.ClearInfo;
const GameState = root.GameState;
const PieceKind = root.pieces.PieceKind;
const Piece = root.pieces.Piece;

const Color = nterm.Color;
const View = nterm.View;

const milliTimestamp = std.time.milliTimestamp;

const Self = @This();

pub const DISPLAY_W = 44;
pub const DISPLAY_H = 24;

const empty_color = Color.Black;
const garbage_color = Color.BrightBlack;

// TODO: Extract settings to config
const AUTOLOCK_GRACE = 15;
const G = 1.5; // Multiply by framerate before passing to Game
const SOFT_G = 40;
const LOCK_DELAY = 500;
const CLEAR_ERASE_DELAY = 1000;

name: []const u8,
state: GameState,
playfield_colors: [40][10]Color,
last_clear_info: ClearInfo,
last_clear_time: i64,
view: View,

already_held: bool,
just_rotated: bool,
move_count: u8,
last_move_time: i64,
last_tick_time: i64,
softdropping: bool,
vel: f32,

start_time: i64,
cleared: u64,
garbage_cleared: u64,
placed: u64,
lines_sent: u64,
score: u64,
keys_pressed: u64,
last_keys_pressed: u64,
finesse: u64,

pub fn init(name: []const u8, state: GameState, view: View) Self {
    const now = milliTimestamp();
    return Self{
        .name = name,
        .state = state,
        .playfield_colors = [_][10]Color{[_]Color{empty_color} ** 10} ** 40,
        .last_clear_info = .{
            .b2b = false,
            .cleared = 0,
            .pc = false,
            .t_spin = .None,
        },
        .last_clear_time = now,
        .view = view,

        .already_held = false,
        .just_rotated = false,
        .move_count = 0,
        .last_move_time = now,
        .last_tick_time = now,
        .softdropping = false,
        .vel = 0.0,

        .start_time = now,
        .cleared = 0,
        .garbage_cleared = 0,
        .placed = 0,
        .lines_sent = 0,
        .score = 0,
        .keys_pressed = 0,
        .last_keys_pressed = 0,
        .finesse = 0,
    };
}

pub fn moveLeft(self: *Self) void {
    self.keys_pressed += 1;
    if (self.state.slide(-1) == 0) {
        return;
    }

    self.just_rotated = false;
    if (self.move_count < AUTOLOCK_GRACE) {
        self.move_count += 1;
        self.last_move_time = milliTimestamp();
    }
}

pub fn moveLeftAll(self: *Self) void {
    if (self.state.slide(-10) == 0) {
        return;
    }

    self.just_rotated = false;
    if (self.move_count < AUTOLOCK_GRACE) {
        self.last_move_time = milliTimestamp();
    }
}

pub fn moveRight(self: *Self) void {
    self.keys_pressed += 1;
    if (self.state.slide(1) == 0) {
        return;
    }

    self.just_rotated = false;
    if (self.move_count < AUTOLOCK_GRACE) {
        self.move_count += 1;
        self.last_move_time = milliTimestamp();
    }
}

pub fn moveRightAll(self: *Self) void {
    if (self.state.slide(10) == 0) {
        return;
    }

    self.just_rotated = false;
    if (self.move_count < AUTOLOCK_GRACE) {
        self.last_move_time = milliTimestamp();
    }
}

pub fn rotateCw(self: *Self) void {
    self.keys_pressed += 1;
    if (!self.state.rotate(.Cw)) {
        return;
    }

    self.just_rotated = true;
    if (self.move_count < AUTOLOCK_GRACE) {
        self.move_count += 1;
        self.last_move_time = milliTimestamp();
    }
}

pub fn rotateDouble(self: *Self) void {
    self.keys_pressed += 1;
    if (!self.state.rotate(.Double)) {
        return;
    }

    self.just_rotated = true;
    if (self.move_count < AUTOLOCK_GRACE) {
        self.move_count += 1;
        self.last_move_time = milliTimestamp();
    }
}

pub fn rotateCcw(self: *Self) void {
    self.keys_pressed += 1;
    if (!self.state.rotate(.CCw)) {
        return;
    }

    self.just_rotated = true;
    if (self.move_count < AUTOLOCK_GRACE) {
        self.move_count += 1;
        self.last_move_time = milliTimestamp();
    }
}

// TODO: Make drop speed configurable
pub fn softDrop(self: *Self) void {
    if (self.softdropping) {
        return;
    }

    self.softdropping = true;
    self.vel += SOFT_G;
}

pub fn hardDrop(self: *Self) void {
    const dropped = self.state.dropToGround();
    self.score += @intCast(dropped * 2);
    if (dropped > 0) {
        self.just_rotated = false;
    }

    self.place();
}

fn place(self: *Self) void {
    const clear_info = self.lockCurrent();
    self.updateStats(clear_info[0], clear_info[1], clear_info[2]);
    self.clearLines();

    if (clear_info[0].cleared > 0) {
        self.last_clear_info = clear_info[0];
        self.last_clear_time = milliTimestamp();
    }

    self.state.nextPiece();
    self.already_held = false;
    self.just_rotated = false;
    self.move_count = 0;
    self.last_move_time = milliTimestamp();
    self.softdropping = false;
    self.vel = 0.0;
}

fn lockCurrent(self: *Self) struct { ClearInfo, u64, u64 } {
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
                self.playfield_colors[y][x] = self.state.current.kind.color();
            }
        }
    }

    // Scoring values taken from Tetris.wiki
    // https://tetris.wiki/Scoring#Recent_guideline_compatible_games
    const info = self.state.lockCurrent(self.just_rotated);
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
    // TODO: Are B2B bonuses applied to combos?
    if (self.state.canonicalcombo()) |combo| {
        clear_score += 50 * combo;
    }
    // TODO: Calculate attack
    const attack = 0;

    return .{
        info,
        clear_score,
        attack,
    };
}

fn updateStats(self: *Self, info: ClearInfo, score: u64, attack: u64) void {
    self.score += score * self.level();
    self.cleared += info.cleared;
    self.placed += 1;
    self.lines_sent += attack;

    // TODO: Calculate finesse
    const keys_pressed = self.keys_pressed - self.last_keys_pressed;
    _ = keys_pressed; // autofix
    self.last_keys_pressed = self.keys_pressed;
}

fn clearLines(self: *Self) void {
    var clears: usize = 0;
    var i: usize = @max(0, self.state.pos.y);
    while (i + clears < self.playfield_colors.len) {
        self.playfield_colors[i] = self.playfield_colors[i + clears];
        if (!isRowFull(self.playfield_colors[i])) {
            i += 1;
            continue;
        }

        clears += 1;
        const is_garbage = for (self.playfield_colors[i]) |cell| {
            if (cell == garbage_color) {
                break true;
            }
        } else false;
        if (is_garbage) {
            self.garbage_cleared += 1;
        }
    }
    while (i < self.playfield_colors.len) : (i += 1) {
        self.playfield_colors[i] = [_]Color{empty_color} ** 10;
    }
}

fn isRowFull(row: [10]Color) bool {
    for (row) |cell| {
        if (cell == empty_color) {
            return false;
        }
    }
    return true;
}

pub fn hold(self: *Self) void {
    self.keys_pressed += 1;
    if (self.already_held) {
        return;
    }

    self.state.hold();
    self.already_held = true;
    self.just_rotated = false;
    self.move_count = 0;
    self.last_move_time = milliTimestamp();
}

pub fn tick(self: *Self) void {
    const now = milliTimestamp();
    const dt = @as(f32, @floatFromInt(now - self.last_tick_time)) / 1000.0;
    self.vel += G * dt;

    // Handle autolocking
    if (self.state.onGround()) {
        self.vel = 0.0;

        if (self.move_count > AUTOLOCK_GRACE or now - self.last_move_time > LOCK_DELAY) {
            self.place();
        }
    }

    // Handle gravity
    const dropped = self.state.drop(@intFromFloat(self.vel));
    self.vel -= @floatFromInt(dropped);
    if (self.softdropping) {
        self.score += dropped;
    }
    if (dropped > 0) {
        self.just_rotated = false;
        self.last_move_time = now;
    }

    self.last_tick_time = now;
    self.softdropping = false;
}

/// Returns the current level
pub fn level(self: Self) u64 {
    return (self.cleared / 10) + 1;
}

/// Returns the current Attack Per Line (APL)
pub fn apl(self: Self) f32 {
    if (self.lines_sent == 0) {
        return 0.0;
    }
    return @as(f32, @floatFromInt(self.lines_sent)) / @as(f32, @floatFromInt(self.cleared));
}

/// Returns the current Attack Per Piece (APP)
pub fn app(self: Self) f32 {
    if (self.lines_sent == 0) {
        return 0.0;
    }
    return @as(f32, @floatFromInt(self.lines_sent)) / @as(f32, @floatFromInt(self.placed));
}

/// Returns the current Attack Per Minute (APM)
pub fn apm(self: Self) f32 {
    const elasped = milliTimestamp() - self.start_time;
    return @as(f32, @floatFromInt(self.lines_sent)) / @as(f32, @floatFromInt(elasped)) * std.time.ms_per_min;
}

/// Returns the current Pieces Per Second (PPS)
pub fn pps(self: Self) f32 {
    const elasped = milliTimestamp() - self.start_time;
    return @as(f32, @floatFromInt(self.placed)) / @as(f32, @floatFromInt(elasped)) * std.time.ms_per_s;
}

/// Returns the current Keys Per Piece (KPP)
pub fn kpp(self: Self) f32 {
    if (self.placed == 0) {
        return 0.0;
    }
    return @as(f32, @floatFromInt(self.last_keys_pressed)) / @as(f32, @floatFromInt(self.placed));
}

/// Returns the current VS Score
pub fn vsScore(self: Self) f32 {
    const elasped = milliTimestamp() - self.start_time;
    return 100.0 * @as(f32, @floatFromInt(self.lines_sent + self.garbage_cleared)) / @as(f32, @floatFromInt(elasped)) * std.time.ms_per_s;
}

pub fn drawToScreen(self: Self) !void {
    try self.drawNameLines();
    self.drawHold();
    try self.drawScoreLevel();
    try self.drawClearInfo();
    self.drawMatrix();
    self.drawNext();
    // TODO: Draw stats
}

fn drawNameLines(self: Self) !void {
    try self.view.printAligned(.Center, 0, .White, .Black, "{s}", .{self.name});
    try self.view.printAligned(.Center, 1, .White, .Black, "LINES - {d}", .{self.cleared});
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
                view.writeText(@intCast(x2), @intCast(y2), color, color, "  ");
            } else {
                view.writeText(@intCast(x2), @intCast(y2), color, empty_color, "▒▒");
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
    hold_box.writeText(3, 0, .White, .Black, "HOLD");
    if (self.state.hold_kind) |hold_kind| {
        const hold_piece = Piece{
            .facing = .Up,
            .kind = hold_kind,
        };
        const y: i8 = if (hold_kind == .I) 4 else 3;
        drawPiece(hold_box, 1, y, hold_piece, true);
    }
}

fn drawScoreLevel(self: Self) !void {
    const LEFT = 0;
    const TOP = 8;
    const WIDTH = 10;
    const HEIGHT = 6;

    const score_level_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
    score_level_box.drawBox(0, 0, WIDTH, HEIGHT);
    score_level_box.writeText(1, 1, .White, .Black, "SCORE");
    try printGlitchyU64(score_level_box, 1, 2, self.score);
    score_level_box.writeText(1, 3, .White, .Black, "LEVEL");
    try printGlitchyU64(score_level_box, 1, 4, self.level());
}

fn drawClearInfo(self: Self) !void {
    const LEFT = 0;
    const TOP = 15;
    const WIDTH = 10;
    const HEIGHT = 5;
    if (milliTimestamp() - self.last_clear_time >= CLEAR_ERASE_DELAY) {
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
    if (self.state.canonicalcombo()) |combo| {
        if (self.last_clear_info.cleared > 0 and combo > 0) {
            try clear_info_box.printAligned(.Center, 3, .White, .Black, "{d} COMBO!", .{combo});
        }
    }
    if (self.last_clear_info.pc) {
        clear_info_box.writeAligned(.Center, 4, .White, .Black, "ALL CLEAR!");
    }
}

fn printGlitchyU64(view: View, x: u8, y: u8, value: u64) !void {
    if (value < 100_000_000) {
        try view.printAt(x, y, .White, .Black, "{d}", .{value});
    } else if (value < 0x1_0000_0000) {
        // Print in hexadecimal if the score is too large
        try view.printAt(x, y, .White, .Black, "{x}", .{value});
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
        try view.printAt(x, y, .White, .Black, "{s}", .{std.unicode.fmtUtf16le(bytes[start..8])});
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
            const color = self.playfield_colors[y][x];
            matrix_box_inner.writeText(@intCast(x * 2), @intCast(19 - y), color, color, "  ");
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

fn drawNext(self: Self) void {
    const MAX_NEXT_DISPLAYABLE = 7;
    const LEFT = 34;
    const TOP = 2;
    const WIDTH = 10;

    const n_display_next: u16 = @min(MAX_NEXT_DISPLAYABLE, self.state.next_pieces.len);
    const next_box = self.view.sub(LEFT, TOP, WIDTH, n_display_next * 3 + 1);
    next_box.drawBox(0, 0, WIDTH, next_box.height);
    next_box.writeText(3, 0, .White, .Black, "NEXT");

    for (0..n_display_next) |i| {
        const piece = Piece{
            .facing = .Up,
            .kind = self.state.next_pieces[i],
        };
        const y: i8 = if (piece.kind == .I) 4 else 3;
        drawPiece(next_box, 1, y + @as(i8, @intCast(i * 3)), piece, true);
    }
}
