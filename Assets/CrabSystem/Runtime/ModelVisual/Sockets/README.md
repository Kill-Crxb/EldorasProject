# Model sockets — staged

`Editor/ModelSocketSetup.cs`. Builds socket transforms on a rig and wires them into that model's
`ModelSocketProvider`. Nothing outside this folder is modified by the script itself; it edits the
model prefab, and `Porphi.prefab` is backed up at
`_Staging/_Backup/Database/Characters/MC/Porphi.prefab.pre-sockets.bak`.

## Run it

**Tools → Crab → Sockets → Set Up Porphi Sockets**
**Tools → Crab → Sockets → Log Porphi Sockets** — read-only dump of what is currently wired,
including null slot assets and null transforms.

Idempotent. Re-running reuses existing socket objects, so offsets you tune by hand survive, and
rewrites the provider's two lists from the tables at the top of the script.

## What it found

The provider was never removed from `Porphi.prefab` — it was there and enabled, with one
`slotSockets` entry (`Slot_MainWeapon`) whose transform was null, and an empty `namedSockets`
list. Every `GetSocket` and `GetNamedSocket` call returned null.

`Database/3d/Humanoid/HumanM_Model 1.prefab` carries the component too, but serialized against an
older version of the script (`weapon`, `shield`, `helmet`, `chest`, `boots`, `feeteffects`,
`backeffects`, `extraSockets`). Unity drops those fields silently, so it is empty as well — there
was no good copy to restore from.

## Sockets are children of bones

Not the bones themselves. A weapon rarely sits at a hand bone's exact pivot and rotation, and a
child can be offset without disturbing the skin. Everything is created at the bone's origin with
no rotation — nudge them in the scene view until props sit right.

## The tables

Slot sockets are **props only**. Armour is normally a skinned mesh bound to the whole rig, so
mapping `bodyarmor` to `UpperChest` would make the chestpiece swing with one bone. Add armour
slots only where the piece really is a rigid prop.

| List | Id | Bone | Wanted by |
|---|---|---|---|
| slot | `mainwep` | `Hand_R` | `ModelModule.EquipVisual`, `CombatStanceModule.weaponSlotId` |
| slot | `offwep` | `Hand_L` | `ModelModule.EquipVisual` |
| named | `hand_r` | `Hand_R` | `ProjectileSpawn.socketId`, `SpellcraftSystem.drawSocketId` — both default to this |
| named | `hand_l` | `Hand_L` | symmetry for VFX |
| named | `mainwep_sheathed` | `UpperChest` | `CombatStanceModule.sheathSocketId` |
| named | `head` / `chest` / `hips` | matching bones | `VFXSystem.SpawnAtSocket` |
| named | `foot_l` / `foot_r` | `Foot_L` / `Foot_R` | footstep dust off `FootstepEmitter.onStep` |

`mainwep_sheathed` on `UpperChest` is a back sheath. Move it to `Hips` for a hip scabbard.

## Reusing it for another model

Add a menu item calling `SetUp("Assets/path/To/OtherModel.prefab")`. The tables are shared, and
bones that do not exist on that rig are skipped with a line in the report rather than an error.

## Still open

`ModelSocketProvider.GetAllSockets()` caches by `mapping.slot.slotId` as authored, while
`GetSocket()` looks up `slotId.ToLower()`. Every slot asset is lowercase today so it works, but
one capital letter in a `slotId` makes the lookup silently return null. One `.ToLower()` in the
cache build fixes it — not done here, since it is a change to a production file.

`ModelSocketConfig` (namespace `CrabThirdPerson.Character`) is a second, entirely unused socket
system with path-based lookups like `"Armature/Hand/WeaponSocket"` that do not match this rig.
Nothing references it. Worth deleting before it costs someone an afternoon.
