# Moveset (staging)

LMB fires the active weapon's moveset — see `Moveset_Build.md` in the project docs.

New here: `WeaponMoveset.cs` (the SO, Create → Combat → Weapon Moveset), `MovesetModule.cs` (the brain module).
Edited in place: `ItemDefinition.moveset`, `CombatStanceModule.unarmedMoveset`, `HotbarSystem` (moveset slot:
bar `mouse`, slot 0), `FightTargetGoal` (attacks through the moveset when the NPC has one).

Revert: delete this folder and `git restore` those four files.

## Setup
1. Create `Moveset_Katana` and `Moveset_Unarmed` (Create → Combat → Weapon Moveset). Put `BasicAttack1` in Katana Light 1.
2. `Item_SteelKatana` → Moveset = Moveset_Katana. `Base_PC` → CombatStanceModule → Unarmed Moveset = Moveset_Unarmed.
3. Add a `MovesetModule` child under `Base_PC`'s brain (next to AbilitySystem). The mirror inherits it.

## Test
1. Katana, standing: LMB → step 1 (only step for now). The LMB slot shows BasicAttack1 and refuses a dragged ability.
2. Mash LMB during the swing → one more swing fires as it ends (buffer), not mid-swing.
3. Run, jump, or hold RMB + LMB → nothing (Running / Air / Parry chains are empty).
4. R (unarmed) + LMB → nothing (Unarmed Light empty); R back → katana swing.
5. Flinched mid-swing with a press buffered → the buffered press is dropped.
6. Mirror still attacks (through its moveset once it has the module).
