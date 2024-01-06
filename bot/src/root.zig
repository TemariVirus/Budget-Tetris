pub const pathfind = @import("pathfind.zig");
pub const pc = @import("pc.zig");

const std = @import("std");
const assert = std.debug.assert;

const engine = @import("engine");
const BoardMask = engine.bit_masks.BoardMask;
const Facing = engine.pieces.Facing;
const Piece = engine.pieces.Piece;
const Position = engine.pieces.Position;

pub const PiecePosition = packed struct {
    const x_offset = 2; // Minimum x for position is -2

    y: i8,
    _x: u4,
    facing: Facing,

    pub fn pack(piece: Piece, pos: Position) PiecePosition {
        return PiecePosition{
            .y = pos.y,
            ._x = @as(u4, @intCast(pos.x + x_offset)),
            .facing = piece.facing,
        };
    }

    pub fn getX(self: PiecePosition) i8 {
        return @as(i8, self._x) - x_offset;
    }

    pub fn setX(self: *PiecePosition, x: i8) void {
        self._x = @as(u4, @intCast(x + x_offset));
    }
};

pub fn PiecePosSet(comptime shape: [3]usize) type {
    const len = shape[0] * shape[1] * shape[2];
    const BackingSet = std.StaticBitSet(len);

    return struct {
        const Self = @This();

        data: BackingSet,

        pub fn init() Self {
            return Self{
                .data = BackingSet.initEmpty(),
            };
        }

        pub fn flatIndex(piece: Piece, pos: Position) usize {
            const facing = @intFromEnum(piece.facing);
            const x: usize = @intCast(pos.x - piece.minX());
            const y: usize = @intCast(pos.y - piece.minY());

            assert(x < shape[0]);
            assert(y < shape[1]);
            assert(facing < shape[2]);

            return x + y * shape[0] + facing * shape[0] * shape[1];
        }

        pub fn contains(self: Self, piece: Piece, pos: Position) bool {
            const index = Self.flatIndex(piece, pos);
            return self.data.isSet(index);
        }

        pub fn put(self: *Self, piece: Piece, pos: Position) void {
            const index = Self.flatIndex(piece, pos);
            self.data.set(index);
        }

        pub fn putGet(self: *Self, piece: Piece, pos: Position) bool {
            const index = Self.flatIndex(piece, pos);

            const was_set = self.data.isSet(index);
            self.data.set(index);
            return was_set;
        }
    };
}

test {
    std.testing.refAllDecls(@This());
}
