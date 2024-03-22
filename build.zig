const std = @import("std");
const Build = std.Build;
const builtin = std.builtin;

const engine = @import("engine");

pub fn build(b: *Build) void {
    const target = b.standardTargetOptions(.{});
    const optimize = b.standardOptimizeOption(.{});

    // Dependencies
    const engine_pkg = engine.package(b, target, optimize);
    const nterm_module = b.dependency("nterm", .{
        .target = target,
        .optimize = optimize,
    }).module("nterm");

    _ = buildExe(b, target, optimize, engine_pkg, nterm_module);
}

fn buildExe(
    b: *Build,
    target: Build.ResolvedTarget,
    optimize: builtin.OptimizeMode,
    engine_pkg: engine.Package,
    nterm_module: *Build.Module,
) void {
    const exe = b.addExecutable(.{
        .name = "Budget Tetris",
        .root_source_file = .{ .path = "src/main.zig" },
        .target = target,
        .optimize = optimize,
    });

    // Add dependencies
    engine_pkg.link(exe);
    exe.root_module.addImport("nterm", nterm_module);

    if (exe.root_module.optimize == .ReleaseFast) {
        exe.root_module.strip = true;
    }

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
