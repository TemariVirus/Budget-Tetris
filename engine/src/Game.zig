// TODO: Add sound
const std = @import("std");
const root = @import("main.zig");
const terminal = @import("terminal.zig");

const GameState = root.GameState;
const PieceKind = root.pieces.PieceKind;
const Piece = root.pieces.Piece;

const Color = terminal.Color;
const View = terminal.View;

const Self = @This();

pub const DISPLAY_W = 44;
pub const DISPLAY_H = 24;

const empty_color = Color.Black;
const garbage_color = Color.BrightBlack;

name: []const u8,
state: GameState,
playfield: [40][10]Color,
view: View,

already_held: bool,

pub fn init(name: []const u8, state: GameState, view: View) Self {
    return Self{
        .name = name,
        .state = state,
        .playfield = [_][10]Color{[_]Color{empty_color} ** 10} ** 40,
        .view = view,
        .already_held = false,
    };
}

pub fn moveLeft(self: *Self) void {
    _ = self.state.slide(-1);
}

pub fn moveLeftAll(self: *Self) void {
    _ = self.state.slide(-10);
}

pub fn moveRight(self: *Self) void {
    _ = self.state.slide(1);
}

pub fn moveRightAll(self: *Self) void {
    _ = self.state.slide(10);
}

// TODO: add velocity instead of dropping instantly
pub fn softDrop(self: *Self) void {
    _ = self.state.drop(40);
}

pub fn rotateCw(self: *Self) void {
    _ = self.state.rotate(.Cw);
}

pub fn rotateDouble(self: *Self) void {
    _ = self.state.rotate(.Double);
}

pub fn rotateCcw(self: *Self) void {
    _ = self.state.rotate(.CCw);
}

pub fn hardDrop(self: *Self) void {
    _ = self.state.dropToGround();

    self.lockCurrent();
    clearLines(self);

    self.state.nextPiece();
    self.already_held = false;
}

fn lockCurrent(self: *Self) void {
    _ = self.state.lock(false);

    const start: u8 = @max(0, self.state.pos.y);
    for (start..@intCast(self.state.pos.y + 4)) |y| {
        var row = self.state.current.mask().rows[@intCast(@as(isize, @intCast(y)) - self.state.pos.y)];
        row = if (self.state.pos.x > 0)
            row >> @intCast(self.state.pos.x)
        else
            row << @intCast(-self.state.pos.x);

        for (0..10) |x| {
            if ((row >> @intCast(10 - x)) & 1 == 1) {
                self.playfield[y][x] = self.state.current.kind.color();
            }
        }
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

fn clearLines(self: *Self) void {
    var clears: usize = 0;
    var i: usize = 0;
    while (i + clears < self.playfield.len) {
        self.playfield[i] = self.playfield[i + clears];
        if (isRowFull(self.playfield[i])) {
            clears += 1;
        } else {
            i += 1;
        }
    }
    while (i < self.playfield.len) : (i += 1) {
        self.playfield[i] = [_]Color{empty_color} ** 10;
    }
}

pub fn hold(self: *Self) void {
    if (self.already_held) {
        return;
    }

    self.state.hold();
    self.already_held = true;
}

pub fn drawToScreen(self: *Self) void {
    // Subtract 2 so that the centering is biased to the left
    const name_center_x = (self.view.width - self.name.len - 2) / 2;
    self.view.drawText(@intCast(name_center_x), 0, .White, .Black, self.name);

    self.drawHold();
    self.drawScoreLevel();
    self.drawMatrix();
    self.drawNext();
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
                view.drawText(@intCast(x2), @intCast(y2), color, color, "  ");
            } else {
                view.drawText(@intCast(x2), @intCast(y2), color, empty_color, "▒▒");
            }
        }
    }
}

fn drawHold(self: *Self) void {
    const LEFT = 0;
    const TOP = 2;
    const WIDTH = 10;
    const HEIGHT = 5;

    const hold_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
    hold_box.drawBox(0, 0, WIDTH, HEIGHT);
    hold_box.drawText(3, 0, .White, .Black, "HOLD");
    if (self.state.hold_kind) |hold_kind| {
        const hold_piece = Piece{
            .facing = .Up,
            .kind = hold_kind,
        };
        const y: i8 = if (hold_kind == .I) 4 else 3;
        drawPiece(hold_box, 1, y, hold_piece, true);
    }
}

fn drawScoreLevel(self: *Self) void {
    const LEFT = 0;
    const TOP = 8;
    const WIDTH = 10;
    const HEIGHT = 6;

    const score_level_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
    score_level_box.drawBox(0, 0, WIDTH, HEIGHT);
    score_level_box.drawText(1, 1, .White, .Black, "SCORE");
    score_level_box.drawText(1, 3, .White, .Black, "LEVEL");
}

fn drawMatrix(self: *Self) void {
    const LEFT = 11;
    const TOP = 2;
    const WIDTH = 22;
    const HEIGHT = 22;

    const matrix_box = self.view.sub(LEFT, TOP, WIDTH, HEIGHT);
    const matrix_box_inner = matrix_box.sub(1, 1, WIDTH - 2, HEIGHT - 2);
    matrix_box.drawBox(0, 0, WIDTH, HEIGHT);
    for (0..20) |y| {
        for (0..10) |x| {
            const color = self.playfield[y][x];
            matrix_box_inner.drawText(@intCast(x * 2), @intCast(19 - y), color, color, "  ");
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

fn drawNext(self: *Self) void {
    const MAX_NEXT_DISPLAYABLE = 7;
    const LEFT = 34;
    const TOP = 2;
    const WIDTH = 10;

    const n_display_next: u16 = @min(MAX_NEXT_DISPLAYABLE, self.state.next_pieces.len);
    const next_box = self.view.sub(LEFT, TOP, WIDTH, n_display_next * 3 + 1);
    next_box.drawBox(0, 0, WIDTH, next_box.height);
    next_box.drawText(3, 0, .White, .Black, "NEXT");

    for (0..n_display_next) |i| {
        const piece = Piece{
            .facing = .Up,
            .kind = self.state.next_pieces[i],
        };
        const y: i8 = if (piece.kind == .I) 4 else 3;
        drawPiece(next_box, 1, y + @as(i8, @intCast(i * 3)), piece, true);
    }
}
