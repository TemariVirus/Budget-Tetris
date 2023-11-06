const builtin = @import("builtin");
const std = @import("std");
const time = std.time;
const Allocator = std.mem.Allocator;

pub const Key = windows.VKey;
const KeyStateArray = std.bit_set.ArrayBitSet(usize, 256);

var key_states: KeyStateArray = undefined;
var key_triggers: std.ArrayList(KeyTrigger) = undefined;
var last_tick: i128 = undefined;

const KeyTrigger = struct {
    var id_counter: usize = 0;

    id: usize,
    key: Key,
    delay: u64,
    // A repeat delay of 0 means no repeat.
    repeat_delay: u64,
    elapsed_since_action: ?u64,
    elapsed_since_down: ?u64,
    action: *const fn () void,

    fn init(
        key: Key,
        delay: u64,
        repeat_delay: u64,
        action: *const fn () void,
    ) KeyTrigger {
        defer id_counter += 1;
        return .{
            .id = id_counter,
            .key = key,
            .delay = delay,
            .repeat_delay = repeat_delay,
            .elapsed_since_action = null,
            .elapsed_since_down = null,
            .action = action,
        };
    }

    fn tick(self: *KeyTrigger, elapsed: u64) void {
        const is_down = key_states.isSet(@intFromEnum(self.key));
        if (!is_down) {
            self.elapsed_since_action = null;
            self.elapsed_since_down = null;
            return;
        }

        // If key was just pressed
        if (self.elapsed_since_down == null) {
            self.elapsed_since_down = 0;
            if (self.delay == 0) {
                self.action();
                self.elapsed_since_action = 0;
            }
            return;
        }

        // Initial action call
        self.elapsed_since_down.? += elapsed;
        if (self.elapsed_since_action == null) {
            if (self.elapsed_since_down.? < self.delay) {
                return;
            }

            self.action();
            self.elapsed_since_action = self.elapsed_since_down.? - self.delay;
        } else {
            self.elapsed_since_action.? += elapsed;
        }

        // Repeated action call(s)
        if (self.repeat_delay == 0) {
            return;
        }
        while (self.elapsed_since_action.? >= self.repeat_delay) {
            self.action();
            self.elapsed_since_action.? -= self.repeat_delay;
        }
    }
};

