using UnityEngine;

// A monster's loot and kill XP (Loot_Build.md §4-5b). On death it rolls its LootTable into its own inventory,
// so the corpse is looted through the normal interact → loot window path, and pays the killer its XP. While
// anything is left, a loot glow plays over the body and the corpse can be interacted with. Only the killer may
// loot for ownerOnlySeconds; the corpse despawns corpseSeconds after death, looted or not.
//
// A character without this module never despawns and pays nothing (players, the Mirror).

namespace CrabSystem
{
    public class LootModule : MonoBehaviour, IBrainModule, IInteractionGate
    {
        [SerializeField] private LootTable table;
        [Tooltip("Paid to the killer on the kill.")]
        [SerializeField, Min(0)] private int xp;

        [Header("Corpse")]
        [Tooltip("Seconds after death that only the killer may loot.")]
        [SerializeField, Min(0f)] private float ownerOnlySeconds = 60f;
        [Tooltip("Seconds after death until the corpse despawns, looted or not.")]
        [SerializeField, Min(0f)] private float corpseSeconds = 180f;

        [Header("Loot glow")]
        [Tooltip("Plays over the body while it holds loot (Dark Souls style). Empty for none.")]
        [SerializeField] private GameObject lootMarker;
        [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.3f, 0f);

        [Header("Text")]
        [SerializeField] private Color xpColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private Color levelColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private float textScale = 0.6f;
        [SerializeField] private float textHeight = 1.8f;

        private ControllerBrain brain;
        private DamageSystem damage;
        private InventorySystem inventory;
        private InteractionSystem interaction;
        private ControllerBrain owner;
        private GameObject marker;
        private float diedAt;
        private bool dead;

        public bool IsEnabled { get; set; } = true;
        public LootTable Table => table;
        public int Xp => xp;

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
            damage = brain.Damage;
            inventory = brain.Inventory;
            interaction = brain.Interaction;

            if (damage == null) Debug.LogError($"[LootModule] {brain.name} has no DamageSystem, so it never drops loot.", this);
            if (inventory == null) Debug.LogError($"[LootModule] {brain.name} has no InventorySystem to hold its loot.", this);
            if (damage == null || inventory == null) return;

            damage.OnKilledBy += HandleKilled;
            damage.OnRevived += HandleRevived;
            inventory.OnInventoryChanged += HandleInventoryChanged;
            RefreshCorpse();
        }

        void OnDestroy()
        {
            if (damage != null)
            {
                damage.OnKilledBy -= HandleKilled;
                damage.OnRevived -= HandleRevived;
            }
            if (inventory != null) inventory.OnInventoryChanged -= HandleInventoryChanged;
        }

        public void UpdateModule()
        {
            if (!dead) return;
            if (brain.Clock.Now - diedAt < Mathf.Max(corpseSeconds, ownerOnlySeconds)) return;

            Destroy(brain.gameObject);
        }

        public bool Allows(ControllerBrain actor)
        {
            if (!dead) return false;
            if (owner == null || actor == owner) return true;
            return brain.Clock.Now - diedAt >= ownerOnlySeconds;
        }

        private void HandleKilled(ControllerBrain killer)
        {
            dead = true;
            diedAt = brain.Clock.Now;
            owner = killer;

            PayXp(killer);
            DropLoot();
            RefreshCorpse();
        }

        private void HandleRevived()
        {
            dead = false;
            owner = null;
            inventory.ClearInventory();
            RefreshCorpse();
        }

        private void HandleInventoryChanged()
        {
            if (dead) RefreshCorpse();
        }

        private void DropLoot()
        {
            if (table == null) return;

            IInventoryProvider bag = inventory;
            foreach (LootDrop drop in LootRoller.Roll(table))
                if (!bag.AddItem(drop.itemId, drop.quantity))
                    Debug.LogWarning($"[LootModule] {brain.name}: no room for {drop.quantity} {drop.itemId}.", this);
        }

        private void PayXp(ControllerBrain killer)
        {
            if (xp <= 0 || killer == null || killer.RPG == null) return;

            RPGSystem rpg = killer.RPG;
            int levelBefore = rpg.CurrentLevel;
            rpg.AddXP(xp);

            Vector3 above = brain.transform.position + Vector3.up * textHeight;
            DamageNumberManager.Say($"+{xp} XP", above, xpColor, textScale);
            if (rpg.CurrentLevel > levelBefore)
                DamageNumberManager.Say("Level up", killer.transform.position + Vector3.up * textHeight, levelColor, textScale * 1.5f);
        }

        // The corpse is interactable, and glows, only while dead with something left to take.
        private void RefreshCorpse()
        {
            bool hasLoot = dead && inventory.GetItemCount() > 0;
            if (interaction != null) interaction.SetInteractable(hasLoot);
            ShowMarker(hasLoot);
        }

        private void ShowMarker(bool show)
        {
            if (lootMarker == null) return;
            if (marker == null && show)
                marker = Instantiate(lootMarker, brain.transform.position + markerOffset, Quaternion.identity, brain.transform);
            if (marker != null) marker.SetActive(show);
        }
    }
}
