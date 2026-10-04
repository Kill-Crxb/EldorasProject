# Validate

`Tools > CrabSystem > Validate` runs the CrabSystem Standard's review checklist (`claude/CrabSystem_Standard.md`, section 13) over `Assets/CrabSystem` and writes `Logs/CrabSystem_Validate.md` (project root, next to `Assets/`).

Read-only. A hit is a prompt to look, not proof of a defect. A rule broken on purpose should carry a short comment saying why.

Rules: `#region`, `///` doc comments, `Debug.Log` outside diagnostics, scene searches (`Find*`, `GetComponent(s)InChildren`), third-party assets in the framework, literal stat ids, literal fact keys, unseeded random / wall-clock time in gameplay, and `+=` subscriptions without a matching `-=` in the same file.

Graduated 4 Oct (AU25). It becomes the Health tab of the Crab Dashboard (TL18).
