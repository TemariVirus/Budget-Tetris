const std = @import("std");

pub const Bot = @import("neat/Bot.zig");
pub const NN = @import("neat/NN.zig");

test {
    std.testing.refAllDecls(@This());
}
