const assert = @import("std").debug.assert;
const nanoTimestamp = @import("std").time.nanoTimestamp;

const PeriodicTrigger = @This();

period: u64,
last: i128,

pub fn init(period: u64) PeriodicTrigger {
    assert(period > 0);
    return .{
        .period = period,
        .last = nanoTimestamp(),
    };
}

pub fn trigger(self: *PeriodicTrigger) ?u64 {
    const now = nanoTimestamp();
    const elapsed: u128 = @intCast(now - self.last);
    if (elapsed < self.period) {
        return null;
    }

    // Only add multiples of the period to ensure rendering happens at most
    // once per frame, while allowing frame skips.
    const partial_frame_time = elapsed % self.period;
    const time_to_add: u64 = @intCast(elapsed - partial_frame_time);
    self.last += time_to_add;
    return time_to_add;
}
