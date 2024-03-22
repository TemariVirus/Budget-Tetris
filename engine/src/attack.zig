/// Denotes the type of a T-Spin.
pub const TSpin = enum {
    // No T-Spin.
    none,
    // T-Spin Mini.
    mini,
    // T-Spin.
    full,
};

/// Stores information about a clear.
pub const ClearInfo = struct {
    /// Whether the clear has back-to-back bonus.
    b2b: bool,
    /// The number of lines cleared.
    cleared: u3,
    /// Whether the clear was a perfect clear.
    pc: bool,
    /// The type of T-Spin.
    t_spin: TSpin,
};

/// Represents the attack table used in a game.
pub const AttackTable = struct {
    /// Attack to award for back-to-back bonuses. Back-to-back chains longer
    /// than the array length will use the last value.
    b2b: []const u8,
    /// Attack to award for a combo. Combos longer than the array length will
    /// use the last value.
    combo: []const u8,
    /// Attack to award for clearing 0, 1, 2, 3 or 4 lines.
    clears: [5]u8,
    /// Attack to award for clearing 0, 1, 2 or 3 lines with a T-Spin.
    t_spin: [4]u8,
    /// Attack to award for clearing 1, 2, 3 or 4 lines with a perfect clear.
    perfect_clear: [4]u8,

    /// Returns the attack of the specified clear.
    pub fn getAttack(self: AttackTable, info: ClearInfo, b2b: u32, combo: u32) u16 {
        var attack: u16 = if (info.pc)
            self.perfect_clear[info.cleared - 1]
        else if (info.t_spin == .full)
            self.t_spin[info.cleared]
        else
            self.clears[info.cleared];

        if (info.b2b) {
            attack += self.b2b[@min(b2b, self.b2b.len - 1)];
        }

        attack += self.combo[@min(combo, self.combo.len - 1)];
        return attack;
    }
};

pub const TargetMode = enum {
    /// Attacks are sent to no one.
    none,
    /// A random player is targeted.
    random,
    /// A random player is targeted, excluding the sender.
    random_but_self,
    /// All players are targeted.
    all,
    /// All players are targeted, excluding the sender.
    all_but_self,
    /// The sender is targeted.
    self,
};
