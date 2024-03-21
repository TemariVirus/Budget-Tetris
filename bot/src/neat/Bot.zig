const std = @import("std");
const expect = std.testing.expect;

const engine = @import("engine");
const AttackTable = engine.attack.AttackTable;
const BoardMask = engine.bit_masks.BoardMask;
// TODO: Replace with `anytype`
const GameState = engine.GameState(engine.bags.SevenBag, engine.kicks.srsPlus);
const Facing = engine.pieces.Facing;
const Rotation = engine.kicks.Rotation;

const root = @import("../root.zig");
const NN = root.neat.NN;
const Placement = root.Placement;

const Self = @This();

// TODO: Cache transpositions

// Old bot used a height of 24 for its board masks
const LEGACY_HEIGHT = 24;
const DISCOUNT_FACTOR = 0.95;
const MAX_DEPTH = 24;

const MOVE_MUL = 1.05;
const MOVE_TARGET = 6;
const MOVE_TRESH_MIN = -1;
const MOVE_TRESH_MAX = -0.01;
const MOVE_TRESH_START = -0.05;

const DISCOUNTS: [MAX_DEPTH]f32 = blk: {
    var discounts: [MAX_DEPTH]f32 = undefined;
    for (0..MAX_DEPTH) |i| {
        discounts[i] = std.math.pow(f32, DISCOUNT_FACTOR, i);
    }
    break :blk discounts;
};

network: NN,
move_tresh: f32 = MOVE_TRESH_START,
think_nanos: u128,
attack_table: AttackTable,

start_time: i128 = undefined,
end_search: bool = false,
current_depth: u32 = 0,
max_depth: f32 = 0,
node_count: u64 = 0,

pub fn init(network: NN, think_seconds: f64, attack_table: AttackTable) Self {
    return .{
        .network = network,
        .think_nanos = @intFromFloat(think_seconds * std.time.ns_per_s),
        .attack_table = attack_table,
    };
}

