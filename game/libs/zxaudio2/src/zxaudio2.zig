const std = @import("std");
const assert = std.debug.assert;
const zwin32 = @import("zwin32");
const w32 = zwin32.w32;
const IUnknown = w32.IUnknown;
const WINAPI = w32.WINAPI;
const UINT32 = w32.UINT32;
const DWORD = w32.DWORD;
const HRESULT = w32.HRESULT;
const LONGLONG = w32.LONGLONG;
const ULONG = w32.ULONG;
const BOOL = w32.BOOL;
const xaudio2 = zwin32.xaudio2;
const mf = zwin32.mf;
const wasapi = zwin32.wasapi;
const hrPanicOnFail = zwin32.hrPanicOnFail;
const hrErrorOnFail = zwin32.hrErrorOnFail;
const errorToHRESULT = zwin32.errorToHRESULT;

const WAVEFORMATEX = wasapi.WAVEFORMATEX;

test {
    std.testing.refAllDeclsRecursive(@This());
}

const optimal_voice_format = WAVEFORMATEX{
    .wFormatTag = wasapi.WAVE_FORMAT_PCM,
    .nChannels = 1,
    .nSamplesPerSec = 48_000,
    .nAvgBytesPerSec = 2 * 48_000,
    .nBlockAlign = 2,
    .wBitsPerSample = 16,
    .cbSize = @sizeOf(WAVEFORMATEX),
};

const StopOnBufferEndVoiceCallback = extern struct {
    usingnamespace xaudio2.IVoiceCallback.Methods(@This());
    __v: *const xaudio2.IVoiceCallback.VTable = &vtable,

    const vtable = xaudio2.IVoiceCallback.VTable{ .OnBufferEnd = _onBufferEnd };

    fn _onBufferEnd(_: *xaudio2.IVoiceCallback, context: ?*anyopaque) callconv(WINAPI) void {
        const voice = @as(*xaudio2.ISourceVoice, @ptrCast(@alignCast(context)));
        hrPanicOnFail(voice.Stop(.{}, xaudio2.COMMIT_NOW));
    }
};
var stop_on_buffer_end_vcb: StopOnBufferEndVoiceCallback = .{};

