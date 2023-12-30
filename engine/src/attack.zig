pub const TSpin = enum {
    None,
    Mini,
    Full,
};

pub const ClearInfo = struct {
    b2b: bool,
    cleared: u3,
    pc: bool,
    t_spin: TSpin,
};

pub const AttackTable = struct {
    b2b: []const u8,
    combo: []const u8,
    clears: [5]u8,
    t_spin: [4]u8,
    perfect_clear: [4]u8,

    pub fn getAttack(self: AttackTable, info: ClearInfo, b2b: ?u32, combo: ?u32) u8 {
        var attack = if (info.pc)
            self.perfect_clear[info.cleared - 1]
        else if (info.t_spin == .Full)
            self.t_spin[info.cleared]
        else
            self.clears[info.cleared];

        if (info.b2b and b2b) |chain_len| {
            const idx = @min(chain_len, self.b2b.len - 1);
            attack += self.b2b[idx];
        }

        if (combo) |combo_len| {
            const idx = @min(combo_len, self.combo.len - 1);
            attack += self.combo[idx];
        }

        return attack;
    }
};
