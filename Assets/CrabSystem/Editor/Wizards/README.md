# CrabSystem wizards — staged

First two. Everything lives in `Editor/`, so none of it compiles into a player build, and nothing
outside this folder is modified.

| Menu | What it does |
|---|---|
| **Tools → Crab → Wizards → Weapon…** | Creates a weapon's whole data chain in one pass. |
| **Tools → Crab → Wizards → Weapon Asset Report** | Inventory of every weapon-side asset, who references it, and what is already broken. Never deletes. |

`CrabWizardGUI.cs` is the shared chrome — title, sections, a validation box, folder creation, id
and asset-name conversion. The next wizard should use it rather than growing its own HelpBox soup.

## The weapon wizard

One screen, three assets:

```
Wpn_<Name>.asset          WeaponData     — type, category, damage dice, flat bonus
<Name>_BaseType.asset     ItemBaseType   — optional; block / parry / two-handed
Item_<Name>.asset         ItemDefinition — ties the above to a subtype, and so to a slot
```

It refuses to write until the equip chain resolves:

```
ItemDefinition.subType → ItemSubType.equipmentSlot → EquipmentSlotDefinition.slotId
```

A break anywhere in there produces an asset that looks authored, loads without complaint, and
simply never equips — which is worth blocking rather than warning about. Duplicate `itemId` is
also an error, not a warning: `ItemManager` loads by id, so two assets sharing one means one is
permanently unreachable.

Icon and equipped prefab are warnings, not errors — a weapon without them is a legitimate
work in progress.

### The hitbox check

`WeaponHitboxCheck.cs`, shared by both windows. A weapon with no `WeaponHitbox` equips, appears in
hand and plays the whole swing, then passes through everything — and **nothing logs**, because
nothing is wrong. `AbilitySystem` asks for hitboxes under the entity, finds none, enables none.

Three things it warns about:

- **No `WeaponHitbox` on the prefab at all.** The silent case above.
- **A hitbox with no collider.** `Awake` falls back to `GetComponent<Collider>()` when the
  serialized field is empty, so either counts — the check reads the private field through
  `SerializedObject` rather than guessing. Without one it logs an error at runtime and never opens.
- **A tag no ability names.** Hitboxes are opened by `AbilityDefinition.hitboxTags`, matched
  case-insensitively; an ability naming *no* tags opens everything. So if every ability names
  specific tags and none of them match this weapon's, the hitbox can never open.

The identity section also prints the prefab's hitbox tags as you assign it, so the common case —
"did I pick the mesh with the hitbox or the one without" — is answered without leaving the window.

The damage row shows min / max / average as you type, and the d4–d12 buttons are there because
picking a die is the one thing you do on every single weapon.

### Defaults it fills for you

- `itemId` tracks the display name until you edit it, then stops fighting you.
- Grid size follows the slot: 1×3 for `mainwep`, 1×2 for `offwep`.
- Tags get the slot, the weapon type and the rarity appended to whatever you typed.
- `category` comes from `subType.parentCategory`, never typed by hand.

## Where assets go

`Assets/Database/Resources/ItemDatabase/<subfolder>` — default `Weapons/Bladed`, to match what is
already there.

⚠ **The existing bulk generator writes somewhere else.** `CrabSystem/Editor/ItemDefinitionGenerator.cs`
has `OutputRoot = "Assets/CrabSystem/ItemInventoryEquipment/Inventory/Items/Resources/ItemDatabase"`,
and that folder **does not exist in the project**. Running it would create a second database root.
It still works — `Resources.LoadAll` matches on the last path segment, so both would load — but the
items would be split across two trees. Worth fixing that constant to the real root.

## The asset report

Scan walks every `ItemDefinition` with weapon data or a weapon slot, every `WeaponData`, and every
`ItemBaseType`, then builds a reverse dependency map over all assets, prefabs, scenes and
controllers to work out who points at what.

Per row it flags:

- unreferenced assets — nothing in the project points at them
- missing icon, equipped prefab, category or subtype
- a weapon-slot item with no `weaponData`
- duplicate `itemId` across assets
- **stale serialized fields** — keys in the `.asset` file that no longer exist on the class

That last one is why the report exists. `Katana.asset` still carries `attackSpeed`, `reach`,
`lightAttackStamina`, `heavyAttackStamina`, `blockStamina`, `canBlock`, `canParry`, `hasCombos`,
`maxComboCount`, `weaponModel`, `weaponSocket`, `attackEffect` and `attackSounds` — none of which
are on `WeaponData` any more. Unity drops them silently on load, so the asset looks fine in the
inspector while the data is gone. The same pattern bit `ModelSocketProvider` on
`HumanM_Model 1.prefab`.

**Select orphans** selects every unreferenced asset so you can delete them from the Project
window, where Unity's own checks still run. The tool deliberately does not delete.

## What the current WeaponData actually holds

Worth knowing before authoring, because the old assets imply otherwise:

| Field | |
|---|---|
| `weaponName`, `weaponType`, `category` | |
| `damageDice`, `flatBonus` | the whole damage model — `RollDamage()` is dice + bonus |
| `isNaturalWeapon`, `preferredSocketName` | claws and teeth |

Attack speed, reach, stamina costs and combo counts are **not** on `WeaponData`. Block and parry
live on `ItemBaseType`. If those need to come back, that is a data-model decision, not something
to work around in a wizard.

## Next wizards

`CrabWizardGUI` is the seam. An armour wizard is the same shape with a different slot list and no
`WeaponData`; an ability wizard is a bigger job and the `Item_Database` doc argues deliberately
against generating abilities from thin input.
