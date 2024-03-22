const std = @import("std");
const Build = std.Build;
const builtin = std.builtin;

const zwin32 = @import("zwin32");
const zxaudio2 = @import("zxaudio2");

pub fn build(b: *Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    // Dependencies
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");
    const zwin32_pkg = zwin32.package(b, target, optimize, .{});
    const zxaudio2_pkg = zxaudio2.package(b, target, optimize, .{
        .deps = .{ .zwin32 = zwin32_pkg.zwin32 },
    });

    // Expose the library root
    _ = b.addModule("engine", .{
        .root_source_file = .{ .path = "src/root.zig" },
        .imports = &.{
            .{ .name = "nterm", .module = nterm_module },
            .{ .name = "zwin32", .module = zwin32_pkg.zwin32 },
            .{ .name = "zxaudio2", .module = zxaudio2_pkg.zxaudio2 },
        },
    });

    _ = buildExe(b, target, optimize, nterm_module, zwin32_pkg, zxaudio2_pkg);

    buildTests(b);
}

fn buildExe(
    b: *Build,
    target: Build.ResolvedTarget,
    optimize: builtin.OptimizeMode,
    nterm_module: *Build.Module,
    zwin32_pkg: zwin32.Package,
    zxaudio2_pkg: zxaudio2.Package,
) void {
    const exe = b.addExecutable(.{
        .name = "Budget Tetris",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });

    // Add dependencies
    exe.root_module.addImport("nterm", nterm_module);
    zwin32_pkg.link(exe, .{ .xaudio2 = true });
    zxaudio2_pkg.link(exe);

    if (exe.root_module.optimize == .ReleaseFast) {
        exe.root_module.strip = true;
    }

    // Install sound assets
    const install_sounds = b.addInstallDirectory(.{
        .source_dir = .{ .path = "./assets/sound" },
        .install_dir = .bin,
        .install_subdir = "sound",
    });
    exe.step.dependOn(&install_sounds.step);

    b.installArtifact(exe);

    // Add run step
    const run_cmd = b.addRunArtifact(exe);
    run_cmd.step.dependOn(b.getInstallStep());
    if (b.args) |args| {
        run_cmd.addArgs(args);
    }
    const run_step = b.step("run", "Run the app");
    run_step.dependOn(&run_cmd.step);
}

fn buildTests(b: *Build) void {
    const lib_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/root.zig" },
    });
    const run_lib_tests = b.addRunArtifact(lib_tests);

    const exe_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/main.zig" },
    });
    const run_exe_tests = b.addRunArtifact(exe_tests);

    const test_step = b.step("test", "Run library tests");
    test_step.dependOn(&run_lib_tests.step);
    test_step.dependOn(&run_exe_tests.step);
}
