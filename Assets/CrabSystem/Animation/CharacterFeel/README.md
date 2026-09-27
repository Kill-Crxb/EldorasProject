# Character feel — staged

Procedural motion layers for the player character. Folder is still named `HeadLookIK` from when
that was all it held; renaming it mid-testing seemed worse than the misnomer.

Nothing outside this folder is modified except two read-only properties on `CharacterMotor`
(`CollisionLayers`, `MaxSlopeAngle`), backed up in `_Staging/_Backup/`.

## Files

| File | Role |
|---|---|
| `IAnimatorIKReceiver.cs` | Interface. `IKOrder` + `ApplyIK`. |
| `AnimatorIKRelay.cs` | Auto-attached to the Animator's GameObject. Owns `OnAnimatorIK`, fans out to receivers in order, once per frame. |
| `AnimatorIKModule.cs` | Shared plumbing: animator lookup, relay binding, model-swap re-bind, IK-Pass-off error. |
| `FootIKSystem.cs` | Foot planting, hip drop, turn-in-place foot lock, ground-contact events. Order 0. |
| `LandingImpactSystem.cs` | Hip dip on touchdown, scaled by fall speed. Order 5. |
| `HeadLookSystem.cs` | Camera-driven head look plus micro-drift. Order 10. |
| `BodyLeanSystem.cs` | Bank into turns, pitch into acceleration. **Not** an IK receiver — see below. |
| `FootstepEmitter.cs` | Audio and VFX hooks off the foot IK's contact events. |
| `Debugging/*.cs` | Gizmos and context-menu state dumps. |

## Setup

1. `FootIKSystem`, `LandingImpactSystem`, `HeadLookSystem`, `BodyLeanSystem` onto the
   `ControllerBrain` GameObject. All four are auto-discovered by `CacheModuleArrays`.
2. **IK Pass** ticked on a layer of the Animator Controller. Without it none of the IK modules
   do anything, and each logs one error after a second.
3. `footHeight` on `FootIKSystem` — add `FootIKDebugger`, stand on flat ground in play mode,
   right-click → **Measure Foot Height**, copy the number in.
4. **A lean pivot** — see below. `BodyLeanSystem` disables itself with an error until it has one.
5. `FootstepEmitter` on the same GameObject, with an `AudioSource` and some clips.

### The lean pivot, and why it is manual

`BodyLeanSystem` rotates a transform **above** the Animator, in `Update`, before the animation
update runs. It does not use `animator.bodyRotation`, which is tempting and wrong: that is
applied after the IK pass resolves, so foot IK would never see the lean and her feet would swing
out from under her while the solver happily planted them where they used to be.

Rotating an ancestor instead means every world position the IK pass reads — `GetIKPosition`
included — already carries the lean, and the feet plant correctly under a banked body.

Make an empty GameObject under the character root, move the model root under it, assign it to
`leanPivot`. It has to be a transform **nothing else drives** — if movement or facing already
writes its rotation, the two will fight. That is why the script does not guess one for you.

⚠ **Then set `facingOverride` on `HeadLookSystem` to `EntityRoot`.** By default head look reads
`ModelRoot.forward`, and once the model root sits under a banking pivot that forward is tilted,
so her head goes crooked in turns.

## Foot lock (turn-in-place)

With `CameraDrivesFacing`, rotating the camera while stationary spins the body over stationary
feet and they skate. Below `lockSpeedThreshold` each foot's world-space target is frozen where
it planted, so the body turns over the feet instead. A foot releases and re-plants when the body
has twisted past `lockReleaseAngle` (50°) or the animated foot has walked further than
`lockReleaseDistance` (0.35 m) from its lock — the second guard is the leg-stretch catch, since
the angle alone does not cover a foot that simply moves away.

Set `lockWhileStationary` false to turn the whole thing off.

## Contact events

`FootIKSystem.OnFootPlanted` fires from inside the IK pass with the contact point, surface
normal, collider and current speed. Handlers must be cheap — a one-shot and a particle emit, not
a raycast.

Contact detection deliberately runs even when the IK weight has faded to zero at speed.
Otherwise footsteps would stop the moment she sprints, which is precisely backwards.

`FootstepEmitter` consumes it: per-surface clip sets matched on collider tag, volume ramped by
speed, pitch jitter, no-repeat clip picking, and a `UnityEvent<Vector3, Vector3>` carrying
position and normal for dust or decals.

## Landing impact

Fall speed is tracked while airborne and sampled at touchdown, mapped through
`minImpactSpeed` → `maxImpactSpeed` (3 → 14 m/s) into a hip dip up to `maxDip` (0.18 m). The
compression is fast and the recovery is slow; that asymmetry is what reads as absorbing weight
rather than bouncing. `OnLanded(float strength)` is there for camera shake, dust and audio.

## Head micro-drift

A degree and a half of Perlin drift on the look direction, seeded per instance so a crowd does
not sway in unison. Set `noiseAngle` to 0 to disable. Perfectly still heads read as mannequins;
this costs nothing and is the only lifelike trick available without a face rig.

## Lean tuning

`bankPerTurnRate` is degrees of roll per degree-per-second of turn. **Negate it if she banks the
wrong way** — the sign depends on which way your model root faces, and guessing it from here
would have been a coin flip. `pitchPerAcceleration` negative leans back instead of forward.
Both scale down below `referenceSpeed` so a slow pivot on the spot does not throw her sideways.

`SpeedBlend` from `MovementSystem` is the alternative scale if you want lean only above the run
threshold; `referenceSpeed` was chosen instead so a walk still gets a little.

## Order

`AnimatorIKRelay` drives receivers by `IKOrder`: feet (0) → landing (5) → head (10). Lean sits
outside that entirely, in `Update`, ahead of the animator.

## Revert

Delete the folder. Per-feature backups in `_Staging/_Backup/`: `HeadLookIK_v1` (head look before
the relay was generalised), `FootIK_v1` (before the anti-snap pass), `CharacterFeel_v2` (before
this pass). `CharacterMotor.cs.bak` is the only file outside the folder.
