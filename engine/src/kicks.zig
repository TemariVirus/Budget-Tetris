//! Functions encoding various kick tables.
//! The (0, 0) kick is always implied as the first kick, and thus is not returned.

pub const Rotation = enum(u8) {
    Cw, // Clockwise
    Double,
    ACw, // Anti-clockwise
};

pub const none = @import("kicks/none.zig").none;
pub const srs = @import("kicks/srs.zig").srs;
pub const srs180 = @import("kicks/srs180.zig").srs180;
pub const srsPlus = @import("kicks/srs_plus.zig").srsPlus;
