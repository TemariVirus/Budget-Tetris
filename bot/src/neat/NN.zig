const std = @import("std");
const math = std.math;
const json = std.json;
const Allocator = std.mem.Allocator;
const assert = std.debug.assert;

const INPUTS = 8;
const OUTPUTS = 2;
const OUTPUT_OFFSET = INPUTS + 1;
const HIDDEN_OFFSET = OUTPUT_OFFSET + OUTPUTS;

const Self = @This();

const ActivationFn = *const fn (f32) f32;
pub const ActivationType = enum(u8) {
    sigmoid = 0,
    tanh,
    relu,
    leaky_relu,
    isru,
    elu,
    selu,
    gelu,
    softplus,

    pub fn func(self: ActivationType) ActivationFn {
        return switch (self) {
            .sigmoid => Activation.sigmoid,
            .tanh => Activation.tanh,
            .relu => Activation.relu,
            .leaky_relu => Activation.leakyRelu,
            .isru => Activation.isru,
            .elu => Activation.elu,
            .selu => Activation.selu,
            .gelu => Activation.gelu,
            .softplus => Activation.softplus,
        };
    }
};

const Activation = struct {
    pub fn sigmoid(x: f32) f32 {
        return 1.0 / (1.0 + @exp(-x));
    }

    pub fn tanh(x: f32) f32 {
        return math.tanh(x);
    }

    pub fn relu(x: f32) f32 {
        return if (x >= 0) x else 0;
    }

    pub fn leakyRelu(x: f32) f32 {
        return if (x >= 0) x else 0.01 * x;
    }

    pub fn isru(x: f32) f32 {
        return if (x >= 0) x else x / @sqrt(x * x + 1);
    }

    pub fn elu(x: f32) f32 {
        return if (x >= 0) x else @exp(x) - 1;
    }

    pub fn selu(x: f32) f32 {
        return 1.05070098735548 * if (x >= 0) x else 1.670086994173469 * (@exp(x) - 1);
    }

    pub fn gelu(x: f32) f32 {
        return x / (1.0 + @exp(-1.702 * x));
    }

    pub fn softplus(x: f32) f32 {
        return @log(1 + @exp(x));
    }
};

pub const ConnectionJson = struct {
    Enabled: bool,
    Input: u32,
    Output: u32,
    Weight: f32,
};

const NNJson = struct {
    Name: []const u8,
    Connections: []ConnectionJson,
    Activations: []ActivationType,
};

const Node = struct {
    value: f32 = 0,
    activation: ActivationFn,

    pub fn updateValue(self: *Node, nodes: []Node, inputs: []Connection) void {
        self.value = 0;
        for (inputs) |c| {
            self.value += nodes[c.input].value * c.weight;
        }
        self.value = self.activation(self.value);
    }
};

const Connection = struct {
    input: u32,
    weight: f32,
};

fn JaggedArray(comptime T: type) type {
    return struct {
        items: [*]T,
        splits: []u32,

        pub fn init(allocator: Allocator, items: []std.ArrayList(T)) !JaggedArray(T) {
            const splits = try allocator.alloc(u32, items.len + 1);
            splits[0] = 0;
            for (items, 0..) |list, i| {
                splits[i + 1] = @intCast(splits[i] + list.items.len);
            }

            const flat_items = try allocator.alloc(T, splits[splits.len - 1]);
            var i: usize = 0;
            for (items) |list| {
                @memcpy(flat_items[i..][0..list.items.len], list.items);
                i += list.items.len;
            }

            return .{
                .items = flat_items.ptr,
                .splits = splits,
            };
        }

        pub fn get(self: JaggedArray(T), index: usize) []T {
            return self.items[self.splits[index]..self.splits[index + 1]];
        }

        pub fn deinit(self: JaggedArray(T), allocator: Allocator) void {
            allocator.free(self.items[0..self.splits[self.splits.len - 1]]);
            allocator.free(self.splits);
        }
    };
}

name: []const u8,
// Layout: [...inputs, bias, ...outputs, ...hiddens]
nodes: []Node,
connections: JaggedArray(Connection),
inputs_used: [5]bool,

/// Initializes a neural network from a list of connections and activation functions.
pub fn init(allocator: Allocator, name: []const u8, connections: []ConnectionJson, activations: []ActivationType) !Self {
    const node_count = blk: {
        var max: u32 = 0;
        for (connections) |c| {
            max = @max(max, c.Output);
        }
        break :blk max + 1;
    };

    // Only nodes that can be non-zero are useful
    const useful = try scanForwards(allocator, connections, node_count);
    defer allocator.free(useful);
    const used = try scanBackwards(allocator, connections, node_count);
    defer allocator.free(used);

    // All inputs and outputs must be kept
    for (0..HIDDEN_OFFSET) |i| {
        useful[i] = true;
    }
    for (HIDDEN_OFFSET..node_count) |i| {
        useful[i] = useful[i] and used[i];
    }

    // Remove non-useful nodes and re-map indices
    const useful_count = blk: {
        var count: usize = 0;
        for (useful) |u| {
            if (u) {
                count += 1;
            }
        }
        break :blk count;
    };
    const nodes = try allocator.alloc(Node, useful_count);
    const node_map = try allocator.alloc(u32, node_count);
    defer allocator.free(node_map);
    var index: u32 = 0;
    for (0..node_count) |i| {
        if (useful[i]) {
            nodes[index] = Node{ .activation = activations[i].func() };
            node_map[i] = index;
            index += 1;
        }
    }

    var connection_lists = try allocator.alloc(std.ArrayList(Connection), nodes.len);
    for (connection_lists) |*list| {
        list.* = std.ArrayList(Connection).init(allocator);
    }
    defer {
        for (connection_lists) |list| {
            list.deinit();
        }
        allocator.free(connection_lists);
    }

    for (connections) |c| {
        if (!c.Enabled or !useful[c.Input] or !useful[c.Output]) {
            continue;
        }
        try connection_lists[node_map[c.Output]].append(.{ .input = c.Input, .weight = c.Weight });
    }
    const connections_arrs = try JaggedArray(Connection).init(allocator, connection_lists);

    var inputs_used: [5]bool = undefined;
    @memcpy(&inputs_used, used[0..5]);
    return Self{
        .name = try allocator.dupe(u8, name),
        .nodes = nodes,
        .connections = connections_arrs,
        .inputs_used = inputs_used,
    };
}

