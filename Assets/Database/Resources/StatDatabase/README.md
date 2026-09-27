# Stat database

Every `StatSchema` in this folder is registered by `StatsManager` at startup via
`Resources.LoadAll<StatSchema>("StatDatabase")`. Drop a schema in and entities can reference
it by asset name in `StatSystem.schemaIds` — there is no inspector list to keep in sync.

Schema names must be unique; a clash is logged as an error naming both. Stat ids must be
unique across *all* schemas, since `StatSystem` merges them into one set per entity.

A schema marked `derived` is never written to the save file and always starts at its
default. Use it for anything produced by contributions rather than owned by the character.

`CoreDerivation` is not a schema — it is the breakpoint table that turns core stats into
secondary ones. It lives here because it is the same kind of balance data, and it is
referenced by GUID from `StatDerivationSystem` rather than loaded by name.

The sibling `ItemDatabase` and `AbilityDatabase` folders work the same way for
`ItemDefinition` and `AbilityDefinition`.
