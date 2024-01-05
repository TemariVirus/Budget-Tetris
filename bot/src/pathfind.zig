const std = @import("std");

const engine = @import("engine");
const BoardMask = engine.bit_masks.BoardMask;
const Facing = engine.pieces.Facing;
const GameState = engine.GameState;
const Piece = engine.pieces.Piece;
const Position = engine.pieces.Position;

const PiecePosSet = @import("root.zig").PiecePosSet(.{ 10, 24, 4 });

pub const Move = enum(u3) {
    Left = 0,
    AllLeft = 1,
    Right = 2,
    AllRight = 3,
    RotateCw = 4,
    RotateCCw = 5,
    RotateDouble = 6,
    Drop = 7,

    const moves = [_]Move{
        .Left,
        .AllLeft,
        .Right,
        .AllRight,
        .RotateCw,
        .RotateCCw,
        .RotateDouble,
        .Drop,
    };
};

pub const MAX_PATH_LEN = 64;
const MoveArray = struct {
    // Longest path I've found so far is 33 moves in [this map](https://jstris.jezevec10.com/map/52506)
    // It's possible to get 100s of moves with custom kick tables, but any SRS-based kick table should
    // be doable in <= 64 moves (24 bytes) for any map, probably?
    array: std.PackedIntArray(u3, MAX_PATH_LEN),

    fn init() MoveArray {
        return MoveArray{
            .array = std.PackedIntArray(u3, MAX_PATH_LEN).initAllTo(0),
        };
    }

    fn get(self: MoveArray, i: usize) Move {
        return @enumFromInt(self.array.get(i));
    }

    fn set(self: *MoveArray, i: usize, move: Move) void {
        self.array.set(i, @intFromEnum(move));
    }

    fn iter(self: MoveArray, len: usize) PathIterator {
        return PathIterator{
            .array = self.array,
            .index = 0,
            .len = len,
        };
    }
};

pub const PathIterator = struct {
    array: std.PackedIntArray(u3, MAX_PATH_LEN),
    index: usize,
    len: usize,

    pub fn next(self: PathIterator) ?Move {
        if (self.index >= self.len) {
            return null;
        }

        defer self.index += 1;
        return @enumFromInt(self.array.get(self.index));
    }
};

const SearchNode = struct {
    piece: Piece,
    pos: Position,
};

const NodeQueue = std.BoundedArray(struct { SearchNode, MoveArray }, 50);

fn validTarget(game: GameState, target_facing: Facing, target_pos: Position) bool {
    var valid_game = game;
    valid_game.current.facing = target_facing;
    valid_game.pos = target_pos;
    // The piece cannot move into solid cells and must end on the ground
    return !valid_game.collides(valid_game.current, target_pos) and valid_game.onGround();
}

/// Find the shortest path from the current piece at the current position to
/// the target position. If no path exists, returns `null`. The path always
/// ends with an implicit hard drop, which is not stored in the returned array.
pub fn shortestPath(game: GameState, target_facing: Facing, target_pos: Position) ?PathIterator {
    if (!validTarget(game, target_facing, target_pos)) {
        return null;
    }

    // Simple breadth-first search
    var seen = PiecePosSet.init();
    var queue = NodeQueue.init(0) catch unreachable;

    const start_node = SearchNode{
        .piece = game.current,
        .pos = game.pos,
    };
    seen.put(start_node.piece, start_node.pos);
    queue.append(.{ start_node, MoveArray.init() }) catch unreachable;

    for (0..MAX_PATH_LEN) |path_len| {
        var new_nodes = NodeQueue.init(0) catch unreachable;
        for (0..queue.len) |i| {
            const node = queue.get(i)[0];
            for (Move.moves) |move| {
                var new_game = game;
                new_game.current = node.piece;
                new_game.pos = node.pos;
                switch (move) {
                    .Left => _ = new_game.slide(-1),
                    .AllLeft => _ = new_game.slide(-10),
                    .Right => _ = new_game.slide(1),
                    .AllRight => _ = new_game.slide(10),
                    .RotateCw => _ = new_game.rotate(.QuarterCw),
                    .RotateCCw => _ = new_game.rotate(.QuarterCCw),
                    .RotateDouble => _ = new_game.rotate(.Half),
                    .Drop => _ = new_game.drop(1),
                }

                if (seen.putGet(new_game.current, new_game.pos)) {
                    continue;
                }

                var new_moves = queue.get(i)[1];
                new_moves.set(path_len, move);

                if (new_game.current.facing == target_facing) {
                    // Hard drop
                    _ = new_game.dropToGround();
                    if (std.meta.eql(new_game.pos, target_pos)) {
                        std.debug.print("{}\n", .{new_nodes.len});
                        return new_moves.iter(path_len + 1);
                    }
                }

                const new_node = SearchNode{
                    .piece = new_game.current,
                    .pos = new_game.pos,
                };
                new_nodes.append(.{ new_node, new_moves }) catch unreachable;
            }
        }

        queue = new_nodes;
        // All nodes exhausted, so there is no path
        if (queue.len == 0) {
            return null;
        }
    }

    @panic("Path too long");
}

// TODO: implement and compare performance with simple BFS
// TODO: try x dist only as heuristic
pub fn shortestPathAStar(game: GameState, target: Position) ?[]Move {
    if (!validTarget(game, target)) {
        return null;
    }

    @compileError("Not implemented");
}
