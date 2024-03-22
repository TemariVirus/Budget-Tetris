const std = @import("std");
const Build = std.Build;
const builtin = std.builtin;

pub fn build(b: *std.Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    const engine_module = b.dependency("engine", .{
        .target = target,
        .optimize = optimize,
    }).module("engine");
    const nterm_module = engine_module.import_table.get("nterm").?;

    // Expose the library root
    _ = b.addModule("bot", .{
        .root_source_file = .{ .path = "src/root.zig" },
        .imports = &.{
            .{ .name = "engine", .module = engine_module },
        },
    });

    buildExe(b, target, optimize, engine_module, nterm_module);

    buildTests(b, engine_module);

    buildBench(b, target, engine_module);
}

fn buildExe(
    b: *std.Build,
    target: Build.ResolvedTarget,
    optimize: builtin.OptimizeMode,
    engine_module: *Build.Module,
    nterm_module: *Build.Module,
) void {
    const train_exe = b.addExecutable(.{
        .name = "Budget Tetris Bot Training",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });
    train_exe.root_module.addImport("engine", engine_module);
    train_exe.root_module.addImport("nterm", nterm_module);

    // Add NNs files
    const install_NNs = b.addInstallDirectory(.{
        .source_dir = .{ .path = "./NNs" },
        .install_dir = .bin,
        .install_subdir = "NNs",
    });
    train_exe.step.dependOn(&install_NNs.step);

    b.installArtifact(train_exe);

    // Add run step
    const run_cmd = b.addRunArtifact(train_exe);
    run_cmd.step.dependOn(b.getInstallStep());
    if (b.args) |args| {
        run_cmd.addArgs(args);
    }
    const run_step = b.step("run", "Run the app");
    run_step.dependOn(&run_cmd.step);
}

fn buildTests(b: *std.Build, engine_module: *Build.Module) void {
    const lib_tests = b.addTest(.{
        .root_source_file = .{ .path = "src/root.zig" },
    });
    lib_tests.root_module.addImport("engine", engine_module);
    const run_lib_tests = b.addRunArtifact(lib_tests);

    const test_step = b.step("test", "Run library tests");
    test_step.dependOn(&run_lib_tests.step);
}

fn buildBench(b: *std.Build, target: Build.ResolvedTarget, engine_module: *Build.Module) void {
    const bench_exe = b.addExecutable(.{
        .name = "Budget Tetris Bot Benchmarks",
        .root_source_file = .{ .path = "src/bench.zig" },
        .target = target,
        .optimize = .ReleaseFast,
    });
    bench_exe.root_module.addImport("engine", engine_module);

    b.installArtifact(bench_exe);

    const bench_cmd = b.addRunArtifact(bench_exe);
    bench_cmd.step.dependOn(b.getInstallStep());
    if (b.args) |args| {
        bench_cmd.addArgs(args);
    }
    const bench_step = b.step("bench", "Run benchmarks");
    bench_step.dependOn(&bench_cmd.step);
}
