# Animation layer arbiter — staged

Single owner for animator layer weights. Callers claim a weight with a priority and release their
own claim; the highest-priority live claim per layer wins each frame.

Two files outside this folder were changed. Both are backed up under `_Staging/_Backup/`:

- `CrabSystem/Abilities/AbilitySystem.cs`
- `CrabSystem/DamageCombat/HitReactionSystem.cs`

## Files

| File | Role |
|---|---|
| `AnimationLayerController.cs` | `IBrainModule`. Owns the weights, arbitrates claims, blends. |
| `AnimationLayerProfile.cs` | Optional ScriptableObject — per-layer fades, rest weights, aliases. |
| `AnimationLayerNames.cs` | Layer name constants. |
| `AirborneLayerClaim.cs` | Holds the full-body layer up while off the ground. This is what makes jump visible. |
| `Debugging/AnimationLayerDebugger.cs` | `Log Layer Bindings` context menu. |

## Setup

1. `AnimationLayerController` onto `Component_Brain` (anywhere under the Brain works —
   `GetModule` resolves it with `GetComponentInChildren` from there).
2. `AirborneLayerClaim` beside it.
3. Nothing else. No profile asset needed; no inspector references to wire.

## Why it is safe to add

The controller touches a layer **only after something claims it**. Every other layer keeps the
weight the Animator Controller authored — so `Reactions` at 1 and `Upper Body Block` at 1 stay
where they are. Adding the component with nothing claiming changes no behaviour at all.

`runtimeWeighting = false` on the component disables all writes in play mode, which is the
fastest way to confirm a problem is or is not the arbiter.

## Priorities

| Constant | Value | Used by |
|---|---|---|
| `PriorityLocomotion` | 10 | `AirborneLayerClaim` — jump, fall, land |
| `PriorityAbility` | 50 | `AbilitySystem` |
| `PriorityReaction` | 75 | `HitReactionSystem` |
| `PriorityOverride` | 100 | debug and cutscenes |

The sequence that used to break: jump claims at 10, an attack in mid-air claims at 50 and wins,
the attack completes and **releases its own claim** instead of writing zero, the arbiter looks
again and the jump's claim at 10 is still live, so the fall animation resumes.

## What changed in the two patched files

`AbilitySystem` previously wrote 1 to the layer it wanted and **0 to the other one**. That zero
is what stomped anything else holding that layer. It now claims the layer it needs and releases
the other, and `CompleteAbility` calls `ReleaseAll` rather than fading both to zero.

`HitReactionSystem` claims at 75 and releases, instead of fading to zero on the way out.

Both keep their original direct-write path and use it when no controller is present, so the
prefabs that do not have the component behave exactly as before.

## Gotchas

**Layer 0 is `Head`, and Unity ignores layer 0's weight** — the base layer is always 1. It is
also the only layer with **IK Pass** ticked, which is where the head-look and foot IK callbacks
come from. Do not reorder layers without checking that IK Pass moves with it.

**Claims are held until released.** A system that claims and then hits an early return without
releasing pins that layer forever, and it looks exactly like the bug this replaces.
`AirborneLayerClaim` sidesteps this by deriving its claim from grounded state each frame rather
than from a jump event, and releases in `OnDisable`.

**The arbiter writes through `AnimationSystem.SetLayerWeight`**, which cancels any in-flight
`FadeLayerWeight`. Any system still calling `FadeLayerWeight` directly on a claimed layer will
be overwritten the next frame. `SpellcraftSystem` still writes `Upper Body Combat` directly —
it is untouched and works today, but it is the next candidate to convert if it ever fights an
ability.

## Diagnostics

Right-click the component → **Log Layer Bindings**. Prints every layer with its rest weight,
current weight, claim count and whether it is being driven, then one line per live claim with
its weight, priority and owning type.

## Revert

Delete this folder and restore the two files from `_Staging/_Backup/`. Or just remove the
`AnimationLayerController` component — both patched systems fall back to their original
direct-write path when it is absent.