pub const AudioContext = struct {
    allocator: std.mem.Allocator,
    device: *xaudio2.IXAudio2,
    master_voice: *xaudio2.IMasteringVoice,
    source_voices: std.ArrayList(*xaudio2.ISourceVoice),
    sound_pool: SoundPool,

    pub fn init(allocator: std.mem.Allocator) !AudioContext {
        const device = blk: {
            var device: ?*xaudio2.IXAudio2 = null;
            try hrErrorOnFail(xaudio2.create(&device, .{}, 0));
            break :blk device.?;
        };

        const master_voice = blk: {
            var voice: ?*xaudio2.IMasteringVoice = null;
            try hrErrorOnFail(device.CreateMasteringVoice(
                &voice,
                xaudio2.DEFAULT_CHANNELS,
                xaudio2.DEFAULT_SAMPLERATE,
                .{},
                null,
                null,
                .GameEffects,
            ));
            break :blk voice.?;
        };

        var source_voices = std.ArrayList(*xaudio2.ISourceVoice).init(allocator);
        {
            var i: u32 = 0;
            while (i < 32) : (i += 1) {
                var voice: ?*xaudio2.ISourceVoice = null;
                try hrErrorOnFail(device.CreateSourceVoice(
                    &voice,
                    &optimal_voice_format,
                    .{},
                    xaudio2.DEFAULT_FREQ_RATIO,
                    @as(*xaudio2.IVoiceCallback, @ptrCast(&stop_on_buffer_end_vcb)),
                    null,
                    null,
                ));
                source_voices.append(voice.?) catch unreachable;
            }
        }

        try hrErrorOnFail(mf.MFStartup(mf.VERSION, 0));

        return .{
            .allocator = allocator,
            .device = device,
            .master_voice = master_voice,
            .source_voices = source_voices,
            .sound_pool = SoundPool.init(allocator),
        };
    }

    pub fn deinit(actx: *AudioContext) void {
        actx.device.StopEngine();
        hrErrorOnFail(mf.MFShutdown()) catch |e| {
            std.debug.print("MFShutdown failed: {}\n", .{e});
        };
        actx.sound_pool.deinit(actx.allocator);
        for (actx.source_voices.items) |voice| {
            voice.DestroyVoice();
        }
        actx.source_voices.deinit();
        actx.master_voice.DestroyVoice();
        _ = actx.device.Release();
        actx.* = undefined;
    }

    pub fn getSourceVoice(actx: *AudioContext) !*xaudio2.ISourceVoice {
        const idle_voice = blk: {
            for (actx.source_voices.items) |voice| {
                var state: xaudio2.VOICE_STATE = undefined;
                voice.GetState(&state, .{ .VOICE_NOSAMPLESPLAYED = true });
                if (state.BuffersQueued == 0) {
                    break :blk voice;
                }
            }

            var voice: ?*xaudio2.ISourceVoice = null;
            try hrErrorOnFail(actx.device.CreateSourceVoice(
                &voice,
                &optimal_voice_format,
                .{},
                xaudio2.DEFAULT_FREQ_RATIO,
                @as(*xaudio2.IVoiceCallback, @ptrCast(&stop_on_buffer_end_vcb)),
                null,
                null,
            ));
            actx.source_voices.append(voice.?) catch unreachable;
            break :blk voice.?;
        };

        // Reset voice state
        try hrErrorOnFail(idle_voice.SetEffectChain(null));
        try hrErrorOnFail(idle_voice.SetVolume(1.0));
        try hrErrorOnFail(idle_voice.SetSourceSampleRate(optimal_voice_format.nSamplesPerSec));
        try hrErrorOnFail(idle_voice.SetChannelVolumes(1, &[1]f32{1.0}, xaudio2.COMMIT_NOW));
        try hrErrorOnFail(idle_voice.SetFrequencyRatio(1.0, xaudio2.COMMIT_NOW));

        return idle_voice;
    }

    pub fn playSound(actx: *AudioContext, handle: SoundHandle, params: struct {
        play_begin: u32 = 0,
        play_length: u32 = 0,
        loop_begin: u32 = 0,
        loop_length: u32 = 0,
        loop_count: u32 = 0,
    }) !void {
        const sound = actx.sound_pool.lookupSound(handle);
        if (sound == null)
            return;

        const voice = try actx.getSourceVoice();

        try hrErrorOnFail(voice.SubmitSourceBuffer(&.{
            .Flags = .{ .END_OF_STREAM = true },
            .AudioBytes = @as(u32, @intCast(sound.?.data.?.len)),
            .pAudioData = sound.?.data.?.ptr,
            .PlayBegin = params.play_begin,
            .PlayLength = params.play_length,
            .LoopBegin = params.loop_begin,
            .LoopLength = params.loop_length,
            .LoopCount = params.loop_count,
            .pContext = voice,
        }, null));

        try hrErrorOnFail(voice.Start(.{}, xaudio2.COMMIT_NOW));
    }

    pub fn loadSound(actx: *AudioContext, relpath: []const u8) !SoundHandle {
        var buffer: [std.fs.MAX_PATH_BYTES]u8 = undefined;
        var fba = std.heap.FixedBufferAllocator.init(buffer[0..]);
        const allocator = fba.allocator();

        const abspath = std.fs.path.join(allocator, &.{
            std.fs.selfExeDirPathAlloc(allocator) catch unreachable,
            relpath,
        }) catch unreachable;

        var abspath_w: [std.os.windows.PATH_MAX_WIDE:0]u16 = undefined;
        abspath_w[std.unicode.utf8ToUtf16Le(abspath_w[0..], abspath) catch unreachable] = 0;

        const data = try loadBufferData(actx.allocator, abspath_w[0..]);
        return actx.sound_pool.addSound(data);
    }
};

