const std = @import("std");

pub const Package = struct {
    zxaudio2: *std.Build.Module,

    pub fn link(pkg: Package, exe: *std.Build.Step.Compile) void {
        exe.root_module.addImport("zxaudio2", pkg.zxaudio2);
    }
};

pub fn package(
    b: *std.Build,
    _: std.Build.ResolvedTarget,
    _: std.builtin.Mode,
    args: struct {
        deps: struct { zwin32: *std.Build.Module },
    },
) Package {
    const zxaudio2 = b.addModule("zxaudio2", .{
        .root_source_file = .{ .path = thisDir() ++ "/src/zxaudio2.zig" },
        .imports = &.{
            .{ .name = "zwin32", .module = args.deps.zwin32 },
        },
    });

    return .{
        .zxaudio2 = zxaudio2,
    };
}

pub fn build(b: *std.Build) void {
    const optimize = b.standardOptimizeOption(.{});
    const target = b.standardTargetOptions(.{});

    const zwin32 = b.dependency("zwin32", .{});

    _ = package(b, target, optimize, .{
        .deps = .{
            .zwin32 = zwin32.module("zwin32"),
        },
    });
}

inline fn thisDir() []const u8 {
    return comptime std.fs.path.dirname(@src().file) orelse ".";
}
