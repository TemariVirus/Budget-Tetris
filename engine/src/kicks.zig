const pieces = @import("pieces.zig");
const PieceKind = pieces.PieceKind;
const PiecePos = pieces.PiecePos;
const Rotation = pieces.Rotation;

/// Classic SRS kicks.
pub fn srs(kind: PieceKind, rotation: Rotation) []PiecePos {
    return switch (rotation) {
        .Right => switch (kind) {
            .IUp => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = -1 },
                PiecePos{ .x = 1, .y = 2 },
            },
            .OUp => &[0]PiecePos{},
            .TUp, .SUp, .ZUp, .JUp, .LUp => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = -1, .y = 1 },
                PiecePos{ .x = 0, .y = -2 },
                PiecePos{ .x = -1, .y = -2 },
            },

            .IRight => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = -1, .y = 2 },
                PiecePos{ .x = 2, .y = -1 },
            },
            .ORight => &[0]PiecePos{},
            .TRight, .SRight, .ZRight, .JRight, .LRight => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = 1, .y = -1 },
                PiecePos{ .x = 0, .y = 2 },
                PiecePos{ .x = 1, .y = 2 },
            },

            .IDown => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 1 },
                PiecePos{ .x = -1, .y = -2 },
            },
            .ODown => &[0]PiecePos{},
            .TDown, .SDown, .ZDown, .JDown, .LDown => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = 1, .y = 1 },
                PiecePos{ .x = 0, .y = -2 },
                PiecePos{ .x = 1, .y = -2 },
            },

            .ILeft => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = 1, .y = -2 },
                PiecePos{ .x = -2, .y = 1 },
            },
            .OLeft => &[0]PiecePos{},
            .TLeft, .SLeft, .ZLeft, .JLeft, .LLeft => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = -1, .y = -1 },
                PiecePos{ .x = 0, .y = 2 },
                PiecePos{ .x = -1, .y = 2 },
            },
        },
        .Left => switch (kind) {
            .IUp => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = -1, .y = 2 },
                PiecePos{ .x = 2, .y = -1 },
            },
            .OUp => &[0]PiecePos{},
            .TUp, .SUp, .ZUp, .JUp, .LUp => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = 1, .y = 1 },
                PiecePos{ .x = 0, .y = -2 },
                PiecePos{ .x = 1, .y = -2 },
            },

            .IRight => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 1 },
                PiecePos{ .x = -1, .y = -2 },
            },
            .ORight => &[0]PiecePos{},
            .TRight, .SRight, .ZRight, .JRight, .LRight => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = 1, .y = -1 },
                PiecePos{ .x = 0, .y = 2 },
                PiecePos{ .x = 1, .y = 2 },
            },

            .IDown => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = 1, .y = -2 },
                PiecePos{ .x = -2, .y = 1 },
            },
            .ODown => &[0]PiecePos{},
            .TDown, .SDown, .ZDown, .JDown, .LDown => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = -1, .y = 1 },
                PiecePos{ .x = 0, .y = -2 },
                PiecePos{ .x = -1, .y = -2 },
            },

            .ILeft => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = -1 },
                PiecePos{ .x = 1, .y = 2 },
            },
            .OLeft => &[0]PiecePos{},
            .TLeft, .SLeft, .ZLeft, .JLeft, .LLeft => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = -1, .y = -1 },
                PiecePos{ .x = 0, .y = 2 },
                PiecePos{ .x = -1, .y = 2 },
            },
        },
        .Down => &[0]PiecePos{},
    };
}

/// The modified SRS kicks that Tetr.io uses.
pub fn srs180(kind: PieceKind, rotation: Rotation) []PiecePos {
    return switch (rotation) {
        .Down => switch (kind) {
            .IUp, .IRight, .IDown, .ILeft, .OUp, .ORight, .ODown, .OLeft => &[0]PiecePos{},
            .TUp, .SUp, .ZUp, .JUp, .LUp => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 0, .y = 1 },
                PiecePos{ .x = 1, .y = 1 },
                PiecePos{ .x = -1, .y = 1 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
            },
            .TRight, .SRight, .ZRight, .JRight, .LRight => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = 1, .y = 2 },
                PiecePos{ .x = 1, .y = 1 },
                PiecePos{ .x = 0, .y = 2 },
                PiecePos{ .x = 0, .y = 1 },
            },
            .TDown, .SDown, .ZDown, .JDown, .LDown => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 0, .y = -1 },
                PiecePos{ .x = -1, .y = -1 },
                PiecePos{ .x = 1, .y = -1 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
            },
            .TLeft, .SLeft, .ZLeft, .JLeft, .LLeft => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = -1, .y = 2 },
                PiecePos{ .x = -1, .y = 1 },
                PiecePos{ .x = 0, .y = 2 },
                PiecePos{ .x = 0, .y = 1 },
            },
        },
        else => srs(kind, rotation),
    };
}

/// Tetr.io's SRS+ kicks.
pub fn srsPlus(kind: PieceKind, rotation: Rotation) []PiecePos {
    return switch (kind) {
        .IUp => switch (rotation) {
            .Right => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = -2, .y = -1 },
                PiecePos{ .x = 1, .y = 2 },
            },
            .Left => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = 2, .y = -1 },
                PiecePos{ .x = -1, .y = 2 },
            },
            .Down => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 0, .y = 1 },
            },
        },
        .IRight => switch (rotation) {
            .Right => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = -1, .y = 2 },
                PiecePos{ .x = 2, .y = -1 },
            },
            .Left => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = -1, .y = -2 },
                PiecePos{ .x = 2, .y = 1 },
            },
            .Down => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
            },
        },
        .IDown => switch (rotation) {
            .Right => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 2, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
                PiecePos{ .x = 2, .y = 1 },
                PiecePos{ .x = -1, .y = -2 },
            },
            .Left => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = 1 },
                PiecePos{ .x = 1, .y = -2 },
            },
            .Down => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 0, .y = -1 },
            },
        },
        .ILeft => switch (rotation) {
            .Right => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = 1, .y = -2 },
                PiecePos{ .x = -2, .y = 1 },
            },
            .Left => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = 1, .y = 0 },
                PiecePos{ .x = -2, .y = 0 },
                PiecePos{ .x = 1, .y = 2 },
                PiecePos{ .x = -2, .y = -1 },
            },
            .Down => &[_]PiecePos{
                PiecePos{ .x = 0, .y = 0 },
                PiecePos{ .x = -1, .y = 0 },
            },
        },
        else => srs180(kind, rotation),
    };
}