/// Loads a neural network from a json file.
pub fn load(allocator: Allocator, path: []const u8) !Self {
    const file = try std.fs.cwd().openFile(path, .{});
    var reader = json.Reader(4096, std.fs.File.Reader).init(allocator, file.reader());
    defer reader.deinit();
    const saved = try json.parseFromTokenSource(NNJson, allocator, &reader, .{
        .ignore_unknown_fields = true,
    });
    defer saved.deinit();

    return try init(allocator, saved.value.Name, saved.value.Connections, saved.value.Activations);
}

/// The allcator passed in must be the same allocator used to allocate the NN.
pub fn deinit(self: Self, allocator: Allocator) void {
    allocator.free(self.name);
    allocator.free(self.nodes);
    self.connections.deinit(allocator);
}

/// Returns a mask indicating which nodes are affected the inputs.
fn scanForwards(allocator: Allocator, connections: []ConnectionJson, node_count: u32) ![]bool {
    const visited = try allocator.alloc(bool, node_count);
    // Visit input nodes
    for (0..OUTPUT_OFFSET) |i| {
        if (!visited[i]) {
            scanDownstream(visited, connections, @intCast(i));
        }
    }
    return visited;
}

fn scanDownstream(visited: []bool, connections: []ConnectionJson, i: u32) void {
    visited[i] = true;
    for (connections) |c| {
        if (c.Input != i) {
            continue;
        }
        if (!visited[c.Output] and c.Enabled) {
            scanDownstream(visited, connections, c.Output);
        }
    }
}

/// Returns a mask indicating which nodes affect the outputs.
fn scanBackwards(allocator: Allocator, connections: []ConnectionJson, node_count: u32) ![]bool {
    const visited = try allocator.alloc(bool, node_count);
    // Visit outputs nodes
    for (OUTPUT_OFFSET..HIDDEN_OFFSET) |i| {
        if (!visited[i]) {
            scanUpstream(visited, connections, @intCast(i));
        }
    }
    return visited;
}

fn scanUpstream(visited: []bool, connections: []ConnectionJson, i: u32) void {
    visited[i] = true;
    for (connections) |c| {
        if (c.Output != i) {
            continue;
        }
        if (!visited[c.Input] and c.Enabled) {
            scanUpstream(visited, connections, c.Input);
        }
    }
}

pub fn predict(self: Self, input: [INPUTS]f32) [OUTPUTS]f32 {
    // Set input nodes
    for (0..INPUTS) |i| {
        self.nodes[i].value = input[i];
    }
    self.nodes[INPUTS].value = 1.0; // Bias node

    // Update hidden all nodes
    for (HIDDEN_OFFSET..self.nodes.len) |i| {
        self.nodes[i].updateValue(self.nodes, self.connections.get(i));
    }

    // Update ouput nodes and get output
    var output: [OUTPUTS]f32 = undefined;
    for (0..OUTPUTS) |i| {
        self.nodes[OUTPUT_OFFSET + i].updateValue(self.nodes, self.connections.get(OUTPUT_OFFSET + i));
        output[i] = self.nodes[OUTPUT_OFFSET + i].value;
    }

    return output;
}

test "NN prediction with hidden node" {
    const allocator = std.testing.allocator;

    const nn = try load(allocator, "NNs/Qoshae.json");
    defer nn.deinit(allocator);

    var out = nn.predict([_]f32{ 5.2, 1.0, 3.0, 9.0, 11.0, 5.0, 2.0, -0.97 });
    assert(out[0] == 0.9761649966239929);
    assert(out[1] == 0.9984789490699768);

    out = nn.predict([_]f32{ 2.2, 0.0, 3.0, 5.0, 10.0, 8.0, 4.0, -0.97 });
    assert(out[0] == 0.9988278150558472);
    assert(out[1] == 0.9965899586677551);
}

test "NN prediction with unused hidden node" {
    const allocator = std.testing.allocator;

    const nn = try load(allocator, "NNs/Xesa.json");
    defer nn.deinit(allocator);

    var out = nn.predict([_]f32{ 5.2, 1.0, 3.0, 9.0, 11.0, 5.0, 2.0, -0.97 });
    assert(out[0] == 0.455297589302063);
    assert(out[1] == -0.9720132350921631);

    out = nn.predict([_]f32{ 2.2, 0.0, 3.0, 5.0, 10.0, 8.0, 4.0, -0.97 });
    assert(out[0] == 1.2168807983398438);
    assert(out[1] == -0.9620361924171448);
}