pub fn findMoves(self: *Self, game: GameState) Placement {
    self.start_time = std.time.nanoTimestamp();
    self.end_search = false;
    self.max_depth = 0;
    self.node_count = 0;

    var best_score = -std.math.inf(f32);
    var best_placement: Placement = undefined;

    const output = self.network.predict(getFeatures(game.playfield, self.network.inputs_used, 0, 0, 0));
    // TODO: Cache
    // ulong hash = HashState(0, 0, 0);
    // if (!CachedStateValues.ContainsKey(hash))
    //      CachedStateValues.Add(hash, outs[0]);

    // Iterative deepening
    outer: for (0..MAX_DEPTH) |depth| {
        self.current_depth = @intCast(depth);
        for (0..2) |i| {
            var clone = game;
            if (i == 1) {
                // Don't hold if it's the same piece
                if (clone.current.kind == clone.hold_kind) {
                    break;
                }

                clone.hold();
            }

            for ([_]Facing{ Facing.up, Facing.right, Facing.down, Facing.left }) |facing| {
                clone.current.facing = facing;
                var x = clone.current.minX();
                const max_x = clone.current.maxX();
                while (x <= max_x) : (x += 1) {
                    if (self.end_search) {
                        break :outer;
                    }

                    clone.current.facing = facing;
                    clone.pos = .{ .x = x, .y = clone.current.kind.startPos().y };
                    _ = clone.dropToGround();
                    const dropped_y = clone.pos.y;

                    // S and Z pieces are the same when rotated 180 degrees, so we only check
                    // the up and right orientations
                    if ((clone.current.kind != .s and clone.current.kind != .z) or
                        (facing == .up or facing == .right))
                    {
                        var new_state = clone;
                        const info = new_state.lockCurrent(-1);
                        new_state.nextPiece();
                        // Check if better
                        const attack: f32 = @floatFromInt(self.attack_table.getAttack(info, new_state.b2b, new_state.combo));
                        const score = search(self, new_state, @intCast(depth), info.cleared, attack, output);
                        if (score > best_score) {
                            best_score = score;
                            best_placement = .{
                                .piece = clone.current,
                                .pos = clone.pos,
                            };
                        }
                    }

                    // Only try to spin left/right facing pieces into place, except O pieces
                    // which have no kicks
                    if ((clone.current.facing == .left or clone.current.facing == .right) and clone.current.kind != .o) {
                        for (0..2) |r| {
                            const rotation = switch (r) {
                                0 => Rotation.quarter_cw,
                                1 => Rotation.quarter_ccw,
                                else => unreachable,
                            };
                            clone.pos = .{ .x = x, .y = dropped_y };
                            clone.current.facing = facing;

                            for (0..2) |_| {
                                const kick = clone.rotate(rotation);
                                if (kick == -1 or !clone.onGround()) {
                                    break;
                                }

                                // Place piece
                                var new_state = clone;
                                const info = new_state.lockCurrent(kick);
                                new_state.nextPiece();
                                // Check if better
                                const attack: f32 = @floatFromInt(self.attack_table.getAttack(
                                    info,
                                    new_state.b2b,
                                    new_state.combo,
                                ));
                                const score = search(
                                    self,
                                    new_state,
                                    @intCast(depth),
                                    info.cleared,
                                    attack,
                                    output,
                                );
                                if (score > best_score) {
                                    best_score = score;
                                    best_placement = .{
                                        .piece = clone.current,
                                        .pos = clone.pos,
                                    };
                                }

                                // Only try to spin T pieces twice (for TSTs)
                                if (clone.current.kind != .t) {
                                    break;
                                }
                            }
                        }
                    }
                }

                // O pieces can't be rotated
                if (clone.current.kind == .o) {
                    self.max_depth += 0.5;
                    break;
                } else {
                    self.max_depth += 0.5 / 4.0;
                }
            }
        }
    }
    // TODO: Cache
    // Remove excess cache
    // if (CachedValues.Count < 200000) CachedValues.Clear();
    // if (CachedStateValues.Count < 5000000) CachedStateValues.Clear();

    // Check if PC found
    //

    // Adjust movetresh
    self.end_search = true;
    const time_remaining: f32 = @floatFromInt(@as(i128, @intCast(self.think_nanos)) - std.time.nanoTimestamp());
    const time_remaining_rel = time_remaining / @as(f32, @floatFromInt(self.think_nanos));
    if (time_remaining_rel > 0) {
        self.move_tresh *= std.math.pow(f32, MOVE_MUL, time_remaining_rel * (std.math.e - time_remaining_rel) / (1 + std.math.e));
    } else {
        self.move_tresh *= std.math.pow(f32, MOVE_MUL, self.max_depth - MOVE_TARGET);
    }
    self.move_tresh = @min(@max(self.move_tresh, MOVE_TRESH_MIN), MOVE_TRESH_MAX);

    return best_placement;
}

