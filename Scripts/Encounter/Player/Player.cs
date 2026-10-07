using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class Player : Resource
{
    [Export] public string name = "Mattmac53";
    [Export] public Character character;

    [Export] public int strength;
    [Export] public int dexterity;
    [Export] public int intelligence;
    [Export] public int faith;

    // p12: every weapon not in a hand is in the backup slot, the only slot that holds more
    // than one, and a character carries at most MAX_WEAPONS weapons in all.
    [Export] public string[] backupIds = new string[0];
    // Read once from a save made when the backup slot held a single weapon, then moved into
    // backupIds. Nothing writes it any more.
    [Export] public string backupSlotId = "";
    [Export] public string leftHandId = "";
    [Export] public string rightHandId = "";
    [Export] public string armourId = "";

    // Two per backup weapon, in backupIds' order: weapon i owns [2i] and [2i + 1], so its
    // upgrades travel with it as it moves between the backup slot and a hand.
    [Export] public string[] backupUpgradeIds = new string[0];
    [Export] public string[] leftHandUpgradeIds = new string[0];
    [Export] public string[] rightHandUpgradeIds = new string[0];
    [Export] public string[] armourUpgradeIds = new string[0];

    // The character board's tokens (p19): each is spent once and flipped, and only a
    // bonfire rest turns them back. Exported so a spent token stays spent across encounters
    // and saves; unlike the endurance bar, nothing but a rest clears them.
    [Export] public bool estusUsed;
    [Export] public bool heroicUsed;
    [Export] public bool luckUsed;
    // Divine Blessing (a ring, not a board token) is spent the same way: once per rest.
    [Export] public bool divineBlessingUsed;

    // Encounter-scoped and deliberately NOT exported: the bar clears on victory (p19),
    // so it must never be written into the saved character. PlayerToken drives it.
    public Endurance endurance { get; private set; } = new Endurance();

    // Conditions live here rather than on the board token for the same reason the endurance
    // bar does: the portrait pane has a Player, not a PlayerToken, and it is where the
    // character's conditions are now shown.
    public readonly HashSet<EncounterManager.StatusEffect> conditions = new HashSet<EncounterManager.StatusEffect>();

    public Player() { }

    public Player(string name, Character character) {
        this.name = name;
        this.character = character;

        this.strength = character.strengthTiers[0];
        this.dexterity = character.dexterityTiers[0];
        this.intelligence = character.intelligenceTiers[0];
        this.faith = character.faithTiers[0];
    }

    // Equipment accessors via the GameManager owned pool.
    public void RefreshTokens() {
        estusUsed = false;
        heroicUsed = false;
        luckUsed = false;
        divineBlessingUsed = false;
    }

    public const int MAX_WEAPONS = 3;
    public const int UPGRADES_PER_ITEM = 2;

    // Which backup weapon the equipment cross and the loadout are showing. A view, not saved;
    // one past the last is the empty place a new backup weapon goes, where there is room.
    public int shownBackup;

    // The backup weapon on show: what the cross's top slot and the loadout's Backup card mean.
    public Weapon GetBackupSlot() {
        string[] ids = BackupIds();
        return shownBackup >= 0 && shownBackup < ids.Length ? GameManager.GetInstance(ids[shownBackup]) as Weapon : null;
    }

    public List<Weapon> GetBackups() {
        List<Weapon> weapons = new List<Weapon>();
        foreach (string id in BackupIds()) {
            if (GameManager.GetInstance(id) is Weapon weapon) weapons.Add(weapon);
        }
        return weapons;
    }

    public string[] BackupIds() {
        if (!string.IsNullOrEmpty(backupSlotId)) {
            backupIds = Append(backupIds, backupSlotId);
            backupSlotId = "";
        }
        return backupIds ??= new string[0];
    }

    public static string[] OneBackup(string id) => string.IsNullOrEmpty(id) ? new string[0] : new[] { id };

    public int WeaponCount() =>
        BackupIds().Length + (string.IsNullOrEmpty(leftHandId) ? 0 : 1) + (string.IsNullOrEmpty(rightHandId) ? 0 : 1);

    public bool HasRoomForWeapon() => WeaponCount() < MAX_WEAPONS;

    // How many places the backup slot steps through; with `includeEmpty` (the loadout, where a
    // weapon can be added) the empty place after them counts while there is room for another.
    public int BackupStops(bool includeEmpty) => BackupIds().Length + (includeEmpty && HasRoomForWeapon() ? 1 : 0);

    public void CycleBackup(int direction, bool includeEmpty) {
        int stops = BackupStops(includeEmpty);
        shownBackup = stops == 0 ? 0 : ((shownBackup + direction) % stops + stops) % stops;
    }

    // Back onto a weapon when the place on show is the empty one and this view has none.
    public void ClampShownBackup(bool includeEmpty) {
        int stops = BackupStops(includeEmpty);
        shownBackup = stops == 0 ? 0 : Mathf.Clamp(shownBackup, 0, stops - 1);
    }

    // The loadout's Backup card: replaces the weapon on show, removes it (an empty id), or
    // adds one at the empty place.
    public void SetShownBackup(string id) {
        string[] ids = BackupIds();
        if (shownBackup >= 0 && shownBackup < ids.Length) {
            if (string.IsNullOrEmpty(id)) RemoveBackupAt(shownBackup);
            else backupIds[shownBackup] = id;
        } else if (!string.IsNullOrEmpty(id)) {
            InsertBackup(ids.Length, id, null);
            shownBackup = backupIds.Length - 1;
        }
    }

    public string BackupUpgradeId(int slot) {
        int at = shownBackup * UPGRADES_PER_ITEM + slot;
        return backupUpgradeIds != null && at >= 0 && at < backupUpgradeIds.Length ? backupUpgradeIds[at] : "";
    }

    public void SetBackupUpgrade(int slot, string id) {
        if (shownBackup < 0 || shownBackup >= BackupIds().Length) return;
        backupUpgradeIds = Fit(backupUpgradeIds, BackupIds().Length * UPGRADES_PER_ITEM);
        backupUpgradeIds[shownBackup * UPGRADES_PER_ITEM + slot] = id;
    }

    public string[] ShownBackupUpgrades() => new[] { BackupUpgradeId(0), BackupUpgradeId(1) };

    // p22: a backup weapon swaps into a hand, and whatever that hand held goes into the backup
    // slot in its place. p12: a two-hander needs the other hand empty, so what is there goes
    // to the backup slot as well; a two-hander already held is put away when its spare hand
    // is filled. The weapon count never changes, so the three-weapon limit holds.
    public void SwapBackupIntoHand(int index, bool leftHand) {
        string[] ids = BackupIds();
        if (index < 0 || index >= ids.Length) return;

        string incoming = ids[index];
        string[] incomingUpgrades = UpgradesOfBackup(index);
        RemoveBackupAt(index);

        string handId = leftHand ? leftHandId : rightHandId;
        string[] handUpgrades = leftHand ? leftHandUpgradeIds : rightHandUpgradeIds;
        string otherId = leftHand ? rightHandId : leftHandId;
        string[] otherUpgrades = leftHand ? rightHandUpgradeIds : leftHandUpgradeIds;

        if (!string.IsNullOrEmpty(handId)) InsertBackup(index, handId, handUpgrades);

        Weapon incomingWeapon = GameManager.GetInstance(incoming) as Weapon;
        Weapon otherWeapon = GameManager.GetInstance(otherId) as Weapon;
        // A shield that sits beside a two-hander may stay (or come in) beside one.
        bool clash = (incomingWeapon?.numHands > 1 && !(otherWeapon?.SitsBesideTwoHander ?? false))
            || (otherWeapon?.numHands > 1 && !(incomingWeapon?.SitsBesideTwoHander ?? false));
        if (!string.IsNullOrEmpty(otherId) && clash) {
            InsertBackup(BackupIds().Length, otherId, otherUpgrades);
            otherId = "";
            otherUpgrades = new string[0];
        }

        if (leftHand) {
            leftHandId = incoming;
            leftHandUpgradeIds = incomingUpgrades;
            rightHandId = otherId;
            rightHandUpgradeIds = otherUpgrades;
        } else {
            rightHandId = incoming;
            rightHandUpgradeIds = incomingUpgrades;
            leftHandId = otherId;
            leftHandUpgradeIds = otherUpgrades;
        }
        shownBackup = Mathf.Clamp(index, 0, Mathf.Max(0, BackupIds().Length - 1));
    }

    private string[] UpgradesOfBackup(int index) {
        string[] upgrades = new string[UPGRADES_PER_ITEM];
        for (int k = 0; k < UPGRADES_PER_ITEM; k++) {
            int at = index * UPGRADES_PER_ITEM + k;
            upgrades[k] = backupUpgradeIds != null && at < backupUpgradeIds.Length ? backupUpgradeIds[at] ?? "" : "";
        }
        return upgrades;
    }

    private void InsertBackup(int index, string id, string[] upgrades) {
        List<string> ids = new List<string>(BackupIds());
        List<string> ups = new List<string>(Fit(backupUpgradeIds, ids.Count * UPGRADES_PER_ITEM));
        ids.Insert(index, id);
        ups.InsertRange(index * UPGRADES_PER_ITEM, Fit(upgrades, UPGRADES_PER_ITEM));
        backupIds = ids.ToArray();
        backupUpgradeIds = ups.ToArray();
    }

    private void RemoveBackupAt(int index) {
        List<string> ids = new List<string>(BackupIds());
        List<string> ups = new List<string>(Fit(backupUpgradeIds, ids.Count * UPGRADES_PER_ITEM));
        ids.RemoveAt(index);
        ups.RemoveRange(index * UPGRADES_PER_ITEM, UPGRADES_PER_ITEM);
        backupIds = ids.ToArray();
        backupUpgradeIds = ups.ToArray();
        if (shownBackup > backupIds.Length) shownBackup = backupIds.Length;
    }

    private static string[] Fit(string[] array, int length) {
        string[] fitted = new string[length];
        for (int i = 0; i < length; i++) fitted[i] = array != null && i < array.Length ? array[i] ?? "" : "";
        return fitted;
    }

    public static string[] Append(string[] array, string id) {
        List<string> list = new List<string>(array ?? new string[0]) { id };
        return list.ToArray();
    }
    public Weapon GetLeftHand() => GameManager.GetInstance(leftHandId) as Weapon;
    public Weapon GetRightHand() => GameManager.GetInstance(rightHandId) as Weapon;
    public Armour GetArmour() => GameManager.GetInstance(armourId) as Armour;

    // The rings in the armour's upgrade slots (p12). A slot the armour does not have holds
    // nothing, so a ring left in it by a change of armour is carried but not worn.
    public List<Ring> GetRings() {
        List<Ring> rings = new List<Ring>();
        int slots = Mathf.Min(GetArmour()?.upgradeSlots ?? 0, armourUpgradeIds?.Length ?? 0);
        for (int i = 0; i < slots; i++) {
            if (GameManager.GetInstance(armourUpgradeIds[i]) is Ring ring) rings.Add(ring);
        }
        return rings;
    }

    public bool HasRing(Ring.Effect effect) => RingCount(effect) > 0;

    // ----- Passive abilities -----

    // The passives of the armour and the weapons in hand (p25: only what is held counts),
    // each with the item it is printed on, for the few that are "this weapon" only.
    public IEnumerable<(EquipmentEffect effect, Equipment item)> Passives() {
        Armour armour = GetArmour();
        if (armour?.passives != null) {
            foreach (EquipmentEffect e in armour.passives) if (e != null) yield return (e, armour);
        }
        foreach (Weapon w in HeldWeapons()) {
            if (w?.passives == null) continue;
            foreach (EquipmentEffect e in w.passives) if (e != null) yield return (e, w);
        }
    }

    public EquipmentEffect Passive(EquipmentEffect.EffectType type, EquipmentEffect.Condition condition = EquipmentEffect.Condition.NONE) {
        foreach ((EquipmentEffect e, Equipment _) in Passives()) {
            if (e.type == type && e.condition == condition) return e;
        }
        return null;
    }

    public bool HasPassive(EquipmentEffect.EffectType type, EquipmentEffect.Condition condition = EquipmentEffect.Condition.NONE) =>
        Passive(type, condition) != null;

    // Black Knight Armour: "reduce the stat requirements of your armour upgrades by 2".
    public int RingRequirementCut() => Passive(EquipmentEffect.EffectType.UPGRADE_REQ_MOD)?.magnitude ?? 0;

    // Party defence from a spell (Magic Barrier, Sacred Oath, Sunlight Straight Sword): extra
    // Block / Resist dice until the next character activation begins. Encounter-scoped, so
    // not exported, like the endurance bar.
    public readonly List<(Dice die, EquipmentEffect.DefenseKind kind)> defenceBuffs = new List<(Dice, EquipmentEffect.DefenseKind)>();

    // The gems in a held weapon's upgrade slots (p12), only as many as it has slots. A weapon
    // in the backup slot attacks with nothing, so its gems do nothing there.
    public List<Gem> GemsOn(Weapon weapon) {
        List<Gem> gems = new List<Gem>();
        if (weapon == null || string.IsNullOrEmpty(weapon.id)) return gems;
        string[] ups = weapon.id == leftHandId ? leftHandUpgradeIds : weapon.id == rightHandId ? rightHandUpgradeIds : null;
        int slots = Mathf.Min(weapon.upgradeSlots, ups?.Length ?? 0);
        for (int i = 0; i < slots; i++) {
            if (GameManager.GetInstance(ups[i]) is Gem gem) gems.Add(gem);
        }
        return gems;
    }

    public int RingCount(Ring.Effect effect) {
        int count = 0;
        foreach (Ring ring in GetRings()) if (ring.effect == effect) count++;
        return count;
    }

    public int GetMaxEndurance() => Endurance.BOXES;
    public int GetRemainingHP() => endurance.healthRemaining;

    // Stamina and Health share the bar, so what is spendable is simply what is uncovered.
    public int GetCurrentStamina() => endurance.free;

    // Character level = 1 + total stat investments: how many tiers each attribute has
    // climbed above its starting tier, summed across all four. A freshly created
    // character is Level 1.
    public int GetLevel() {
        if (character == null) return 1;
        return 1
             + TierIndex(strength, character.strengthTiers)
             + TierIndex(dexterity, character.dexterityTiers)
             + TierIndex(intelligence, character.intelligenceTiers)
             + TierIndex(faith, character.faithTiers);
    }

    private static int TierIndex(int value, int[] tiers) {
        if (tiers == null) return 0;
        int idx = 0;
        for (int i = 0; i < tiers.Length; i++) if (value >= tiers[i]) idx = i;
        return idx;
    }

    // The weapons in hand. Only these and the armour count (p25: "the character's armour and
    // hand slots"): a weapon in the backup slot is carried, not used, so its dice, dodge,
    // immunities and passives do nothing until it is swapped into a hand.
    private Weapon[] HeldWeapons() => new[] { GetLeftHand(), GetRightHand() };

    // Status effects the wearer is immune to, gathered from the armour and both hands.
    public List<EncounterManager.StatusEffect> GetImmunities() {
        var set = new HashSet<EncounterManager.StatusEffect>();
        void Collect(Godot.Collections.Array<EncounterManager.StatusEffect> arr) {
            if (arr == null) return;
            foreach (var s in arr)
                if (s != EncounterManager.StatusEffect.NONE) set.Add(s);
        }
        Collect(GetArmour()?.immunities);
        foreach (Weapon w in HeldWeapons())
            Collect(w?.immunities);
        return new List<EncounterManager.StatusEffect>(set);
    }

    public bool IsPushImmune() {
        if (GetArmour()?.pushImmune ?? false) return true;
        foreach (Weapon w in HeldWeapons())
            if (w?.pushImmune ?? false) return true;
        return false;
    }

    // Passive EquipmentEffects contributed by all equipped gear (armour + weapons/shields).
    public List<EquipmentEffect> GetEquippedPassives() {
        var list = new List<EquipmentEffect>();
        void Collect(Godot.Collections.Array<EquipmentEffect> arr) {
            if (arr == null) return;
            foreach (var e in arr)
                if (e != null && e.type != EquipmentEffect.EffectType.NONE) list.Add(e);
        }
        Collect(GetArmour()?.passives);
        foreach (Weapon w in HeldWeapons())
            Collect(w?.passives);
        return list;
    }

    // Statuses this character's weapon attacks inflict on hit.
    public List<EncounterManager.StatusEffect> GetInflictedStatuses() {
        var set = new HashSet<EncounterManager.StatusEffect>();
        foreach (Weapon w in HeldWeapons()) {
            if (w?.attacks == null) continue;
            foreach (PlayerMove m in w.attacks)
                if (m != null && m.statusEffect != EncounterManager.StatusEffect.NONE)
                    set.Add(m.statusEffect);
        }
        return new List<EncounterManager.StatusEffect>(set);
    }

    // ----- Stat summary, mirrors Character.* but reads currently-equipped instances -----

    public int GetDodge() {
        int dodge = GetArmour()?.dodgeAbility ?? 0;
        Weapon[] weapons = HeldWeapons();
        foreach (Weapon w in weapons) dodge += w?.dodgeAbility ?? 0;
        return dodge;
    }

    public (int min, int max) GetBestPhysicalDamage() => GetBestAttackRange(false);
    public (int min, int max) GetBestMagicDamage() => GetBestAttackRange(true);
    public (int min, int max) GetPhysicalDefense() => GetDefenseRange(false);
    public (int min, int max) GetMagicDefense() => GetDefenseRange(true);

    // The attack with the highest ceiling among everything held, and the dice it rolls: the
    // summary shows both the pool and its range, so they have to come from one place.
    public (List<Dice> dice, int modifier) GetBestAttackPool(bool magic) {
        List<Dice> best = new List<Dice>();
        int bestModifier = 0, bestMax = 0;
        Weapon[] weapons = HeldWeapons();
        foreach (Weapon w in weapons) {
            if (w?.attacks == null) continue;
            foreach (PlayerMove move in w.attacks) {
                if (move?.damage == null || move.isMagic != magic) continue;
                List<Dice> dice = new List<Dice>();
                foreach (Dice d in move.damage) if (d != null) dice.Add(d);
                int max = RangeOf(dice, move.modifier).max;
                if (max <= bestMax) continue;
                bestMax = max;
                best = dice;
                bestModifier = move.modifier;
            }
        }
        return (best, bestModifier);
    }

    // Block or Resist gathered from every equipped piece, as CombatResolver rolls it.
    public (List<Dice> dice, int modifier) GetDefensePool(bool magic) {
        List<Dice> pool = new List<Dice>();
        void Add(Godot.Collections.Array<Dice> dice) {
            if (dice == null) return;
            foreach (Dice d in dice) if (d != null) pool.Add(d);
        }
        Armour armour = GetArmour();
        Add(magic ? armour?.magicDefense : armour?.physicalDefense);
        foreach (Weapon w in HeldWeapons()) {
            Add(magic ? w?.magicDefense : w?.physicalDefense);
        }
        AddRingDefence(pool, magic);
        foreach ((Dice die, EquipmentEffect.DefenseKind kind) in defenceBuffs) {
            if (die != null && (kind == EquipmentEffect.DefenseKind.BOTH || (kind == EquipmentEffect.DefenseKind.MAGIC) == magic)) pool.Add(die);
        }
        int modifier = magic ? armour?.magicDefenseModifier ?? 0 : armour?.physicalDefenseModifier ?? 0;
        return (pool, modifier);
    }

    // V2 failed dodge: the armour slot's Block or Resist on its own.
    public (List<Dice> dice, int modifier) GetArmourPool(bool magic) {
        Armour armour = GetArmour();
        List<Dice> pool = new List<Dice>();
        Godot.Collections.Array<Dice> dice = magic ? armour?.magicDefense : armour?.physicalDefense;
        if (dice != null) foreach (Dice d in dice) if (d != null) pool.Add(d);
        AddRingDefence(pool, magic);
        int modifier = magic ? armour?.magicDefenseModifier ?? 0 : armour?.physicalDefenseModifier ?? 0;
        return (pool, modifier);
    }

    // What the armour's upgrades add, so the V2 failed dodge's armour-only roll has it too.
    // Magic Stoneplate Ring: +1 black die to Resist. Steel Armour: +1 black die to Block and
    // Resist while it has an upgrade equipped.
    private void AddRingDefence(List<Dice> pool, bool magic) {
        if (Heroic.BlackDie == null) return;
        if (magic) for (int i = 0; i < RingCount(Ring.Effect.MAGIC_STONEPLATE); i++) pool.Add(Heroic.BlackDie);
        EquipmentEffect steel = GetArmour()?.passives == null ? null
            : System.Linq.Enumerable.FirstOrDefault(GetArmour().passives, e => e != null && e.type == EquipmentEffect.EffectType.DEFENSE_DICE
                && e.condition == EquipmentEffect.Condition.IF_ARMOUR_UPGRADE_EQUIPPED);
        if (steel != null && GetRings().Count > 0) {
            for (int i = 0; i < Mathf.Max(1, steel.magnitude); i++) pool.Add(Heroic.BlackDie);
        }
    }

    private (int min, int max) GetBestAttackRange(bool magic) {
        var (dice, modifier) = GetBestAttackPool(magic);
        return dice.Count == 0 && modifier == 0 ? (0, 0) : RangeOf(dice, modifier);
    }

    private (int min, int max) GetDefenseRange(bool magic) {
        var (dice, modifier) = GetDefensePool(magic);
        return RangeOf(dice, modifier);
    }

    private static (int min, int max) RangeOf(List<Dice> dice, int modifier) {
        int min = modifier, max = modifier;
        foreach (Dice d in dice) { var (a, b) = FacesRange(d); min += a; max += b; }
        return (Mathf.Max(0, min), max);
    }

    private static (int min, int max) FacesRange(Dice d) {
        int min = int.MaxValue, max = int.MinValue;
        foreach (int face in d.dice) {
            if (face < min) min = face;
            if (face > max) max = face;
        }
        return (min == int.MaxValue ? 0 : min, max == int.MinValue ? 0 : max);
    }

    // public string currentNode;
    // public bool isAggro = false;
    // public EncounterManager.StatusEffect statusEffect = EncounterManager.StatusEffect.NONE;
}
