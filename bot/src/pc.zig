const std = @import("std");
const time = std.time;
const Allocator = std.mem.Allocator;
const expect = std.testing.expect;

const engine = @import("engine");
const kicks = engine.kicks;
const BoardMask = engine.bit_masks.BoardMask;
const GameState = engine.GameState(SevenBag, kicks.srsPlus);
const Position = engine.pieces.Position;
const Piece = engine.pieces.Piece;
const PieceKind = engine.pieces.PieceKind;
const SevenBag = engine.bags.SevenBag;

const root = @import("root.zig");
const PiecePosSet = root.PiecePosSet(.{ 10, 24, 4 });
pub const PiecePosition = root.PiecePosition;

const NodeSet = std.AutoHashMap(SearchNode, void);
// By drawing a snaking path through the playfield, the highest density of
// pushed unexplored nodes (around 2 / 3) is achieved. Thus, the highest stack
// length is given by: 10 * 24 * 4 * (2 / 3) = 640.
const PlacementStack = std.BoundedArray(PiecePosition, 640);

pub const Placement = struct {
    piece: Piece,
    pos: Position,
};

const Move = enum {
    Left,
    Right,
    RotateCw,
    RotateDouble,
    RotateCcw,
    Drop,

    const moves = [_]Move{
        .Left,
        .Right,
        .RotateCw,
        .RotateDouble,
        .RotateCcw,
        .Drop,
    };
};

const SearchNode = struct {
    // TODO: compress the boardmask
    board: BoardMask,
    current: PieceKind,
    depth: u8,
};

const VISUALISE = false;

pub const FindPcError = error{
    NoPcExists,
    NotEnoughPieces,
};

/// Finds a perfect clear with the least number of pieces possible for the given
/// game state, and returns the sequence of placements required to achieve it.
pub fn findPc(allocator: Allocator, game: GameState, min_height: u8, comptime max_pieces: usize) ![]Placement {
    const field_height = blk: {
        var i: usize = BoardMask.HEIGHT;
        while (i >= 1) : (i -= 1) {
            if (game.playfield.rows[i - 1] != BoardMask.EMPTY_ROW) {
                break;
            }
        }
        break :blk i;
    };
    const bits_set = blk: {
        var set: usize = 0;
        for (0..field_height) |i| {
            set += @popCount(game.playfield.rows[i] & ~BoardMask.EMPTY_ROW);
        }
        break :blk set;
    };
    const empty_cells = BoardMask.WIDTH * field_height - bits_set;

    // Assumes that all pieces have 4 cells and that the playfield is 10 cells wide.
    // Thus, an odd number of empty cells means that a perfect clear is impossible.
    if (empty_cells % 2 == 1) {
        return FindPcError.NoPcExists;
    }

    var pieces_needed = if (empty_cells % 4 == 2)
        (empty_cells + 10) / 4
    else
        empty_cells / 4;
    if (pieces_needed == 0) {
        pieces_needed = 5;
    }

    // 20 is the lowest common multiple of the width of the playfield (10) and the
    // number of cells in a piece (4). 20 / 4 = 5 extra pieces for each bigger
    // perfect clear
    var cache = NodeSet.init(allocator);
    defer cache.deinit();

    var pieces = getPieces(game, max_pieces);
    while (pieces_needed <= pieces.len) : (pieces_needed += 5) {
        const max_height = (4 * pieces_needed + bits_set) / BoardMask.WIDTH;
        if (max_height < min_height) {
            continue;
        }

        const placements = try allocator.alloc(Placement, pieces_needed);
        errdefer allocator.free(placements);

        cache.clearRetainingCapacity();
        if (try findPcInner(game, &pieces, placements, &cache, @intCast(max_height))) {
            return placements;
        }

        allocator.free(placements);
    }

    return FindPcError.NotEnoughPieces;
}

fn getPieces(game: GameState, comptime pieces_count: usize) [pieces_count]PieceKind {
    if (pieces_count == 0) {
        return .{};
    }
    if (pieces_count == 1) {
        return .{game.current.kind};
    }

    var pieces = [_]PieceKind{undefined} ** pieces_count;
    pieces[0] = game.current.kind;
    const start: usize = if (game.hold_kind) |hold| blk: {
        pieces[1] = hold;
        break :blk 2;
    } else 1;

    for (game.next_pieces, start..) |piece, i| {
        if (i >= pieces.len) {
            break;
        }
        pieces[i] = piece;
    }

    var bag_copy = game.bag;
    for (start + game.next_pieces.len..pieces.len) |i| {
        pieces[i] = bag_copy.next();
    }

    return pieces;
}

