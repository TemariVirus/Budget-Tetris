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
    const bot_module = b.dependency("bot", .{
        .target = target,
        .optimize = optimize,
    }).module("bot");
    const zwin32_pkg = zwin32.package(b, target, optimize, .{});
    const zxaudio2_pkg = zxaudio2.package(b, target, optimize, .{
        .deps = .{ .zwin32 = zwin32_pkg.zwin32 },
    });

    _ = buildExe(
        b,
        target,
        optimize,
        nterm_module,
        bot_module,
        zwin32_pkg.zwin32,
        zxaudio2_pkg.zxaudio2,
        zwin32_pkg.install_xaudio2,
    );
}

fn buildExe(
    b: *Build,
    target: Build.ResolvedTarget,
    optimize: builtin.OptimizeMode,
    nterm_module: *Build.Module,
    bot_module: *Build.Module,
    zwin32_module: *Build.Module,
    zxaudio2_module: *Build.Module,
    install_xaudio2: *Build.Step,
) void {
    const exe = b.addExecutable(.{
        .name = "Budget Tetris",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });

    // Add dependencies
    const engine_module = bot_module.import_table.get("engine").?;
    exe.root_module.addImport("nterm", nterm_module);
    exe.root_module.addImport("engine", engine_module);
    exe.root_module.addImport("bot", bot_module);
    exe.root_module.addImport("zwin32", zwin32_module);
    exe.root_module.addImport("zxaudio2", zxaudio2_module);
    exe.step.dependOn(install_xaudio2);

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
