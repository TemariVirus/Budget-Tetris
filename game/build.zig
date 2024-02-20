const std = @import("std");
const zwin32 = @import("zwin32");
const zxaudio2 = @import("zxaudio2");

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    const exe = b.addExecutable(.{
        .name = "Budget Tetris",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });

    // Add nterm dependency
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");
    exe.root_module.addImport("nterm", nterm_module);

    // Add zwin32 dependency
    const zwin32_pkg = zwin32.package(b, target, optimize, .{});
    zwin32_pkg.link(exe, .{ .xaudio2 = true });

    // Add zxaudio2 dependency
    const zxaudio2_pkg = zxaudio2.package(b, target, optimize, .{
        .deps = .{ .zwin32 = zwin32_pkg.zwin32 },
    });
    zxaudio2_pkg.link(exe);

    // Expose the library root
    _ = b.addModule("engine", .{
        .root_source_file = .{ .path = "src/root.zig" },
        .imports = &.{
            .{ .name = "nterm", .module = nterm_module },
        },
    });

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

    // Add test step
    const lib_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/root.zig" },
        .target = target,
        .optimize = optimize,
    });
    const run_lib_tests = b.addRunArtifact(lib_tests);

    const exe_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });
    const run_exe_tests = b.addRunArtifact(exe_tests);

    const test_step = b.step("test", "Run library tests");
    test_step.dependOn(&run_lib_tests.step);
    test_step.dependOn(&run_exe_tests.step);
}
