//! Adapted from the Zig standard library.
const Allocator = @import("std").mem.Allocator;
const assert = @import("std").debug.assert;
const copyForwards = @import("std").mem.copyForwards;

/// This ring queue stores read and write indices while being able to utilise
/// the full backing slice by incrementing the indices modulo twice the slice's
/// length and reducing indices modulo the slice's length on slice access. This
/// means that whether the queue is full or empty can be distinguished by
/// looking at the difference between the read and write indices without adding
/// an extra boolean flag or having to reserve a slot in the queue.
///
/// This queue has not been implemented with thread safety in mind, and
/// therefore should not be assumed to be suitable for use cases involving
/// separate reader and writer threads.
pub fn RingQueue(comptime T: type) type {
    return struct {
        const Self = @This();

        data: []T,
        head: usize,
        tail: usize,

        pub const Error = error{ Full, ReadLengthInvalid };

        /// Allocates a new `RingQueue`; `deinit()` should be called to free the queue.
        pub fn init(allocator: Allocator, capacity: usize) Allocator.Error!Self {
            const bytes = try allocator.alloc(T, capacity);
            return Self{
                .data = bytes,
                .head = 0,
                .tail = 0,
            };
        }

        /// Frees the data backing a `RingQueue`; must be passed the same `Allocator` as
        /// `init()`.
        pub fn deinit(self: *Self, allocator: Allocator) void {
            allocator.free(self.data);
            self.* = undefined;
        }

        /// Returns `index` modulo the length of the backing slice.
        pub fn mask(self: Self, index: usize) usize {
            return index % self.data.len;
        }

        /// Returns `index` modulo twice the length of the backing slice.
        pub fn mask2(self: Self, index: usize) usize {
            return index % (2 * self.data.len);
        }

        /// Enqueues `item` into the queue. Returns `error.Full` if the queue
        /// is full.
        pub fn enqueue(self: *Self, item: T) Error!void {
            if (self.isFull()) return error.Full;
            self.data[self.mask(self.head)] = item;
            self.head = self.mask2(self.head + 1);
        }

        /// Dequeues the first item from the queue and return it. Returns `null` if the
        /// queue is empty.
        pub fn dequeue(self: *Self) ?T {
            if (self.isEmpty()) return null;
            const item = self.data[self.mask(self.tail)];
            self.tail = self.mask2(self.tail + 1);
            return item;
        }

        /// Reads the item at `index` from the queue and returns it without dequeuing.
        /// Returns `null` if `index` is out of bounds.
        pub fn peekIndex(self: *Self, index: usize) ?T {
            if (index >= self.len()) return null;
            return self.data[self.mask(self.tail + index)];
        }

        /// Returns `true` if the queue is empty and `false` otherwise.
        pub fn isEmpty(self: Self) bool {
            return self.head == self.tail;
        }

        /// Returns `true` if the queue is full and `false` otherwise.
        pub fn isFull(self: Self) bool {
            return self.mask2(self.head + self.data.len) == self.tail;
        }

        /// Returns the length of the queue.
        pub fn len(self: Self) usize {
            const wrap_offset = 2 * self.data.len * @intFromBool(self.head < self.tail);
            const adjusted_head = self.head + wrap_offset;
            return adjusted_head - self.tail;
        }
    };
}
