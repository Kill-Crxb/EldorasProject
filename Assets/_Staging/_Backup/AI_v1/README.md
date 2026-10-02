# AI_v1 backups (29 Sep 2026)

Mirror NPC + first GOAP goal pass.

- `GOAPContext.cs.bak` — before adding `aiControl` (the entity's AIControlSource).
- `ControllerBrain.cs.bak` — before `ICameraProvider` was cached for players only. A declined
  CameraModule on an NPC built from Base_PC froze the parkour handler's facing to its dead yaw.
- `PerceptionModule.cs.bak` — before the scan ignored triggers (hitboxes and sensors filled the
  20-collider buffer, so the player was often never checked) and before a target survived one
  missed scan.
- `ModelModule.cs.bak` — before a model swap re-hung equipped items and a socket was cleared
  before being filled (the mirror's natural weapon was equipped before it had a model).
- `ItemInstance.cs.bak` — before `Definition` was looked up on first use. An instance Unity
  deserialized (Base_PC's serialized equipment) never ran its constructor, so its definition was
  null and ModelModule skipped the visual without a word — the mirror's missing katana.
