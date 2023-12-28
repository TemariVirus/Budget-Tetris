const std = @import("std");

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    const lib = b.addStaticLibrary(.{
        .name = "Budget Tetris Engine",
        .root_source_file = .{ .path = "src/root.zig" },
        .target = target,
        .optimize = optimize,
    });

    // Add nterm dependency
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");
    lib.addModule("nterm", nterm_module);

    // Expose the library root
    _ = b.addModule("engine", .{
        .source_file = .{ .path = "src/root.zig" },
        .dependencies = &.{
            .{ .name = "nterm", .module = nterm_module },
        },
    });

    b.installArtifact(lib);

    // Add test step
    const lib_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/root.zig" },
        .target = target,
        .optimize = optimize,
    });
    const run_lib_tests = b.addRunArtifact(lib_tests);
    const test_step = b.step("test", "Run library tests");
    test_step.dependOn(&run_lib_tests.step);
}
