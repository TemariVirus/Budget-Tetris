const std = @import("std");
const json = std.json;
const Allocator = std.mem.Allocator;

const root = @import("root.zig");
const NodeSet = std.AutoHashMap(SearchNode, void);
const PiecePosSet = root.PiecePosSet(.{ 10, 24, 4 });
pub const PiecePosition = root.PiecePosition;
const PlacementStack = std.BoundedArray(PiecePosition, 640);

const engine = @import("engine");
const tbp = engine.tbp;

const BoardMask = engine.bit_masks.BoardMask;
const GameState = engine.GameState(engine.bags.SevenBag, engine.kicks.srs);
const PieceKind = engine.pieces.PieceKind;
const Piece = engine.pieces.Piece;
const Position = engine.pieces.Position;

const Move = enum {
    left,
    right,
    rotate_cw,
    rotate_ccw,
    drop,

    const moves = [_]Move{
        .left,
        .right,
        .rotate_cw,
        .rotate_ccw,
        .drop,
    };
};

const SearchNode = struct {
    board: BoardMask,
    current: PieceKind,
    depth: u8,
};

pub fn main() !void {
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    const allocator = gpa.allocator();
    defer _ = gpa.deinit();

    const stdin = std.io.getStdIn().reader();
    const stdout = std.io.getStdOut().writer();

    // Send info message
    const info = tbp.BotInfo{
        .name = "Budget",
        .version = "0.0.0",
        .author = "Zemogus",
        .features = &.{},
    };
    try info.post(stdout);

    // Read rules
    const rules_str = try waitForMessage(allocator, stdin, .rules);
    const rules = try tbp.GameRules.parse(allocator, rules_str);
    allocator.free(rules_str);
    defer rules.deinit();

    // Send ready or error
    try tbp.BotReady.post(stdout);

    // Main bot loop
    outer: while (true) {
        const start_str = try waitForMessage(allocator, stdin, .start);
        const start = try tbp.GameStart.parse(allocator, start_str);
        allocator.free(start_str);
        defer start.deinit();

        // Main game loop
        const next_len = start.value.queue.len;
        var game = start.value.toGamestate();
        while (true) {
            const s = try stdin.readUntilDelimiterAlloc(allocator, '\n', 65536);
            std.debug.print("{s}\n\n", .{s});
            defer allocator.free(s);

            const message_type = tbp.messageType(allocator, s) orelse continue;
            switch (message_type) {
                .suggest => {
                    const piece_pos = try nextMove(allocator, game, 2);
                    const move = tbp.BotMove.fromEngine(.{
                        .facing = piece_pos.facing,
                        .kind = game.current.kind,
                    }, .{
                        .x = piece_pos.getX(),
                        .y = piece_pos.y,
                    }, .none);
                    const suggestion = tbp.BotSuggestion{
                        .moves = &[1]tbp.BotMove{move},
                        .move_info = null,
                    };
                    try suggestion.post(stdout);
                },
                .play => {
                    const play = try tbp.GamePlay.parse(allocator, s);
                    defer play.deinit();

                    const move = play.value.move.toEngine();
                    if (game.current.kind != move.piece.kind) {
                        game.hold();
                    }

                    game.current = move.piece;
                    game.pos = move.pos;
                    _ = game.lockCurrent(-1);
                    game.nextPiece();
                },
                .new_piece => {
                    const new_piece = try tbp.GameNewPiece.parse(allocator, s);
                    defer new_piece.deinit();

                    game.next_pieces[next_len - 1] = new_piece.value.piece;
                    std.debug.print("{any}\n", .{game.next_pieces});
                },
                .stop => break,
                .quit => break :outer,
                else => continue,
            }
        }
    }
}

fn waitForMessage(allocator: Allocator, stdin: anytype, message_type: tbp.MessageType) ![]u8 {
    while (true) {
        const s = try stdin.readUntilDelimiterAlloc(allocator, '\n', 65536);
        std.debug.print("{s}\n\n", .{s});
        if (tbp.messageType(allocator, s) == message_type) {
            return s;
        }
        allocator.free(s);
    }
}

