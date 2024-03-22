const std = @import("std");
const Build = std.Build;
const builtin = std.builtin;

const engine = @import("engine");

pub fn build(b: *Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    // Dependencies
    const engine_pkg = engine.package(b, target, optimize);
    const bot_module = b.dependency("bot", .{
        .target = target,
        .optimize = optimize,
    }).module("bot");
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");

    _ = buildExe(b, target, optimize, engine_pkg, bot_module, nterm_module);
}

fn buildExe(
    b: *Build,
    target: Build.ResolvedTarget,
    optimize: builtin.OptimizeMode,
    engine_pkg: engine.Package,
    bot_module: *Build.Module,
    nterm_module: *Build.Module,
) void {
    const exe = b.addExecutable(.{
        .name = "Budget Tetris",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });

    // Add dependencies
    exe.step.dependOn(engine_pkg.install_xaudio2);
    const engine_module = bot_module.import_table.get("engine").?;
    exe.root_module.addImport("engine", engine_module);
    exe.root_module.addImport("bot", bot_module);
    exe.root_module.addImport("nterm", nterm_module);

    if (exe.root_module.optimize == .ReleaseFast) {
        exe.root_module.strip = true;
    }

    // Add NN files
    const install_NNs = b.addInstallDirectory(.{
        .source_dir = .{ .path = "NNs" },
        .install_dir = .bin,
        .install_subdir = "NNs",
    });
    exe.step.dependOn(&install_NNs.step);

    // Install sound assets
    const install_sounds = b.addInstallDirectory(.{
        .source_dir = .{ .path = "assets/sound" },
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
