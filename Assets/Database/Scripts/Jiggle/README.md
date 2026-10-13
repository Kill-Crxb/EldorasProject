# Porphi breast jiggle — staged

Magica Cloth 2 **BoneSpring** on `Breast_01_L` / `Breast_01_R`, applied to
`Assets/Database/Characters/MC/Porphi.prefab` by an editor menu command.

The prefab as it was before is backed up at
`_Staging/_Backup/Database/Characters/MC/Porphi.prefab.bak`.

## Run it

**Tools → Crab → Jiggle → Setup Porphi Breast Spring**

It loads the prefab, adds a `BreastCloth` child with a `MagicaCloth` component, configures it,
saves, and logs what it set. Running it again reconfigures the same component rather than
stacking a second one.

**Tools → Crab → Jiggle → Remove Porphi Breast Spring** deletes the `BreastCloth` child.
That is the clean revert; the `.bak` is the blunt one.

## Why BoneSpring and not BoneCloth

BoneCloth pins the root bone and swings its children — right for hair, skirts and cloaks.
BoneSpring lets the registered bones themselves travel a bounded distance from their animated
position and spring back, which is what flesh does. MC2 ignores gravity entirely in BoneSpring
mode (`ClothSerializeDataFunction.cs`: `gravity = clothType == BoneSpring ? 0.0f : gravity`),
so there is no sag to fight, and it forces Line connection regardless of what you set.

The rig gives each side a two-bone chain — `Breast_01_*` → `Breast_02_*`, parented to
`UpperChest`. Only the `_01` bones are registered as roots; `_02` comes along as a child.

## Distances are measured, not typed

This FBX's skeleton coordinates sit around 0.001 (Blender export with `FBX_SCALE_NONE`), so a
hardcoded "0.1 m limit distance" could be wildly wrong in world space. The command measures
`Breast_01` → `Breast_02` in the loaded prefab and derives everything from it:

| Setting | Value |
|---|---|
| `springConstraint.limitDistance` | 0.5 × chain length |
| `radius` | 0.35 × chain length |

The console log prints the measured length and the resulting metres. If those numbers look
absurd, the import scale is the problem, not the spring.

## Tuning

Three dials, on the `MagicaCloth` component of the `BreastCloth` child:

- **`springConstraint.springPower`** (0.03) — how hard it pulls back. Higher is tighter and
  faster; lower is looser and slower. This is the main feel control.
- **`springConstraint.limitDistance`** — how far it may travel. Raise for more motion, but past
  roughly the chain length it starts detaching visually.
- **`damping`** (0.05) — how quickly the oscillation dies. Raise it if it wobbles too long.

`angleLimitConstraint.limitAngle` is set to 30° as a safety rail against the bones folding
somewhere anatomically alarming during fast animation. `inertiaConstraint.worldInertia` and
`localInertia` are both 1 — those are what let body movement drive the motion at all, so
lowering them makes the whole thing inert.

## Chest collider

**Tools → Crab → Jiggle → Add Porphi Chest Collider**

Separate from Setup on purpose: Setup rewrites every spring parameter, so re-running it would
discard inspector tuning. This command touches collision only.

It puts a `MagicaCapsuleCollider` on a `ChestCollider` child of `UpperChest`, sized from the
gap between the two breast root bones — the only chest measurement the skeleton offers. That
is a starting point, not a fit: the capsule gizmo draws in the scene view and wants eyeballing
against the torso mesh.

The capsule's rotation is matched to the character root rather than the chest bone. `Direction`
is read in the collider's own local space, and this rig exports with `primary_bone_axis='Y'`,
so the chest bone's X is not reliably the body's left-right. It still follows the chest,
because it is parented to it.

Two things the command wires that are easy to miss by hand:

- **`collisionBones`.** BoneSpring collides only the transforms listed here, and they must
  already belong to the spring — `RenderSetupData` resolves them with `IndexOf` against the
  spring's own transform list, so an unrelated transform silently resolves to -1. Set to the
  two `Breast_02_*` tips.
- **`colliderCollisionConstraint.limitDistance`.** How far a collider may push a particle off
  its origin. The 0.05 m default is an arbitrary absolute on a rig whose import scale is not
  obvious, so it is derived from the measured separation.

## If it still reads flat

Check `normalAxis` before adding more collision. The ellipsoid clamp squashes travel along that
axis, and it defaults to `Up` — correct only if the bone's Y points out from the chest, which
is what `Breast_02_L`'s pure +Y offset from `Breast_01_L` implies. Test it by dropping
`normalLimitRatio` to 0.05: the motion should collapse to a disc that still moves up/down and
side to side. If instead it can still move in and out, the axis is wrong — try `Forward` or
`Right`. A collider cannot fix a clamp that is squashing the wrong axis.

## Slowing the simulation down

`ClothTimeScale.cs` — drop it on the Porphi prefab root. With `targets` empty it drives every
`MagicaCloth` beneath it, so one component covers both the chest and the butt.

This is not damping. `TeamManager` advances each cloth by `deltaTime * timeScale`, so a lower
value simply feeds the solver less time per frame: the motion keeps its shape and its amplitude
and takes longer to play out. Damping cannot do that — it removes energy, so it shrinks the
movement at the same time as slowing it.

MC2 exposes this only as the runtime `SetTimeScale`, with no serialized field, which is why it
needs a component. It re-applies until the value reads back, because the call is a no-op until
the cloth has finished building a few frames after Start — that also makes it live-tunable from
the inspector during play.

Around 0.75 reads as weight. Below roughly 0.4 it starts reading as underwater, because the
slowdown applies to the response to body movement as well, not just the settle.

`MagicaManagerAPI.SetGlobalTimeScale` does the same thing for every cloth in the scene, if a
project-wide slow-motion feel is ever wanted instead.

Lowering `springPower` also lowers the oscillation frequency — frequency goes with the square
root of stiffness — but it raises overshoot at the same time, so it trades speed for looseness
rather than being a clean speed control.

## Not set up

- **Hair.** The rig carries ~25 hair bones (`DEF_HAIR_*`). Those want BoneCloth, not
  BoneSpring, and are a separate setup.

## Requires

Magica Cloth 2 needs Burst ≥ 1.8.2, Collections ≥ 1.4.0 and Mathematics ≥ 1.2.6. Its asmdef is
gated on define constraints for the first two, so if those packages are missing the whole
`MagicaClothV2` assembly silently drops out and this editor script fails to compile with it.
A compile error naming `MagicaCloth2` means that, not a bug in the script.