pub const Stream = struct {
    critical_section: w32.CRITICAL_SECTION,
    allocator: std.mem.Allocator,
    voice: *xaudio2.ISourceVoice,
    voice_cb: *StreamVoiceCallback,
    reader: *mf.ISourceReader,
    reader_cb: *SourceReaderCallback,

    pub fn create(allocator: std.mem.Allocator, device: *xaudio2.IXAudio2, relpath: []const u8) !*Stream {
        const voice_cb = allocator.create(StreamVoiceCallback) catch unreachable;
        voice_cb.* = .{};

        var cs: w32.CRITICAL_SECTION = undefined;
        w32.InitializeCriticalSection(&cs);

        const source_reader_cb = allocator.create(SourceReaderCallback) catch unreachable;
        source_reader_cb.* = .{};

        var sample_rate: u32 = 0;
        const source_reader = blk: {
            var attribs: *mf.IAttributes = undefined;
            try hrErrorOnFail(mf.MFCreateAttributes(&attribs, 1));
            defer _ = attribs.Release();

            try hrErrorOnFail(attribs.SetUnknown(
                &mf.SOURCE_READER_ASYNC_CALLBACK,
                @as(*w32.IUnknown, @ptrCast(source_reader_cb)),
            ));

            var arena_state = std.heap.ArenaAllocator.init(allocator);
            defer arena_state.deinit();
            const arena = arena_state.allocator();

            const abspath = std.fs.path.join(arena, &.{
                std.fs.selfExeDirPathAlloc(arena) catch unreachable,
                relpath,
            }) catch unreachable;

            var abspath_w: [std.os.windows.PATH_MAX_WIDE:0]u16 = undefined;
            abspath_w[std.unicode.utf8ToUtf16Le(abspath_w[0..], abspath) catch unreachable] = 0;

            var source_reader: *mf.ISourceReader = undefined;
            try hrErrorOnFail(mf.MFCreateSourceReaderFromURL(&abspath_w, attribs, &source_reader));

            var media_type: *mf.IMediaType = undefined;
            try hrErrorOnFail(source_reader.GetNativeMediaType(mf.SOURCE_READER_FIRST_AUDIO_STREAM, 0, &media_type));
            defer _ = media_type.Release();

            try hrErrorOnFail(media_type.GetUINT32(&mf.MT_AUDIO_SAMPLES_PER_SECOND, &sample_rate));

            try hrErrorOnFail(media_type.SetGUID(&mf.MT_MAJOR_TYPE, &mf.MediaType_Audio));
            try hrErrorOnFail(media_type.SetGUID(&mf.MT_SUBTYPE, &mf.AudioFormat_PCM));
            try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_NUM_CHANNELS, 2));
            try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_SAMPLES_PER_SECOND, sample_rate));
            try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_BITS_PER_SAMPLE, 16));
            try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_BLOCK_ALIGNMENT, 4));
            try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_AVG_BYTES_PER_SECOND, 4 * sample_rate));
            try hrErrorOnFail(media_type.SetUINT32(&mf.MT_ALL_SAMPLES_INDEPENDENT, w32.TRUE));
            try hrErrorOnFail(source_reader.SetCurrentMediaType(mf.SOURCE_READER_FIRST_AUDIO_STREAM, null, media_type));

            break :blk source_reader;
        };
        assert(sample_rate != 0);

        const voice = blk: {
            var voice: ?*xaudio2.ISourceVoice = null;
            try hrErrorOnFail(device.CreateSourceVoice(&voice, &.{
                .wFormatTag = wasapi.WAVE_FORMAT_PCM,
                .nChannels = 2,
                .nSamplesPerSec = sample_rate,
                .nAvgBytesPerSec = 4 * sample_rate,
                .nBlockAlign = 4,
                .wBitsPerSample = 16,
                .cbSize = @sizeOf(wasapi.WAVEFORMATEX),
            }, .{}, xaudio2.DEFAULT_FREQ_RATIO, @as(*xaudio2.IVoiceCallback, @ptrCast(voice_cb)), null, null));
            break :blk voice.?;
        };

        const stream = allocator.create(Stream) catch unreachable;
        stream.* = .{
            .critical_section = cs,
            .allocator = allocator,
            .voice = voice,
            .voice_cb = voice_cb,
            .reader = source_reader,
            .reader_cb = source_reader_cb,
        };

        voice_cb.stream = stream;
        source_reader_cb.stream = stream;

        // Start async loading/decoding
        try hrErrorOnFail(source_reader.ReadSample(mf.SOURCE_READER_FIRST_AUDIO_STREAM, .{}, null, null, null, null));
        try hrErrorOnFail(source_reader.ReadSample(mf.SOURCE_READER_FIRST_AUDIO_STREAM, .{}, null, null, null, null));

        return stream;
    }

    pub fn destroy(stream: *Stream) void {
        {
            const refcount = stream.reader.Release();
            assert(refcount == 0);
        }
        {
            const refcount = stream.reader_cb.Release();
            assert(refcount == 0);
        }
        w32.DeleteCriticalSection(&stream.critical_section);
        stream.voice.DestroyVoice();
        stream.allocator.destroy(stream.voice_cb);
        stream.allocator.destroy(stream.reader_cb);
        stream.allocator.destroy(stream);
    }

    pub fn setCurrentPosition(stream: *Stream, position: i64) !void {
        w32.EnterCriticalSection(&stream.critical_section);
        defer w32.LeaveCriticalSection(&stream.critical_section);

        const pos = w32.PROPVARIANT{ .vt = w32.VT_I8, .u = .{ .hVal = position } };
        try hrErrorOnFail(stream.reader.SetCurrentPosition(&w32.GUID_NULL, &pos));
        try hrErrorOnFail(stream.reader.ReadSample(
            mf.SOURCE_READER_FIRST_AUDIO_STREAM,
            .{},
            null,
            null,
            null,
            null,
        ));
    }

    fn endOfStreamChunk(stream: *Stream, buffer: *mf.IMediaBuffer) !void {
        w32.EnterCriticalSection(&stream.critical_section);
        defer w32.LeaveCriticalSection(&stream.critical_section);

        try hrErrorOnFail(buffer.Unlock());
        const refcount = buffer.Release();
        assert(refcount == 0);

        // Request new audio buffer
        try hrErrorOnFail(stream.reader.ReadSample(mf.SOURCE_READER_FIRST_AUDIO_STREAM, .{}, null, null, null, null));
    }

    fn playStreamChunk(
        stream: *Stream,
        status: HRESULT,
        _: DWORD,
        stream_flags: mf.SOURCE_READER_FLAG,
        _: LONGLONG,
        sample: ?*mf.ISample,
    ) !void {
        w32.EnterCriticalSection(&stream.critical_section);
        defer w32.LeaveCriticalSection(&stream.critical_section);

        if (stream_flags.END_OF_STREAM) {
            try setCurrentPosition(stream, 0);
            return;
        }
        if (status != w32.S_OK or sample == null) {
            return;
        }

        var buffer: *mf.IMediaBuffer = undefined;
        try hrErrorOnFail(sample.?.ConvertToContiguousBuffer(&buffer));

        var data_ptr: [*]u8 = undefined;
        var data_len: u32 = 0;
        try hrErrorOnFail(buffer.Lock(&data_ptr, null, &data_len));

        // Submit decoded buffer
        try hrErrorOnFail(stream.voice.SubmitSourceBuffer(&.{
            .Flags = .{},
            .AudioBytes = data_len,
            .pAudioData = data_ptr,
            .PlayBegin = 0,
            .PlayLength = 0,
            .LoopBegin = 0,
            .LoopLength = 0,
            .LoopCount = 0,
            .pContext = buffer, // Store pointer to the buffer so that we can release it in endOfStreamChunk()
        }, null));
    }
};