fn search(self: *Self, game: GameState, depth: u32, cleared: u32, attack: f32, prev_output: [2]f32) f32 {
    if (self.end_search or std.time.nanoTimestamp() - self.start_time > self.think_nanos) {
        self.end_search = true;
        return -std.math.inf(f32);
    }

    // Max depth reached; Stop search here
    if (depth == 0) {
        self.node_count += 1;

        // TODO: Cache
        // ulong hash = HashState(cleared, attack, prev_output[1]);
        // if (CachedStateValues.ContainsKey(hash))
        //     return CachedStateValues[hash];

        const features = getFeatures(game.playfield, self.network.inputs_used, cleared, attack, prev_output[1]);
        const score = self.network.predict(features)[0];
        // TODO: Cache
        // CachedStateValues.Add(hash, score);
        return score;
    }

    // TODO: Cache
    // ulong hash = HashBoard(current, _hold, nexti, depth, cleared, attack, intent);
    // if (CachedValues.ContainsKey(hash)) return CachedValues[hash];

    const discount = DISCOUNTS[self.current_depth - depth];
    const output = self.network.predict(getFeatures(
        game.playfield,
        self.network.inputs_used,
        cleared,
        attack,
        prev_output[1],
    ));
    // Stop search here if the move is not good enough
    if (output[0] - prev_output[0] < self.move_tresh) {
        // TODO: Cache
        // ulong statehash = HashState(cleared, attack, intent);
        // if (!CachedStateValues.ContainsKey(statehash))
        //     CachedStateValues.Add(statehash, output[0]);
        return output[0];
    }

    var score = -std.math.inf(f32);
    for (0..2) |i| {
        var clone = game;
        if (i == 1) {
            // Don't hold if it's the same piece
            if (clone.current.kind == clone.hold_kind) {
                break;
            }

            // TODO: Cache
            // if (CachedValues.ContainsKey(hash))
            //     return CachedValues[hash];

            clone.hold();
        }

        // Check all landing spots
        for ([_]Facing{ Facing.up, Facing.right, Facing.down, Facing.left }) |facing| {
            clone.current.facing = facing;
            var x = clone.current.minX();
            const max_x = clone.current.maxX();
            while (x <= max_x) : (x += 1) {
                if (self.end_search) {
                    break;
                }

                clone.current.facing = facing;
                clone.pos = .{ .x = x, .y = clone.current.kind.startPos().y };
                _ = clone.dropToGround();
                const dropped_y = clone.pos.y;

                // S and Z pieces are the same when rotated 180 degrees, so we only check
                // the up and right orientations
                if ((clone.current.kind != .s and clone.current.kind != .z) or
                    (facing == .up or facing == .right))
                {
                    // Place piece
                    var new_state = clone;
                    const info = new_state.lockCurrent(-1);
                    new_state.nextPiece();
                    // Check if better
                    const new_attack: f32 = @floatFromInt(self.attack_table.getAttack(
                        info,
                        new_state.b2b,
                        new_state.combo,
                    ));
                    score = @max(
                        score,
                        search(
                            self,
                            new_state,
                            depth - 1,
                            cleared + info.cleared,
                            attack + discount * new_attack,
                            output,
                        ),
                    );
                }

                // Only try to spin left/right facing pieces into place, except O pieces
                // which have no kicks
                if ((facing == .left or facing == .right) and clone.current.kind != .o) {
                    // Try to spin in either direction
                    for (0..2) |r| {
                        const rotation = switch (r) {
                            0 => Rotation.quarter_cw,
                            1 => Rotation.quarter_ccw,
                            else => unreachable,
                        };
                        clone.pos = .{ .x = x, .y = dropped_y };
                        clone.current.facing = facing;

                        // Try to spin (at most twice)
                        for (0..2) |_| {
                            const kick = clone.rotate(rotation);
                            if (kick == -1 or !clone.onGround()) {
                                break;
                            }

                            // Place piece
                            var new_state = clone;
                            const info = new_state.lockCurrent(kick);
                            new_state.nextPiece();
                            // Check if better
                            const new_attack: f32 = @floatFromInt(self.attack_table.getAttack(
                                info,
                                new_state.b2b,
                                new_state.combo,
                            ));
                            score = @max(score, search(
                                self,
                                new_state,
                                depth - 1,
                                cleared + info.cleared,
                                attack + discount * new_attack,
                                output,
                            ));

                            // Only try to spin T pieces twice (for TSTs)
                            if (clone.current.kind != .t) {
                                break;
                            }
                        }
                    }
                }
            }

            // O pieces can't be rotated
            if (clone.current.kind == .o) {
                break;
            }
        }
    }

    // TODO: Cache
    // CachedValues.Add(hash, score);
    return score;
}

