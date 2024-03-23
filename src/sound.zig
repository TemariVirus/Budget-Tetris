const std = @import("std");
const Allocator = std.mem.Allocator;

const zwin32 = @import("zwin32");
const w32 = zwin32.w32;

const xaudio2 = zwin32.xaudio2;
const xaudio2fx = zwin32.xaudio2fx;
const hrErrorOnFail = zwin32.hrErrorOnFail;

const zxaudio2 = @import("zxaudio2");
const AudioContext = zxaudio2.AudioContext;
const Stream = zxaudio2.Stream;
const SoundHandle = zxaudio2.SoundHandle;

pub const Sfx = @import("engine").player.Sfx;

const sounds_dir = "sound/";

var audio_context: AudioContext = undefined;
var bgm_stream: ?*Stream = null;

pub var muted = false;
pub var volume: f32 = 1.0;

var sfx_handles = [_]?SoundHandle{null} ** @typeInfo(Sfx).Enum.fields.len;

pub fn init(allocator: Allocator) !void {
    try hrErrorOnFail(w32.CoInitializeEx(
        null,
        w32.COINIT_APARTMENTTHREADED | w32.COINIT_DISABLE_OLE1DDE,
    ));
    errdefer deinit();

    audio_context = try AudioContext.init(allocator);
    try setVolume(volume);

    // Load background music
    bgm_stream = Stream.create(
        allocator,
        audio_context.device,
        sounds_dir ++ "Korobeiniki Remix.m4a",
    ) catch null;
    if (bgm_stream) |stream| {
        try hrErrorOnFail(stream.voice.Start(.{}, xaudio2.COMMIT_NOW));
    }

    // Load sound effects
    sfx_handles[@intFromEnum(Sfx.move)] = audio_context.loadSound(sounds_dir ++ "move.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.rotate)] = audio_context.loadSound(sounds_dir ++ "rotate.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.hard_drop)] = audio_context.loadSound(sounds_dir ++ "harddrop.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.hold)] = audio_context.loadSound(sounds_dir ++ "hold.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.pause)] = audio_context.loadSound(sounds_dir ++ "pause.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.landing)] = audio_context.loadSound(sounds_dir ++ "landing.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.garbage_small)] = audio_context.loadSound(sounds_dir ++ "garbagesmall.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.garbage_large)] = audio_context.loadSound(sounds_dir ++ "garbagelarge.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.single_clear)] = audio_context.loadSound(sounds_dir ++ "single.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.double_clear)] = audio_context.loadSound(sounds_dir ++ "double.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.triple_clear)] = audio_context.loadSound(sounds_dir ++ "triple.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.tetris_clear)] = audio_context.loadSound(sounds_dir ++ "tetris.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.t_spin)] = audio_context.loadSound(sounds_dir ++ "tspin.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.perfect_clear)] = audio_context.loadSound(sounds_dir ++ "pc.m4a") catch null;
}

pub fn deinit() void {
    if (bgm_stream) |stream| {
        stream.destroy();
    }
    audio_context.deinit();

    audio_context = undefined;
    bgm_stream = null;
    w32.CoUninitialize();
}

pub fn playSfx(sfx: Sfx) void {
    if (muted) {
        return;
    }

    const handle = sfx_handles[@intFromEnum(sfx)];
    if (handle) |h| {
        audio_context.playSound(h, .{}) catch {};
    }
}

pub fn setMuted(value: bool) !void {
    muted = value;
    try setVolume(volume);
}

pub fn setVolume(new_volume: f32) !void {
    volume = @min(@max(new_volume, 0.0), 1.0);
    // TODO: BGM doesn't respect volume changes in ReleaseFast mode
    try hrErrorOnFail(audio_context.master_voice.SetVolume(if (muted) 0.0 else volume));
}

pub fn pause() !void {
    if (bgm_stream) |stream| {
        try hrErrorOnFail(stream.voice.Stop(.{}, xaudio2.COMMIT_NOW));
    }
}

pub fn unpause() !void {
    if (bgm_stream) |stream| {
        try hrErrorOnFail(stream.voice.Start(.{}, xaudio2.COMMIT_NOW));
    }
}
