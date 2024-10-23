const std = @import("std");
const Build = std.Build;

pub fn build(b: *Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    // Dependencies
    const bot_module = b.dependency("bot", .{
        .target = target,
        .optimize = optimize,
    }).module("bot");

    _ = buildExe(b, target, optimize, bot_module);
}

fn buildExe(
    b: *Build,
    target: Build.ResolvedTarget,
    optimize: std.builtin.OptimizeMode,
    bot_module: *Build.Module,
) void {
    const exe = b.addExecutable(.{
        .name = "Budget Tetris",
        .root_source_file = b.path("src/main.zig"),
        .target = target,
        .optimize = optimize,
    });

    // Add dependencies
    const engine_module = bot_module.import_table.get("engine").?;
    const zmai_module = bot_module.import_table.get("zmai").?;
    const nterm_module = engine_module.import_table.get("nterm").?;
    exe.root_module.addImport("nterm", nterm_module);
    exe.root_module.addImport("engine", engine_module);
    exe.root_module.addImport("bot", bot_module);
    exe.root_module.addImport("zmai", zmai_module);

    if (exe.root_module.optimize == .ReleaseFast) {
        exe.root_module.strip = true;
    }

    // Add NN files
    const install_NNs = b.addInstallDirectory(.{
        .source_dir = b.path("NNs"),
        .install_dir = .bin,
        .install_subdir = "NNs",
    });
    exe.step.dependOn(&install_NNs.step);

    // Install sound assets
    const install_sounds = b.addInstallDirectory(.{
        .source_dir = b.path("assets/sound"),
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
