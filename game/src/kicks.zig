//! Functions encoding various kick tables.
//! The (0, 0) kick is always implied as the first kick, and thus is not returned.

// The srs180 and srsPlus kick tables were taken directly from Tetr.io's source code (https://tetr.io/js/tetrio.js)

const std = @import("std");
const testing = std.testing;
const toUpper = std.ascii.toUpper;

const root = @import("root.zig");
const Position = root.pieces.Position;
const Piece = root.pieces.Piece;

pub const KickFn = fn (Piece, Rotation) []const Position;

pub const none = @import("kicks/none.zig").none;
pub const srs = @import("kicks/srs.zig").srs;
pub const srs180 = @import("kicks/srs180.zig").srs180;
pub const srsPlus = @import("kicks/srs_plus.zig").srsPlus;

/// Represents a piece rotation.
pub const Rotation = enum {
    /// A 90 degree clockwise rotation.
    Cw,
    /// A 180 degree rotation.
    Double,
    /// A 90 degree counter-clockwise rotation.
    CCw,
};

test {
    testing.refAllDecls(@This());
}
