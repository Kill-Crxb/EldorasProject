# Unarmed Stance — Setup Walkthrough

Everything is authored on **`Base_PC`** and on **the model prefab your ModelDatabase spawns**.
Verified against the prefab as it stands, so the paths below are real, not illustrative.

## Where you already are

| | Status |
|---|---|
| `Unarmed_Fists.asset` | **Done** — `WeaponData`, 1d4, flatBonus 2, Unarmed/Natural. Correct. |
| `CombatStanceModule` GameObject under `Component_Brain` | **Made, but empty** — the GameObject exists with only a Transform. The script isn't attached. |
| Sheath socket | Not started |
| Fist hitboxes | Not started |
| Hotbar pages | Not started |

---

## Step 1 — Get the build green

Delete `Assets/CrabSystem/Input/PlayerInputControls.cs` **and its `.meta`**. It's the stubbed
duplicate; the live generated wrapper is `Assets/CrabSystem/PlayerInputControls.cs`.

Then decide about **R being double-bound**. `PlayerInputControls` now has both `Consume` and
`ToggleStance` on `<Keyboard>/r`. Nothing reads `Consume` today, so it's harmless — but both
actions fire. Open the `.inputactions` asset and delete the `Consume` binding, or move it.

---

## Step 2 — Attach `CombatStanceModule`

Select `Base_PC > Component_Brain > CombatStanceModule` and **Add Component → Combat Stance
Module**. It's auto-discovered by `GetComponentsInChildren<IBrainModule>`, so being a child of
`Component_Brain` is all the wiring it needs — no `ControllerBrain` field to fill.

Fields:

| Field | Value | Note |
|---|---|---|
| Unarmed Weapon | `Unarmed_Fists` | The only required field. |
| Weapon Slot Id | `mainwep` | Matches `Slot_MainWeapon.slotId`. Leave it. |
| Sheath Socket Id | `mainwep_sheathed` | Must match Step 3 exactly. |
| Sheathe / Draw Ability | *empty* | Leave empty until you have the clips — the swap is instant. |
| Transition Delay | `0` | Only useful once there's an animation to hide the swap inside. |
| Animator Bool | `IsUnarmed` | Skipped silently if the parameter doesn't exist. |
| Blackboard Fact | `IsUnarmed` | **Don't rename this** — Step 5's page keys off the same string. |
| Debug Logging | `true` for now | Logs every stance change. |

---

## Step 3 — Sheath socket

This goes on **the model prefab**, not `Base_PC`. `ModelModule.currentModel` is empty in the
prefab — the model is spawned at runtime from the ModelDatabase — so find the prefab that
database points at.

> **Check this first:** `HumanM_Model.prefab` has an `AnimationEventForwarder` but **no
> `ModelSocketProvider`**. If that's your active model, the katana has no socket to attach to
> either, and you'd already be seeing "*no ModelSocketProvider*" warnings. Add the component and
> map `Slot_MainWeapon` → `B-handProp.R` before worrying about sheathing.

On the model's `ModelSocketProvider`, under the new **Named Sockets** list, add one entry:

- Socket Id: `mainwep_sheathed`
- Socket: a back or hip bone — `B-spine` is the obvious candidate on this rig

Named sockets are separate from the slot-socket list precisely so you don't have to invent a fake
`EquipmentSlotDefinition` just to hang a scabbard off a bone.

**Skippable.** With no such socket the weapon is hidden on sheathe instead of moved. Functional,
looks like a placeholder.

---

## Step 4 — Fist hitboxes

The one bit of real work, and **required** — without it, punches connect with nothing. When the
katana is hidden its collider goes with it, so nothing is left to swing.

Also on the model prefab:

1. Empty GameObject under `B-hand.R`, named e.g. `Fist_Hitbox_R`.
2. Add a `SphereCollider`, radius ~0.1. `WeaponHitbox` forces `isTrigger` itself.
3. Add `WeaponHitbox`:
   - Weapon Name: `Right Fist`
   - Hitbox Collider: the sphere you just added
   - Hit Layers: **copy from the Katana prefab's Hitbox** — it's layers 3, 7, 10, 11, 12
   - **Stance Filter: `UnarmedOnly`**
   - Debug Hitbox: `true` while testing
4. Repeat for `B-hand.L` if you want both fists.
5. Open `Katana.prefab` and set its Hitbox's **Stance Filter to `ArmedOnly`**.

Step 5 is what stops the sheathed katana swinging alongside the punch — `AbilitySystem` enables
*every* `WeaponHitbox` under the entity, so the filter is the only thing separating them.

The model spawns under `Component_Brain > Model_System`, so hitboxes on it resolve the brain
correctly via `GetComponentInParent`.

---

## Step 5 — Hotbar pages

Two assets, **Create → RPG → Hotbar Page**:

**`Page_Default`**
- Page Id: `default`
- Activation Fact: **empty** ← this is what makes it the fallback
- Priority: `0`
- Supplied Bars: ignored, the default always supplies everything

**`Page_Unarmed`**
- Page Id: `unarmed`
- Activation Fact: `IsUnarmed` ← same string as Step 2
- Priority: `10`
- Supplied Bars: `centre`, `bottomLeft` — leave `bottomRight` off so consumables stay put

Then select `Base_PC > Component_Brain > HotbarSystem` and add both to **Page Definitions**,
default first. Turn on Debug Logging to see `Page → unarmed` in the console.

Bar ids are exact, lowercase-camel: `centre` (ZXCV), `bottomLeft` (Q), `bottomRight`.
Note the British spelling of `centre`.

**Skippable.** With no pages authored, `HotbarSystem` synthesises a default and behaves exactly as
before.

---

## Step 6 — Animator (optional)

Add an `IsUnarmed` bool parameter and drive your fist idle/locomotion from it. Guarded by
`HasParameter`, so nothing breaks if you skip it.

---

## Order, if you want it working in the fewest moves

1. Step 1 (build) → 2 (attach script) → 4 (fist hitbox on the right hand only).

That gives you: R flips the stance, damage becomes 1d4+2, the katana hides and stops cutting, the
fist connects. Steps 3, 5 and 6 are polish and groundwork on top.

---

## Verifying it

Turn on, in order: `CombatStanceModule.debugLogging`, `WeaponHitbox.debugHitbox` on both the fist
and the blade, `HotbarSystem.debugLogging`. `Base_PC` also already carries a
`BlackboardDebugDisplay` — watch `IsUnarmed` flip there.

Expected on pressing R with the katana equipped:

```
[CombatStance] Base_PC → Unarmed
[HotbarSystem] Page → unarmed          (within ~100ms — facts are polled at 10Hz)
[WeaponHitbox] 'Weapon' skipped — wrong stance for ArmedOnly
[WeaponHitbox] 'Right Fist' enabled
[DamageEffect] Dealt 5.0 Physical to ...
```

## The gotcha most likely to bite

**`useWeaponDamage` must be ticked** on the `DamageEffect` of whatever ability you're swinging
(`BasicAttack1`, `Cleave`, …). If it's unchecked the ability uses its own flat `baseDamage` and
never consults the weapon *or* the fists — R would change the visuals and the hitboxes but the
damage numbers wouldn't move, which looks like the stance system is broken when it isn't.

## What won't work yet, and why that's expected

Punches will play the **katana swing animation**. There's no unarmed `AbilitySlotData` authored
anywhere in `Data/Abilities/Ability Slot`, and no punch clips. Once you have them, they go on
`Page_Unarmed`'s bars — no code changes.