// TODO: Optimise with SIMD
fn getFeatures(playfield: BoardMask, inputs_used: [5]bool, cleared: u32, attack: f32, intent: f32) [8]f32 {
    // Find highest block in each column
    // Heights start from 0
    var heights: [10]i32 = undefined;
    var highest: u32 = 0;
    for (0..10) |x| {
        var height: u32 = LEGACY_HEIGHT;
        const col_mask = @as(u16, 1) << @intCast(10 - x);
        while (height > 0) {
            height -= 1;
            if ((playfield.rows[height] & col_mask) != 0) {
                height += 1;
                break;
            }
        }
        heights[9 - x] = @intCast(height);
        highest = @max(highest, height);
    }

    // Standard height (sqrt of sum of squares of heights)
    const std_h = if (inputs_used[0]) blk: {
        var sqr_sum: i32 = 0;
        for (heights) |h| {
            sqr_sum += h * h;
        }
        break :blk @sqrt(@as(f32, @floatFromInt(sqr_sum)));
    } else undefined;

    // Caves (empty cells with an overhang)
    const caves: f32 = if (inputs_used[1]) blk: {
        const aug_heights = inner: {
            var aug_h: [10]i32 = undefined;
            aug_h[0] = @min(heights[0] - 2, heights[1]);
            for (1..9) |x| {
                aug_h[x] = @min(heights[x] - 2, @max(heights[x - 1], heights[x + 1]));
            }
            aug_h[9] = @min(heights[9] - 2, heights[8]);
            // NOTE: Uncomment this line to restore the bug in the original code
            // aug_h[9] = @min(heights[9] - 2, heights[8] - 1);
            break :inner aug_h;
        };

        var caves: i32 = 0;
        for (0..@max(1, highest) - 1) |y| {
            var covered = ~playfield.rows[y] & playfield.rows[y + 1];
            covered >>= 1; // Remove padding
            // Iterate through set bits
            while (covered != 0) : (covered &= covered - 1) {
                const x = @ctz(covered);
                if (y <= aug_heights[x]) {
                    // Caves deeper down get larger values
                    caves += heights[x] - @as(i32, @intCast(y));
                }
            }
        }

        break :blk @floatFromInt(caves);
    } else undefined;

    // Pillars (sum of min differences in heights)
    const pillars: f32 = if (inputs_used[2]) blk: {
        var pillars: i32 = 0;
        for (0..10) |x| {
            // Columns at the sides map to 0 if they are taller
            var diff: i32 = switch (x) {
                // NOTE: Uncomment this line to restore the bug in the original code
                // 0 => @min(0, heights[1] - heights[0]),
                0 => @max(0, heights[1] - heights[0]),
                1...8 => @intCast(@min(@abs(heights[x - 1] - heights[x]), @abs(heights[x + 1] - heights[x]))),
                9 => @max(0, heights[8] - heights[9]),
                else => unreachable,
            };
            // Exaggerate large differences
            if (diff > 2) {
                diff *= diff;
            }
            pillars += diff;
        }
        break :blk @floatFromInt(pillars);
    } else undefined;

    // Row trasitions
    const row_trans: f32 = if (inputs_used[3]) blk: {
        var row_trans: u32 = 0;
        for (0..highest) |y| {
            const row = playfield.rows[y];
            const trasitions = (row ^ (row << 1)) & 0b00000_111111111_00;
            row_trans += @popCount(trasitions);
        }
        break :blk @floatFromInt(row_trans);
    } else undefined;

    // Column trasitions
    const col_trans: f32 = if (inputs_used[4]) blk: {
        var col_trans: u32 = @popCount(playfield.rows[LEGACY_HEIGHT - 1] & ~BoardMask.EMPTY_ROW);
        for (0..LEGACY_HEIGHT - 1) |y| {
            col_trans += @popCount(playfield.rows[y] ^ playfield.rows[y + 1]);
        }
        break :blk @floatFromInt(col_trans);
    } else undefined;

    return .{ std_h, caves, pillars, row_trans, col_trans, attack, @floatFromInt(cleared), intent };
}

test getFeatures {
    const features = getFeatures(
        BoardMask{
            .rows = .{
                0b11111_1111111111_1,
                0b11111_0010000001_1,
                0b11111_1000000001_1,
                0b11111_0011010001_1,
                0b11111_0000100000_1,
                0b11111_0000000100_1,
            } ++ [_]u16{BoardMask.EMPTY_ROW} ** 34,
        },
        [_]bool{true} ** 5,
        1,
        2,
        0.9,
    );
    try expect(features[0] == 11.7046995);
    try expect(features[1] == 10);
    try expect(features[2] == 47);
    try expect(features[3] == 14);
    try expect(features[4] == 22);
    try expect(features[5] == 2);
    try expect(features[6] == 1);
    try expect(features[7] == 0.9);
}
