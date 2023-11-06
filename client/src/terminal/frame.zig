const std = @import("std");
const terminal = @import("../terminal.zig");

const Allocator = std.mem.Allocator;
const Color = terminal.Color;
const Size = terminal.Size;

const assert = @import("std").debug.assert;

pub const Pixel = struct {
    fg: Color,
    bg: Color,
    char: u21,

    pub fn eql(self: Pixel, other: Pixel) bool {
        return self.fg == other.fg and
            self.bg == other.bg and
            self.char == other.char;
    }
};

pub const Frame = struct {
    size: Size,
    pixels: []Pixel,

    pub fn init(allocator: Allocator, size: Size) !Frame {
        const pixels = try allocator.alloc(Pixel, size.area());
        return Frame{ .size = size, .pixels = pixels };
    }

    pub fn deinit(self: Frame, allocator: Allocator) void {
        allocator.free(self.pixels);
    }

    pub fn inBounds(self: Frame, x: u16, y: u16) bool {
        return x < self.size.width and y < self.size.height;
    }

    pub fn get(self: Frame, x: u16, y: u16) Pixel {
        assert(self.inBounds(x, y));
        const index = @as(usize, y) * self.size.width + x;
        return self.pixels[index];
    }

    pub fn set(self: Frame, x: u16, y: u16, p: Pixel) void {
        assert(self.inBounds(x, y));
        const index = @as(usize, y) * self.size.width + x;
        self.pixels[index] = p;
    }
};

pub const FramePool = struct {
    frames: []Frame,
    used: []bool,

    pub fn init(
        allocator: Allocator,
        size: Size,
        capacity: u32,
    ) !FramePool {
        const frames = try allocator.alloc(Frame, capacity);
        for (0..capacity) |i| {
            frames[i] = try Frame.init(allocator, size);
        }

        const is_used = try allocator.alloc(bool, capacity);
        for (0..capacity) |i| {
            is_used[i] = false;
        }

        return FramePool{ .frames = frames, .used = is_used };
    }

    pub fn deinit(self: FramePool, allocator: Allocator) void {
        for (0..self.frames.len) |i| {
            self.frames[i].deinit(allocator);
        }

        allocator.free(self.frames);
        allocator.free(self.used);
    }

    /// Allocates a frame of the given size. Returns null if no frames are
    /// free, or if the requested size is larger than the size specified at
    /// initialisation. The returned frame is filled with spaces, with black
    /// foreground and background color.
    pub fn alloc(self: FramePool, size: Size) ?Frame {
        const len = size.area();
        if (len > self.frames[0].size.area()) {
            return null;
        }

        const frame = blk: for (0..self.used.len) |i| {
            if (!self.used[i]) {
                self.used[i] = true;
                break :blk self.frames[i];
            }
        } else return null;

        for (0..len) |i| {
            frame.pixels[i] = .{
                .fg = Color.Black,
                .bg = Color.Black,
                .char = ' ',
            };
        }
        return Frame{ .size = size, .pixels = frame.pixels[0..len] };
    }

    pub fn free(self: FramePool, frame: Frame) void {
        for (0..self.frames.len) |i| {
            // Use their pixel arrays to identify frames
            if (self.frames[i].pixels.ptr == frame.pixels.ptr) {
                self.used[i] = false;
                return;
            }
        }
    }
};