const windows = if (builtin.os.tag == .windows)
    struct {
        const win = std.os.windows;

        var game_hwnd: usize = 0;

        const VKey = enum(c_uint) {
            LButton = 0x01,
            RButton = 0x02,
            Cancel = 0x03,
            MButton = 0x04,
            XButton1 = 0x05,
            XButton2 = 0x06,
            Back = 0x08,
            Tab = 0x09,
            Clear = 0x0C,
            Return = 0x0D,
            Shift = 0x10,
            Control = 0x11,
            Menu = 0x12,
            Pause = 0x13,
            Capital = 0x14,
            /// Same as Hangul
            Kana = 0x15,
            // Hangul = 0x15,
            IMEOn = 0x16,
            Junja = 0x17,
            Final = 0x18,
            /// Same as Kanji
            Hanja = 0x19,
            // Kanji = 0x19,
            IMEOff = 0x1A,
            Escape = 0x1B,
            Convert = 0x1C,
            Nonconvert = 0x1D,
            Accept = 0x1E,
            ModeChange = 0x1F,
            Space = 0x20,
            Prior = 0x21,
            Next = 0x22,
            End = 0x23,
            Home = 0x24,
            Left = 0x25,
            Up = 0x26,
            Right = 0x27,
            Down = 0x28,
            Select = 0x29,
            Print = 0x2A,
            Execute = 0x2B,
            Snapshot = 0x2C,
            Insert = 0x2D,
            Delete = 0x2E,
            Help = 0x2F,
            N0 = 0x30,
            N1 = 0x31,
            N2 = 0x32,
            N3 = 0x33,
            N4 = 0x34,
            N5 = 0x35,
            N6 = 0x36,
            N7 = 0x37,
            N8 = 0x38,
            N9 = 0x39,
            A = 0x41,
            B = 0x42,
            C = 0x43,
            D = 0x44,
            E = 0x45,
            F = 0x46,
            G = 0x47,
            H = 0x48,
            I = 0x49,
            J = 0x4A,
            K = 0x4B,
            L = 0x4C,
            M = 0x4D,
            N = 0x4E,
            O = 0x4F,
            P = 0x50,
            Q = 0x51,
            R = 0x52,
            S = 0x53,
            T = 0x54,
            U = 0x55,
            V = 0x56,
            W = 0x57,
            X = 0x58,
            Y = 0x59,
            Z = 0x5A,
            LWin = 0x5B,
            RWin = 0x5C,
            Apps = 0x5D,
            Sleep = 0x5F,
            Numpad0 = 0x60,
            Numpad1 = 0x61,
            Numpad2 = 0x62,
            Numpad3 = 0x63,
            Numpad4 = 0x64,
            Numpad5 = 0x65,
            Numpad6 = 0x66,
            Numpad7 = 0x67,
            Numpad8 = 0x68,
            Numpad9 = 0x69,
            Multiply = 0x6A,
            Add = 0x6B,
            Separator = 0x6C,
            Subtract = 0x6D,
            Decimal = 0x6E,
            Divide = 0x6F,
            F1 = 0x70,
            F2 = 0x71,
            F3 = 0x72,
            F4 = 0x73,
            F5 = 0x74,
            F6 = 0x75,
            F7 = 0x76,
            F8 = 0x77,
            F9 = 0x78,
            F10 = 0x79,
            F11 = 0x7A,
            F12 = 0x7B,
            F13 = 0x7C,
            F14 = 0x7D,
            F15 = 0x7E,
            F16 = 0x7F,
            F17 = 0x80,
            F18 = 0x81,
            F19 = 0x82,
            F20 = 0x83,
            F21 = 0x84,
            F22 = 0x85,
            F23 = 0x86,
            F24 = 0x87,
            NumLock = 0x90,
            Scroll = 0x91,
            LShift = 0xA0,
            RShift = 0xA1,
            LControl = 0xA2,
            RControl = 0xA3,
            LMenu = 0xA4,
            RMenu = 0xA5,
            BrowserBack = 0xA6,
            BrowserForward = 0xA7,
            BrowserRefresh = 0xA8,
            BrowserStop = 0xA9,
            BrowserSearch = 0xAA,
            BrowserFavorites = 0xAB,
            BrowserHome = 0xAC,
            VolumeMute = 0xAD,
            VolumeDown = 0xAE,
            VolumeUp = 0xAF,
            MediaNextTrack = 0xB0,
            MediaPrevTrack = 0xB1,
            MediaStop = 0xB2,
            MediaPlayPause = 0xB3,
            LaunchMail = 0xB4,
            LaunchMediaSelect = 0xB5,
            LaunchApp1 = 0xB6,
            LaunchApp2 = 0xB7,
            Oem1 = 0xBA,
            OemPlus = 0xBB,
            OemComma = 0xBC,
            OemMinus = 0xBD,
            OemPeriod = 0xBE,
            Oem2 = 0xBF,
            Oem3 = 0xC0,
            Oem4 = 0xDB,
            Oem5 = 0xDC,
            Oem6 = 0xDD,
            Oem7 = 0xDE,
            Oem8 = 0xDF,
            Oem102 = 0xE2,
            ProcessKey = 0xE5,
            Packet = 0xE7,
            Attn = 0xF6,
            CrSel = 0xF7,
            ExSel = 0xF8,
            EraseEOF = 0xF9,
            Play = 0xFA,
            Zoom = 0xFB,
            NoName = 0xFC,
            Pa1 = 0xFD,
            OemClear = 0xFE,
        };

        extern "user32" fn GetAsyncKeyState(vKey: c_uint) callconv(win.WINAPI) win.USHORT;

        extern "user32" fn GetForegroundWindow() callconv(win.WINAPI) win.HWND;

        fn init() !void {
            // Return if already initialized
            if (game_hwnd != 0) {
                return;
            }

            const no_of_tries = 5;
            for (0..no_of_tries) |_| {
                game_hwnd = @intFromPtr(GetForegroundWindow());
                // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow#return-value
                // "The foreground window can be NULL in certain circumstances, such as when a window is losing activation."
                if (game_hwnd != 0) {
                    break;
                }

                std.time.sleep(20 * std.time.ns_per_ms);
            } else {
                return error.NoGameWindow;
            }
        }

        fn hasFocus() bool {
            const focused = GetForegroundWindow();
            return game_hwnd == @intFromPtr(focused);
        }

        fn isKeyDown(key: VKey) bool {
            const key_state = GetAsyncKeyState(@intFromEnum(key));
            return key_state & 0x8000 != 0;
        }
    }
else
    @compileError("Client is only supported on Windows at the moment.");

/// To ensure accurate capturing of keyboard input, this function must be
/// called immediately at the start of the program.
pub fn init(allocator: Allocator) !void {
    try windows.init();
    key_triggers = std.ArrayList(KeyTrigger).init(allocator);
    last_tick = time.nanoTimestamp();
}

pub fn deinit() void {
    key_triggers.deinit();
}

fn updateKeyStates() void {
    key_states = KeyStateArray.initEmpty();
    var checked = KeyStateArray.initEmpty();
    for (key_triggers.items) |trigger| {
        const idx = @intFromEnum(trigger.key);
        // Key down checks are somewhat expensive, don't repeat them
        if (checked.isSet(idx)) {
            continue;
        }

        key_states.setValue(idx, windows.isKeyDown(trigger.key));
        checked.set(idx);
    }
}

pub fn tick() void {
    if (!windows.hasFocus()) {
        return;
    }

    const now = time.nanoTimestamp();
    const elapsed: u64 = blk: {
        if (now - last_tick < 0) {
            return;
        }
        break :blk @intCast(now - last_tick);
    };

    updateKeyStates();
    for (key_triggers.items) |*trigger| {
        trigger.tick(elapsed);
    }
    last_tick = now;
}

/// Triggers will be activated in the order they were added.
pub fn addKeyTrigger(
    key: Key,
    delay: u64,
    repeat_delay: ?u64,
    action: *const fn () void,
) !usize {
    const trigger = KeyTrigger.init(key, delay, repeat_delay orelse 0, action);
    try key_triggers.append(trigger);
    return trigger.id;
}

pub fn removeKeyTrigger(id: usize) void {
    for (key_triggers.items, 0..) |trigger, i| {
        if (trigger.id == id) {
            key_triggers.orderedRemove(i);
            break;
        }
    }
}