const StreamVoiceCallback = extern struct {
    usingnamespace xaudio2.IVoiceCallback.Methods(@This());
    __v: *const xaudio2.IVoiceCallback.VTable = &vtable,

    stream: ?*Stream = null,

    const vtable = xaudio2.IVoiceCallback.VTable{ .OnBufferEnd = _onBufferEnd };

    fn _onBufferEnd(iself: *xaudio2.IVoiceCallback, context: ?*anyopaque) callconv(WINAPI) void {
        const self = @as(*StreamVoiceCallback, @ptrCast(iself));
        self.stream.?.endOfStreamChunk(@ptrCast(@alignCast(context))) catch |e|
            hrPanicOnFail(errorToHRESULT(e));
    }
};

const SourceReaderCallback = extern struct {
    usingnamespace mf.ISourceReaderCallback.Methods(@This());
    __v: *const mf.ISourceReaderCallback.VTable = &vtable,

    refcount: u32 = 1,
    stream: ?*Stream = null,

    const vtable = mf.ISourceReaderCallback.VTable{
        .base = .{
            .QueryInterface = _queryInterface,
            .AddRef = _addRef,
            .Release = _release,
        },
        .OnReadSample = _onReadSample,
    };

    fn _queryInterface(
        iself: *IUnknown,
        guid: *const w32.GUID,
        outobj: ?*?*anyopaque,
    ) callconv(WINAPI) HRESULT {
        assert(outobj != null);
        const self = @as(*SourceReaderCallback, @ptrCast(iself));

        if (std.mem.eql(u8, std.mem.asBytes(guid), std.mem.asBytes(&w32.IID_IUnknown))) {
            outobj.?.* = self;
            _ = self.AddRef();
            return w32.S_OK;
        } else if (std.mem.eql(u8, std.mem.asBytes(guid), std.mem.asBytes(&mf.IID_ISourceReaderCallback))) {
            outobj.?.* = self;
            _ = self.AddRef();
            return w32.S_OK;
        }

        outobj.?.* = null;
        return w32.E_NOINTERFACE;
    }

    fn _addRef(iself: *IUnknown) callconv(WINAPI) ULONG {
        const self = @as(*SourceReaderCallback, @ptrCast(iself));
        const prev_refcount = @atomicRmw(u32, &self.refcount, .Add, 1, .Monotonic);
        return prev_refcount + 1;
    }

    fn _release(iself: *IUnknown) callconv(WINAPI) ULONG {
        const self = @as(*SourceReaderCallback, @ptrCast(iself));
        const prev_refcount = @atomicRmw(u32, &self.refcount, .Sub, 1, .Monotonic);
        assert(prev_refcount > 0);
        return prev_refcount - 1;
    }

    fn _onReadSample(
        iself: *mf.ISourceReaderCallback,
        status: HRESULT,
        stream_index: DWORD,
        stream_flags: mf.SOURCE_READER_FLAG,
        timestamp: LONGLONG,
        sample: ?*mf.ISample,
    ) callconv(WINAPI) HRESULT {
        const self = @as(*SourceReaderCallback, @ptrCast(iself));
        self.stream.?.playStreamChunk(
            status,
            stream_index,
            stream_flags,
            timestamp,
            sample,
        ) catch |e| return errorToHRESULT(e);
        return w32.S_OK;
    }
};

