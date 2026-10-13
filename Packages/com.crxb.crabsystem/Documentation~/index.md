# CrabSystem documentation

CrabSystem is a Unity 6 / URP framework for character-driven games. Every character has a `ControllerBrain`, which finds its modules and wires them: stats, resources, abilities, damage, statuses, AI, inventory, saves and UI. The game supplies data assets and prefab variants; the framework supplies behaviour and the editor tools that check the data.

It was built alongside a third-person action RPG (NinjaGame), so some modules are shaped by that game. [Getting started](GettingStarted.md) lists which parts carry over to other genres.

## Read in this order

1. [Getting started](GettingStarted.md): install, project setup, starter prefabs, publishing updates, what a game must provide.
2. [Architecture](Architecture.md): brains and modules, managers, providers, events and facts, the entity clock, the random source, ids.
3. [Systems](Systems.md): every module and manager, with its init order, interfaces and events.
4. [Standard](Standard.md): the rules every system follows, and the review checklist.
5. [Decisions](Decisions.md): settled design choices and the precedents behind them.

`templates/CLAUDE.md` is a starting `CLAUDE.md` for a game project that uses CrabSystem. `tools/build.py` compiles the package and a game's scripts outside the editor, for a quick error check.

Unity doesn't import folders whose names end in `~`, so these files travel with the package without becoming assets.
