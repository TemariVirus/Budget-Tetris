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

    // Expose the library root
    _ = b.addModule("engine", .{
        .source_file = .{ .path = "src/root.zig" },
    });

    b.installArtifact(lib);

    // Add test step
    const main_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/root.zig" },
        .target = target,
        .optimize = optimize,
    });
    if (target_os == .windows) {
        // LibC required on Windows for signal handling
        main_tests.linkLibC();
    }
    const run_main_tests = b.addRunArtifact(main_tests);
    const test_step = b.step("test", "Run library tests");
    test_step.dependOn(&run_main_tests.step);
}
