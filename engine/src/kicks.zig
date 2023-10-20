//! Functions encoding various kick tables.
//! The (0, 0) kick is always implied as the first kick, and thus is not returned.

// The srs180 and srsPlus kick tables were taken directly from Tetr.io's source code (https://tetr.io/js/tetrio.js)

const std = @import("std");
const testing = std.testing;
const mem = std.mem;
const toUpper = std.ascii.toUpper;

const root = @import("main.zig");
const Position = root.pieces.Position;
const Piece = root.pieces.Piece;

pub const none = @import("kicks/none.zig").none;
pub const srs = @import("kicks/srs.zig").srs;
pub const srs180 = @import("kicks/srs180.zig").srs180;
pub const srsPlus = @import("kicks/srs_plus.zig").srsPlus;

pub const Rotation = enum(u8) {
    /// 90° clockwise rotation
    Cw,
    /// 180° rotation
    Double,
    /// 90° anti-clockwise rotation
    ACw,
};

pub const KickTableParseError = error{TableNotFound};

pub const KickTable = enum {
    None,
    Srs,
    Srs180,
    SrsPlus,

    pub fn name(self: KickTable) []const u8 {
        switch (self) {
            .None => return "No kicks",
            .Srs => return "SRS (guideline)",
            .Srs180 => return "SRS (Tetr.io)",
            .SrsPlus => return "SRS+",
        }
    }

    pub fn parse(str: []const u8) KickTableParseError!KickTable {
        inline for (@typeInfo(KickTable).Enum.fields) |f| {
            if (strEqlCaseInsensitive(str, f.name)) {
                return @enumFromInt(f.value);
            }
        }
        return KickTableParseError.TableNotFound;
    }

    pub fn table(self: KickTable) fn (Piece, Rotation) []const Position {
        return switch (self) {
            .None => none,
            .Srs => srs,
            .Srs180 => srs180,
            .SrsPlus => srsPlus,
        };
    }
};

fn strEqlCaseInsensitive(a: []const u8, b: []const u8) bool {
    if (a.len != b.len) {
        return false;
    }
    for (a, 0..) |c, i| {
        if (toUpper(c) != toUpper(b[i])) {
            return false;
        }
    }
    return true;
}

test {
    testing.refAllDecls(@This());
}
