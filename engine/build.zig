const std = @import("std");
const Build = std.Build;
const builtin = std.builtin;

const zwin32 = @import("zwin32");
const zxaudio2 = @import("zxaudio2");

pub const Package = struct {
    engine: *Build.Module,
    install_xaudio2: *Build.Step,

    pub fn link(pkg: Package, exe: *Build.Step.Compile) void {
        exe.root_module.addImport("engine", pkg.engine);
        exe.step.dependOn(pkg.install_xaudio2);
    }
};

pub fn build(b: *Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});
    _ = package(b, target, optimize);
    buildTests(b);
}

pub fn package(
    b: *Build,
    target: Build.ResolvedTarget,
    optimize: builtin.OptimizeMode,
) Package {
    // Dependencies
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");
    const zwin32_pkg = zwin32.package(b, target, optimize, .{});
    const zxaudio2_pkg = zxaudio2.package(b, target, optimize, .{
        .deps = .{ .zwin32 = zwin32_pkg.zwin32 },
    });

    return .{
        .engine = b.addModule("engine", .{
            .root_source_file = .{ .path = thisDir() ++ "/src/root.zig" },
            .imports = &.{
                .{ .name = "nterm", .module = nterm_module },
                .{ .name = "zwin32", .module = zwin32_pkg.zwin32 },
                .{ .name = "zxaudio2", .module = zxaudio2_pkg.zxaudio2 },
            },
        }),
        .install_xaudio2 = zwin32_pkg.install_xaudio2,
    };
}

fn buildTests(b: *Build) void {
    const lib_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/root.zig" },
    });
    const run_lib_tests = b.addRunArtifact(lib_tests);
    const test_step = b.step("test", "Run library tests");
    test_step.dependOn(&run_lib_tests.step);
}

inline fn thisDir() []const u8 {
    return comptime std.fs.path.dirname(@src().file) orelse ".";
}
