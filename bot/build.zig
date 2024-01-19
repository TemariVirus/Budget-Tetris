const std = @import("std");

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    const train_exe = b.addExecutable(.{
        .name = "Budget Tetris Bot Training",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });

    const bot_exe = b.addExecutable(.{
        .name = "Budget Tetris Bot",
        .root_source_file = .{ .path = "src/bot.zig" },
        .target = target,
        .optimize = optimize,
    });

    // Add engine dependency
    const engine_module = b.dependency("engine", .{
        .target = target,
        .optimize = optimize,
    }).module("engine");
    train_exe.root_module.addImport("engine", engine_module);
    bot_exe.root_module.addImport("engine", engine_module);

    // Add nterm dependency
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");
    train_exe.root_module.addImport("nterm", nterm_module);

    // Expose the library root
    _ = b.addModule("bot", .{
        .root_source_file = .{ .path = "src/root.zig" },
        .imports = &.{
            .{ .name = "engine", .module = engine_module },
        },
    });

    b.installArtifact(train_exe);
    b.installArtifact(bot_exe);

    // Add run step
    const run_cmd = b.addRunArtifact(train_exe);
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
    lib_tests.root_module.addImport("engine", engine_module);
    const run_lib_tests = b.addRunArtifact(lib_tests);

    const train_exe_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });
    const run_train_exe_tests = b.addRunArtifact(train_exe_tests);

    const bot_exe_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/bot.zig" },
        .target = target,
        .optimize = optimize,
    });
    const run_bot_exe_tests = b.addRunArtifact(bot_exe_tests);

    const test_step = b.step("test", "Run library tests");
    test_step.dependOn(&run_lib_tests.step);
    test_step.dependOn(&run_train_exe_tests.step);
    test_step.dependOn(&run_bot_exe_tests.step);
}
