# Ability database

Every `AbilityDefinition` asset in this folder is registered by `AbilityManager` at startup
via `Resources.LoadAll<AbilityDefinition>("AbilityDatabase")`. Drop an asset in and it exists
in game — there is no inspector list to keep in sync.

Ids must be unique. A clash is logged as an error naming both assets.

The sibling `ItemDatabase` folder works the same way for `ItemDefinition`, through `ItemManager`.

Both live under a single `Resources` root so there is one place to look for game data. The
`Resources` folder name is a Unity requirement — it is what makes runtime loading by name work.