fn loadBufferData(allocator: std.mem.Allocator, audio_file_path: [:0]const u16) ![]const u8 {
    var source_reader: *mf.ISourceReader = undefined;
    try hrErrorOnFail(mf.MFCreateSourceReaderFromURL(audio_file_path, null, &source_reader));
    defer _ = source_reader.Release();

    var media_type: *mf.IMediaType = undefined;
    try hrErrorOnFail(source_reader.GetNativeMediaType(mf.SOURCE_READER_FIRST_AUDIO_STREAM, 0, &media_type));
    defer _ = media_type.Release();

    try hrErrorOnFail(media_type.SetGUID(&mf.MT_MAJOR_TYPE, &mf.MediaType_Audio));
    try hrErrorOnFail(media_type.SetGUID(&mf.MT_SUBTYPE, &mf.AudioFormat_PCM));
    try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_NUM_CHANNELS, optimal_voice_format.nChannels));
    try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_SAMPLES_PER_SECOND, optimal_voice_format.nSamplesPerSec));
    try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_BITS_PER_SAMPLE, optimal_voice_format.wBitsPerSample));
    try hrErrorOnFail(media_type.SetUINT32(&mf.MT_AUDIO_BLOCK_ALIGNMENT, optimal_voice_format.nBlockAlign));
    try hrErrorOnFail(media_type.SetUINT32(
        &mf.MT_AUDIO_AVG_BYTES_PER_SECOND,
        optimal_voice_format.nBlockAlign * optimal_voice_format.nSamplesPerSec,
    ));
    try hrErrorOnFail(media_type.SetUINT32(&mf.MT_ALL_SAMPLES_INDEPENDENT, w32.TRUE));
    try hrErrorOnFail(source_reader.SetCurrentMediaType(mf.SOURCE_READER_FIRST_AUDIO_STREAM, null, media_type));

    var data = std.ArrayList(u8).init(allocator);
    while (true) {
        var flags: mf.SOURCE_READER_FLAG = .{};
        var sample: ?*mf.ISample = null;
        defer {
            if (sample) |s| _ = s.Release();
        }
        try hrErrorOnFail(source_reader.ReadSample(
            mf.SOURCE_READER_FIRST_AUDIO_STREAM,
            .{},
            null,
            &flags,
            null,
            &sample,
        ));
        if (flags.END_OF_STREAM) {
            break;
        }

        var buffer: *mf.IMediaBuffer = undefined;
        try hrErrorOnFail(sample.?.ConvertToContiguousBuffer(&buffer));
        defer _ = buffer.Release();

        var data_ptr: [*]u8 = undefined;
        var data_len: u32 = 0;
        try hrErrorOnFail(buffer.Lock(&data_ptr, null, &data_len));
        data.appendSlice(data_ptr[0..data_len]) catch unreachable;
        try hrErrorOnFail(buffer.Unlock());
    }
    return data.toOwnedSlice() catch unreachable;
}