fn nextMove(allocator: Allocator, game: GameState, depth: u8) !PiecePosition {
    var best_score: u64 = 0;
    var best_move: PiecePosition = undefined;

    var seen = PiecePosSet.init();
    var stack = PlacementStack.init(0) catch unreachable;
    var cache = NodeSet.init(allocator);
    defer cache.deinit();

    // Starting position
    {
        const piece = Piece{ .facing = .up, .kind = game.current.kind };
        const pos = game.current.kind.startPos();
        stack.append(PiecePosition.pack(piece, pos)) catch unreachable;
    }

    while (stack.popOrNull()) |placement| {
        const piece = Piece{
            .facing = placement.facing,
            .kind = game.current.kind,
        };
        const pos = Position{
            .x = placement.getX(),
            .y = placement.y,
        };
        if (seen.putGet(piece, pos)) {
            continue;
        }

        for (Move.moves) |move| {
            var new_game = game;
            new_game.current = piece;
            new_game.pos = pos;

            switch (move) {
                .left => if (new_game.slide(-1) == 0) {
                    continue;
                },
                .right => if (new_game.slide(1) == 0) {
                    continue;
                },
                .rotate_cw => if (new_game.rotate(.quarter_cw) == -1) {
                    continue;
                },
                .rotate_ccw => if (new_game.rotate(.quarter_ccw) == -1) {
                    continue;
                },
                .drop => if (new_game.drop(1) == 0) {
                    continue;
                },
            }
            // Branch out after movement
            stack.append(PiecePosition.pack(new_game.current, new_game.pos)) catch unreachable;

            // Hard drop
            _ = new_game.dropToGround();
            const piece_pos = PiecePosition.pack(new_game.current, new_game.pos);
            _ = new_game.lockCurrent(-1);
            new_game.nextPiece();

            const score = try nextMoveInner(new_game, &cache, depth - 1);
            if (score > best_score) {
                best_score = score;
                best_move = piece_pos;
            }
        }
    }

    std.debug.print("score: {} | move: {any}\n", .{ best_score, best_move });
    return best_move;
}

fn nextMoveInner(game: GameState, cache: *NodeSet, depth: u8) !u64 {
    if (depth == 0) {
        return eval(game.playfield);
    }

    const node = SearchNode{
        .board = game.playfield,
        .current = game.current.kind,
        .depth = depth,
    };
    if ((try cache.getOrPut(node)).found_existing) {
        return 0;
    }

    var seen = PiecePosSet.init();
    var stack = PlacementStack.init(0) catch unreachable;

    // Starting position
    {
        const piece = Piece{ .facing = .up, .kind = game.current.kind };
        const pos = game.current.kind.startPos();
        stack.append(PiecePosition.pack(piece, pos)) catch unreachable;
    }

    var best_score: u64 = 0;
    while (stack.popOrNull()) |placement| {
        const piece = Piece{
            .facing = placement.facing,
            .kind = game.current.kind,
        };
        const pos = Position{
            .x = placement.getX(),
            .y = placement.y,
        };
        if (seen.putGet(piece, pos)) {
            continue;
        }

        for (Move.moves) |move| {
            var new_game = game;
            new_game.current = piece;
            new_game.pos = pos;

            switch (move) {
                .left => if (new_game.slide(-1) == 0) {
                    continue;
                },
                .right => if (new_game.slide(1) == 0) {
                    continue;
                },
                .rotate_cw => if (new_game.rotate(.quarter_cw) == -1) {
                    continue;
                },
                .rotate_ccw => if (new_game.rotate(.quarter_ccw) == -1) {
                    continue;
                },
                .drop => if (new_game.drop(1) == 0) {
                    continue;
                },
            }
            // Branch out after movement
            stack.append(PiecePosition.pack(new_game.current, new_game.pos)) catch unreachable;

            // Hard drop
            _ = new_game.dropToGround();
            _ = PiecePosition.pack(new_game.current, new_game.pos);
            _ = new_game.lockCurrent(-1);
            new_game.nextPiece();

            const score = try nextMoveInner(new_game, cache, depth - 1);
            if (score > best_score) {
                best_score = score;
            }
        }
    }

    return best_score;
}

fn eval(board: BoardMask) u64 {
    var heights = [_]u8{undefined} ** BoardMask.WIDTH;
    for (0..BoardMask.WIDTH) |x| {
        heights[x] = BoardMask.HEIGHT;
        while (heights[x] > 0) : (heights[x] -= 1) {
            if (board.rows[heights[x] - 1] != BoardMask.EMPTY_ROW) {
                break;
            }
        }
    }

    var max_height = heights[0];
    for (1..BoardMask.WIDTH) |x| {
        max_height = @max(max_height, heights[x]);
    }

    var bumpiness: u64 = 0;
    for (1..BoardMask.WIDTH) |x| {
        const b = if (heights[x] > heights[x - 1])
            heights[x] - heights[x - 1]
        else
            heights[x - 1] - heights[x];
        if (b > 1) {
            bumpiness += b;
        }
    }

    const inv_score: u64 = 4 * bumpiness + max_height;
    return 0 -% inv_score;
}
