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

const sounds_dir = "sound/";

var audio_context: AudioContext = undefined;
var bgm_stream: *Stream = undefined;

var muted = false;
var bgm_volume: f32 = 0.3;
var sfx_volume: f32 = 0.25;

var sfx_handles = [_]?SoundHandle{null} ** @typeInfo(Sfx).Enum.fields.len;

pub const Sfx = enum(u8) {
    move,
    rotate,
    hard_drop,
    hold,
    pause,
    // TODO: figure out what bfall is
    block_fall,
    garbage_small,
    garbage_large,
    single_clear,
    double_clear,
    tetris_clear,
    triple_clear,
    t_spin,
    perfect_clear,
};

pub fn init(allocator: Allocator) !void {
    try hrErrorOnFail(w32.CoInitializeEx(
        null,
        w32.COINIT_APARTMENTTHREADED | w32.COINIT_DISABLE_OLE1DDE,
    ));

    audio_context = try AudioContext.init(allocator);
    try setSfxVolume(sfx_volume);

    // Load sound effects
    sfx_handles[@intFromEnum(Sfx.move)] = audio_context.loadSound(sounds_dir ++ "move.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.rotate)] = audio_context.loadSound(sounds_dir ++ "rotate.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.hard_drop)] = audio_context.loadSound(sounds_dir ++ "harddrop.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.hold)] = audio_context.loadSound(sounds_dir ++ "hold.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.pause)] = audio_context.loadSound(sounds_dir ++ "pause.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.block_fall)] = audio_context.loadSound(sounds_dir ++ "bfall.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.garbage_small)] = audio_context.loadSound(sounds_dir ++ "garbagesmall.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.garbage_large)] = audio_context.loadSound(sounds_dir ++ "garbagelarge.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.single_clear)] = audio_context.loadSound(sounds_dir ++ "single.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.double_clear)] = audio_context.loadSound(sounds_dir ++ "double.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.triple_clear)] = audio_context.loadSound(sounds_dir ++ "triple.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.tetris_clear)] = audio_context.loadSound(sounds_dir ++ "tetris.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.t_spin)] = audio_context.loadSound(sounds_dir ++ "tspin.m4a") catch null;
    sfx_handles[@intFromEnum(Sfx.perfect_clear)] = audio_context.loadSound(sounds_dir ++ "pc.m4a") catch null;

    // Load background music
    bgm_stream = try Stream.create(
        allocator,
        audio_context.device,
        sounds_dir ++ "Korobeiniki Remix.m4a",
    );
    try setBgmVolume(bgm_volume);
    try hrErrorOnFail(bgm_stream.voice.Start(.{}, xaudio2.COMMIT_NOW));
    // Effects chain needed for volume control for some reason
    try setEffects(bgm_stream.voice);
}

fn setEffects(voice: *xaudio2.ISourceVoice) !void {
    var reverb_apo: ?*w32.IUnknown = null;
    try hrErrorOnFail(xaudio2fx.createReverb(&reverb_apo, 0));
    defer _ = reverb_apo.?.Release();

    // Disable reverb as we won't be using it
    var effect_descriptors = [_]xaudio2.EFFECT_DESCRIPTOR{.{
        .pEffect = reverb_apo.?,
        .InitialState = w32.FALSE,
        .OutputChannels = 2,
    }};
    const effect_chain = xaudio2.EFFECT_CHAIN{
        .EffectCount = effect_descriptors.len,
        .pEffectDescriptors = &effect_descriptors,
    };
    try hrErrorOnFail(voice.SetEffectChain(&effect_chain));
}

pub fn deinit() void {
    audio_context.device.StopEngine();
    bgm_stream.destroy();
    audio_context.deinit();

    audio_context = undefined;
    bgm_stream = undefined;
    w32.CoUninitialize();
}

pub fn playSfx(sfx: Sfx) !void {
    if (muted) {
        return;
    }

    const handle = sfx_handles[@intFromEnum(sfx)];
    if (handle) |h| {
        try audio_context.playSound(h, .{});
    }
}

pub fn getMuted() bool {
    return muted;
}

pub fn setMuted(value: bool) !void {
    muted = value;
    try hrErrorOnFail(bgm_stream.voice.SetVolume(if (muted) 0.0 else bgm_volume));
    try hrErrorOnFail(audio_context.master_voice.SetVolume(if (muted) 0.0 else sfx_volume));
}

pub fn getBgmVolume() f32 {
    return bgm_volume;
}

pub fn setBgmVolume(volume: f32) !void {
    bgm_volume = @min(@max(volume, 0.0), 1.0);
    try hrErrorOnFail(bgm_stream.voice.SetVolume(if (muted) 0.0 else bgm_volume));
}

pub fn getSfxVolume() f32 {
    return sfx_volume;
}

pub fn setSfxVolume(volume: f32) !void {
    sfx_volume = @min(@max(volume, 0.0), 1.0);
    try hrErrorOnFail(audio_context.master_voice.SetVolume(if (muted) 0.0 else sfx_volume));
}

pub fn pause() !void {
    try hrErrorOnFail(bgm_stream.voice.Stop(xaudio2.COMMIT_NOW));
}

pub fn unpause() !void {
    try hrErrorOnFail(bgm_stream.voice.Start(.{}, xaudio2.COMMIT_NOW));
}