pub const SoundHandle = struct {
    index: u16 align(4) = 0,
    generation: u16 = 0,
};

const Sound = struct {
    data: ?[]const u8,
};

const SoundPool = struct {
    const max_num_sounds = 256;

    sounds: []Sound,
    generations: []u16,

    fn init(allocator: std.mem.Allocator) SoundPool {
        return .{
            .sounds = blk: {
                const sounds = allocator.alloc(Sound, max_num_sounds + 1) catch unreachable;
                for (sounds) |*sound| {
                    sound.* = .{
                        .data = null,
                    };
                }
                break :blk sounds;
            },
            .generations = blk: {
                const generations = allocator.alloc(u16, max_num_sounds + 1) catch unreachable;
                for (generations) |*gen|
                    gen.* = 0;
                break :blk generations;
            },
        };
    }

    fn deinit(pool: *SoundPool, allocator: std.mem.Allocator) void {
        for (pool.sounds) |sound| {
            if (sound.data != null)
                allocator.free(sound.data.?);
        }
        allocator.free(pool.sounds);
        allocator.free(pool.generations);
        pool.* = undefined;
    }

    fn addSound(
        pool: SoundPool,
        data: []const u8,
    ) SoundHandle {
        var slot_idx: u32 = 1;
        while (slot_idx <= max_num_sounds) : (slot_idx += 1) {
            if (pool.sounds[slot_idx].data == null)
                break;
        }
        assert(slot_idx <= max_num_sounds);

        pool.sounds[slot_idx] = .{ .data = data };
        return .{
            .index = @as(u16, @intCast(slot_idx)),
            .generation = blk: {
                pool.generations[slot_idx] += 1;
                break :blk pool.generations[slot_idx];
            },
        };
    }

    fn destroySound(pool: SoundPool, allocator: std.mem.Allocator, handle: SoundHandle) void {
        const sound = pool.lookupSound(handle);
        if (sound == null)
            return;

        allocator.free(sound.data.?);
        sound.?.* = .{ .data = null };
    }

    fn isSoundValid(pool: SoundPool, handle: SoundHandle) bool {
        return handle.index > 0 and
            handle.index <= max_num_sounds and
            handle.generation > 0 and
            handle.generation == pool.generations[handle.index] and
            pool.sounds[handle.index].data != null;
    }

    fn lookupSound(pool: SoundPool, handle: SoundHandle) ?*Sound {
        if (pool.isSoundValid(handle)) {
            return &pool.sounds[handle.index];
        }
        return null;
    }
};
