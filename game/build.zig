const std = @import("std");
const builtin = @import("builtin");

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const target_os = target.os_tag orelse builtin.os.tag;
    const optimize = b.standardOptimizeOption(.{});

    const lib = b.addStaticLibrary(.{
        .name = "Budget Tetris",
        .root_source_file = .{ .path = "src/root.zig" },
        .target = target,
        .optimize = optimize,
    });
    _ = lib; // autofix

    const exe = b.addExecutable(.{
        .name = "Budget Tetris",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });

    // LibC required on Windows for signal handling
    if (target_os == .windows) {
        exe.linkLibC();
    }

    // Add nterm dependency
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");
    // lib.addModule("nterm", nterm_module);
    exe.addModule("nterm", nterm_module);

    // Expose the library root
    _ = b.addModule("engine", .{
        .source_file = .{ .path = "src/root.zig" },
        .dependencies = &.{
            .{ .name = "nterm", .module = nterm_module },
        },
    });

    // b.installArtifact(lib);
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
