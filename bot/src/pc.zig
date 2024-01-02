const std = @import("std");
const Allocator = std.mem.Allocator;

const engine = @import("engine");
const BoardMask = engine.bit_masks.BoardMask;
const GameState = engine.GameState;
const Position = engine.pieces.Position;
const Piece = engine.pieces.Piece;
const PieceKind = engine.pieces.PieceKind;

const NodeSet = std.AutoHashMap(SearchNode, void);
const PlacementSet = std.AutoHashMap(Placement, void);
const PlacementStack = std.ArrayList(Placement);

const Move = enum {
    Left,
    Right,
    RotateCw,
    RotateDouble,
    RotateCcw,
    Drop,
};

pub const Placement = struct {
    pos: Position,
    piece: Piece,
};

const SearchNode = struct {
    // Oversized boardmask doesn't seem to be a bottleneck??
    board: BoardMask,
    current: PieceKind,
    depth: u8,
};

const VISUALISE = true;

const moves = [_]Move{
    .Left,
    .Right,
    .RotateCw,
    .RotateDouble,
    .RotateCcw,
    .Drop,
};

pub const FindPcError = error{
    NoPcExists,
    NotEnoughPieces,
};

/// Finds a perfect clear with the least number of pieces possible for the given
/// game state, and returns the sequence of placements required to achieve it.
///
/// The layout of `pieces` is expected to be `{ current, hold, next[0], next[1], ... }`.
/// If there is no hold piece, the layout must be `{ current, next[0], next[1], ... }`.
pub fn findPc(allocator: Allocator, game: GameState, pieces: []PieceKind) ![]Placement {
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
    // TODO: locate memory leak
    while (pieces_needed <= pieces.len) : (pieces_needed += 5) {
        const max_height = (4 * pieces_needed + bits_set) / BoardMask.WIDTH;
        if (max_height > 4) {
            return FindPcError.NoPcExists;
        }

        const placements = try allocator.alloc(Placement, pieces_needed);
        errdefer allocator.free(placements);

        var cache = NodeSet.init(allocator);
        defer cache.deinit();

        const seens = try allocator.alloc(PlacementSet, pieces_needed);
        for (seens) |*seen| {
            seen.* = PlacementSet.init(allocator);
        }
        defer {
            for (seens) |*seen| {
                seen.deinit();
            }
            allocator.free(seens);
        }

        const stacks = try allocator.alloc(PlacementStack, pieces_needed);
        for (stacks) |*stack| {
            stack.* = PlacementStack.init(allocator);
        }
        defer {
            for (stacks) |*stack| {
                stack.deinit();
            }
            allocator.free(stacks);
        }

        if (try findPcInner(game, pieces, placements, &cache, seens, stacks, @intCast(max_height))) {
            return placements;
        }

        allocator.free(placements);
    }

    return FindPcError.NotEnoughPieces;
}

fn findPcInner(
    game: GameState,
    pieces: []PieceKind,
    placements: []Placement,
    cache: *NodeSet,
    seens: []const PlacementSet,
    stacks: []const PlacementStack,
    max_height: u8,
) !bool {
    // Base case; check for perfect clear
    if (placements.len == 0) {
        return game.playfield.rows[0] == BoardMask.EMPTY_ROW;
    }

    const node = SearchNode{
        .board = game.playfield,
        .current = game.current.kind,
        .depth = @intCast(placements.len),
    };
    if ((try cache.getOrPut(node)).found_existing) {
        return false;
    }

    if (VISUALISE) {
        // std.debug.print("\x1B[1;1H{}", .{game});
    }

    var seen = seens[0];
    var stack = stacks[0];
    for (0..2) |_| {
        seen.clearRetainingCapacity();
        stack.clearRetainingCapacity();

        // Start at lowest possible position
        const piece = pieces[0];
        const start_pos = Position{
            .x = 0,
            .y = @as(i8, @intCast(max_height)) - 1,
        };
        try stack.append(.{ .piece = .{ .facing = .Up, .kind = piece }, .pos = start_pos });

        while (stack.popOrNull()) |placement| {
            if ((try seen.getOrPut(placement)).found_existing) {
                continue;
            }

            // TODO: optimise move generation
            for (moves) |move| {
                var new_game = game;
                new_game.current = placement.piece;
                new_game.pos = placement.pos;

                switch (move) {
                    .Left => if (new_game.slide(-1) == 0) {
                        continue;
                    },
                    .Right => if (new_game.slide(1) == 0) {
                        continue;
                    },
                    .RotateCw => if (!new_game.rotate(.Cw)) {
                        continue;
                    },
                    .RotateDouble => if (!new_game.rotate(.Double)) {
                        continue;
                    },
                    .RotateCcw => if (!new_game.rotate(.CCw)) {
                        continue;
                    },
                    .Drop => _ = new_game.dropToGround(),
                }
                try stack.append(Placement{
                    .piece = new_game.current,
                    .pos = new_game.pos,
                });

                _ = new_game.dropToGround();
                if (new_game.pos.y + @as(i8, @intCast(new_game.current.top())) > max_height) {
                    continue;
                }

                const info = new_game.lockCurrent(false);
                if (try findPcInner(
                    new_game,
                    pieces[1..],
                    placements[1..],
                    cache,
                    seens[1..],
                    stacks[1..],
                    max_height - info.cleared,
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
