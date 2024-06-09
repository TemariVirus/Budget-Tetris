const std = @import("std");

pub const Libs = struct {
    xaudio2: bool = false,
};

pub const Package = struct {
    zwin32: *std.Build.Module,
    install_xaudio2: *std.Build.Step,

    pub fn link(pkg: Package, exe: *std.Build.Step.Compile, libs: Libs) void {
        exe.root_module.addImport("zwin32", pkg.zwin32);
        if (libs.xaudio2) exe.step.dependOn(pkg.install_xaudio2);
    }
};

pub fn package(
    b: *std.Build,
    _: std.Build.ResolvedTarget,
    _: std.builtin.Mode,
    _: struct {},
) Package {
    const install_xaudio2 = b.allocator.create(std.Build.Step) catch @panic("OOM");
    install_xaudio2.* = std.Build.Step.init(.{ .id = .custom, .name = "zwin32-install-xaudio2", .owner = b });

    install_xaudio2.dependOn(
        &b.addInstallFile(
            lazyPath(b, thisDir() ++ "/bin/x64/xaudio2_9redist.dll"),
            "bin/xaudio2_9redist.dll",
        ).step,
    );

    return .{
        .zwin32 = b.addModule("zwin32", .{
            .root_source_file = lazyPath(b, thisDir() ++ "/src/zwin32.zig"),
        }),
        .install_xaudio2 = install_xaudio2,
    };
}

pub fn build(b: *std.Build) void {
    const optimize = b.standardOptimizeOption(.{});
    const target = b.standardTargetOptions(.{});
    _ = package(b, target, optimize, .{});
}

inline fn thisDir() []const u8 {
    return comptime std.fs.path.dirname(@src().file) orelse ".";
}

fn lazyPath(b: *std.Build, path: []const u8) std.Build.LazyPath {
    return .{
        .src_path = .{
            .owner = b,
            .sub_path = path,
        },
    };
}
