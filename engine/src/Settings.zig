const Self = @This();

pub const default = Self{
    .allow180 = true,
    .g = 50,
    .soft_g = 1000,
};

const settings_path = "settings.json";
var cached: ?Self = null;

/// Allow 180 spins.
allow180: bool,
/// The gravity in thousandths of cells per frame.
g: u16,
/// The softdrop gravity in thousandths of cells per frame.
soft_g: u16,

pub fn playerSettings() *const Self {
    if (cached == null) {
        _ = read(settings_path);
    }
    return &cached;
}

// TODO: Implement reading settings from file
pub fn read(path: []u8) Self {
    _ = path;
    unreachable;

    // Cache read settings
    // cached = settings;
    // return settings;
}

// TODO: Implement writing settings to file
pub fn write(path: []u8) bool {
    _ = path;
    unreachable;
}

pub fn actualG(self: *Self) f32 {
    return @as(f32, @floatFromInt(self.g)) / 1000.0;
}

pub fn actualSoftG(self: *Self) f32 {
    return @as(f32, @floatFromInt(self.soft_g)) / 1000.0;
}

pub fn fromActualG(self: *Self, g: f32) void {
    self.g = @intFromFloat(@max(g, 40) * 1000.0);
}

pub fn fromActualSoftG(self: *Self, soft_g: f32) void {
    self.soft_g = @intFromFloat(@max(soft_g, 40) * 1000.0);
}