fn findPcInner(
    game: GameState,
    pieces: []PieceKind,
    placements: []Placement,
    cache: *NodeSet,
    max_height: u8,
) !bool {
    // Base case; check for perfect clear
    if (placements.len == 0) {
        return max_height == 0;
    }

    const node = SearchNode{
        .board = game.playfield,
        .current = game.current.kind,
        .depth = @intCast(placements.len),
    };
    // TODO: ~97% cache hit rate, consider optimising the cache
    if ((try cache.getOrPut(node)).found_existing) {
        return false;
    }

    if (VISUALISE) {
        std.debug.print("\x1B[1;1H{}", .{game});
    }

    for (0..2) |_| {
        var seen = PiecePosSet.init();
        var stack = PlacementStack.init(0) catch unreachable;

        // Start at lowest possible position
        {
            const piece = Piece{ .facing = .Up, .kind = pieces[0] };
            const pos = Position{
                .x = 0,
                .y = @as(i8, @intCast(max_height)) + piece.minY(),
            };
            stack.append(PiecePosition.pack(piece, pos)) catch unreachable;
        }

        while (stack.popOrNull()) |placement| {
            const piece = Piece{
                .facing = placement.facing,
                .kind = pieces[0],
            };
            const pos = Position{
                .x = placement.getX(),
                .y = placement.y,
            };
            if (seen.putGet(piece, pos)) {
                continue;
            }

            // TODO: optimise move generation
            // TODO: optimise move ordering
            for (Move.moves) |move| {
                var new_game = game;
                new_game.current = piece;
                new_game.pos = pos;

                switch (move) {
                    .Left => if (new_game.slide(-1) == 0) {
                        continue;
                    },
                    .Right => if (new_game.slide(1) == 0) {
                        continue;
                    },
                    .RotateCw => if (new_game.rotate(.QuarterCw) == -1) {
                        continue;
                    },
                    .RotateDouble => if (new_game.rotate(.Half) == -1) {
                        continue;
                    },
                    .RotateCcw => if (new_game.rotate(.QuarterCCw) == -1) {
                        continue;
                    },
                    .Drop => if (new_game.drop(1) == 0) {
                        continue;
                    },
                }
                // Branch out after movement
                stack.append(PiecePosition.pack(new_game.current, new_game.pos)) catch unreachable;

                // Skip this placement if the piece is too high
                _ = new_game.dropToGround();
                if (new_game.pos.y + @as(i8, @intCast(new_game.current.top())) > max_height) {
                    continue;
                }

                const cleared = new_game.lockCurrent(-1).cleared;
                const new_height = max_height - cleared;
                if (!isPcPossible(new_game.playfield.rows[0..new_height])) {
                    continue;
                }

                if (try findPcInner(
                    new_game,
                    pieces[1..],
                    placements[1..],
                    cache,
                    new_height,
                )) {
                    placements[0] = .{
                        .piece = new_game.current,
                        .pos = new_game.pos,
                    };
                    return true;
                }
            }
        }

        // No new piece to hold
        if (pieces.len == 1 or pieces[0] == pieces[1]) {
            break;
        }

        // Hold piece
        const temp = pieces[0];
        pieces[0] = pieces[1];
        pieces[1] = temp;
    }

    return false;
}

// TODO: Check against dictionary of possible PCs if the remaining peice count
// is high (maybe around 6 to 7)
fn isPcPossible(rows: []const u16) bool {
    var walls = ~BoardMask.EMPTY_ROW;
    for (rows) |row| {
        walls &= row;
    }
    walls >>= 1; // Remove padding
    walls &= 0b0111111110; // Any walls at the edges can be skipped

    var end: u4 = 0;
    while (walls != 0) {
        const start: u4 = @intCast(@ctz(walls));
        walls &= walls - 1; // Clear lowest bit

        // Each "segment" separated by a wall must have a multiple of 4 empty cells,
        // as pieces can only be placed in one segment.
        var empty_count: u16 = 0;
        for (rows) |row| {
            const segment = ~row << (15 - start) >> (end -% start);
            empty_count += @popCount(segment);
        }
        if (empty_count % 4 != 0) {
            return false;
        }
        end = start;
    }

    return true;
}

test "4-line PC" {
    const allocator = std.testing.allocator;

    var gamestate = GameState.init(engine.bags.SevenBag.init(0));

    const solution = try findPc(allocator, gamestate, 0, 11);
    defer allocator.free(solution);

    try expect(solution.len == 10);

    for (solution[0 .. solution.len - 1]) |placement| {
        gamestate.current = placement.piece;
        gamestate.pos = placement.pos;
        try expect(!gamestate.lockCurrent(-1).pc);
    }

    gamestate.current = solution[solution.len - 1].piece;
    gamestate.pos = solution[solution.len - 1].pos;
    try expect(gamestate.lockCurrent(-1).pc);
}
