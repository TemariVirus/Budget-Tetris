const std = @import("std");

pub const Color = enum(u8) {
    Black = 30,
    Red = 31,
    Green = 32,
    Yellow = 33,
    Blue = 34,
    Magenta = 35,
    Cyan = 36,
    White = 37,
    BrightBlack = 90,
    BrightRed = 91,
    BrightGreen = 92,
    BrightYellow = 93,
    BrightBlue = 94,
    BrightMagenta = 95,
    BrightCyan = 96,
    BrightWhite = 97,
};

pub fn setFgColor(writer: anytype, color: Color) !void {
    try writer.print("\x1B[{}m", .{@intFromEnum(color)});
}

pub fn setBgColor(writer: anytype, color: Color) !void {
    try writer.print("\x1B[{}m", .{@intFromEnum(color) + 10});
}

pub fn resetColors(writer: anytype) !void {
    try writer.print("\x1B[m", .{});
}

pub fn colorPrint(
    writer: anytype,
    forground: Color,
    background: Color,
    comptime format: []const u8,
    args: anytype,
) !void {
    try writer.print("\x1B[{};{}m", .{ @intFromEnum(forground), @intFromEnum(background) + 10 });
    try writer.print(format, args);
    try writer.print("\x1B[m", .{});
}
