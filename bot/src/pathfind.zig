const std = @import("std");
const assert = std.debug.assert;

const engine = @import("engine");
const BoardMask = engine.bit_masks.BoardMask;
const GameState = engine.GameState;
const Piece = engine.pieces.Piece;
const PieceKind = engine.pieces.PieceKind;
const Position = engine.pieces.Position;

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

pub const MoveArray = struct {
    // Longest path I've found so far is 33 moves in [this map](https://jstris.jezevec10.com/map/52506)
    // It's possible to get 100s of moves with custom kick tables, but any SRS-based kick table should
    // be doable in <= 64 moves (24 bytes) for any map, probably?
    array: std.PackedIntArray(u3, 64),

    pub fn init() MoveArray {
        return MoveArray{
            .array = std.PackedIntArray(u3, 64).initAllTo(0),
        };
    }

    pub fn get(self: MoveArray, i: usize) Move {
        return @enumFromInt(self.array.get(i));
    }

    pub fn set(self: *MoveArray, i: usize, val: Move) void {
        self.array.set(i, @intFromEnum(val));
    }
};

const SearchNode = struct {
    piece: Piece,
    pos: Position,
};

const NodeSet = struct {
    data: [4]BoardMask,
    piece: PieceKind,

    fn init(piece: PieceKind) NodeSet {
        return NodeSet{
            .data = undefined,
            .piece = piece,
        };
    }

    fn contains(self: NodeSet, node: SearchNode) bool {
        assert(self.piece == node.piece.kind);

        const facing = @intFromEnum(node.piece.facing);
        const x: usize = @intCast(node.pos.x - node.piece.minX());
        const y: usize = @intCast(node.pos.y - node.piece.minY());
        return self.data[facing].get(x, y);
    }

    fn put(self: *NodeSet, node: SearchNode) void {
        assert(self.piece == node.piece.kind);

        const facing = @intFromEnum(node.piece.facing);
        const x: usize = @intCast(node.pos.x - node.piece.minX());
        const y: usize = @intCast(node.pos.y - node.piece.minY());
        self.data[facing].set(x, y, true);
    }
};

const NodeQueue = std.BoundedArray(struct { SearchNode, MoveArray }, 64);

/// Find the shortest path from the current piece at the current position to
/// the target position. If no path exists, returns `null`.
pub fn shortestPath(game: GameState, target: Position) ?MoveArray {
    // Simple breadth-first search
    var seen = NodeSet.init(game.current.kind);
    var queue = NodeQueue.init(0) catch unreachable;

    const start_node = SearchNode{
        .piece = game.current,
        .pos = game.pos,
    };
    seen.put(start_node);
    queue.append(.{ start_node, MoveArray.init() }) catch unreachable;

    var path_len: u8 = 0;
    while (path_len < 64) : (path_len += 1) {
        var new_nodes = NodeQueue.init(0) catch unreachable;
        for (0..queue.len) |i| {
            const node = queue.get(i)[0];
            for (Move.moves) |move| {
                var new_game = game;
                new_game.current = node.piece;
                new_game.pos = node.pos;
                switch (move) {
                    .AllLeft => _ = new_game.slide(-10),
                    .Left => _ = new_game.slide(-1),
                    .AllRight => _ = new_game.slide(10),
                    .Right => _ = new_game.slide(1),
                    .RotateCw => _ = new_game.rotate(.QuarterCw),
                    .RotateCCw => _ = new_game.rotate(.QuarterCCw),
                    .RotateDouble => _ = new_game.rotate(.Half),
                    .Drop => _ = new_game.drop(1),
                }

                const new_node = SearchNode{
                    .piece = new_game.current,
                    .pos = new_game.pos,
                };
                if (seen.contains(new_node)) {
                    continue;
                }
                seen.put(new_node);

                var new_moves = queue.get(i)[1];
                new_moves.set(path_len, @intFromEnum(move));
                if (!std.meta.eql(new_game.pos, target)) {
                    new_nodes.append(.{ new_node, new_moves }) catch unreachable;
                    continue;
                }

                return new_moves;
            }
        }

        // All nodes exhausted, so there is no path
        queue = new_nodes;
        if (queue.len == 0) {
            return null;
        }
    }

    @panic("Path too long");
}

// TODO: implement and compare performance with simple BFS
pub fn shortestPathAStar(game: GameState) ?[]Move {
    // TODO: try x dist only as heuristic
    _ = game; // autofix
    @compileError("Not implemented");
}
