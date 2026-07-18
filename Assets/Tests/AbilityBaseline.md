# Ability Test Baseline

The first-stage baseline intentionally separates two kinds of tests:

- Passing regression tests describe behavior that already works and must not regress.
- Ignored `KnownDefect` tests describe the intended behavior of confirmed defects. Remove the
  matching `Ignore` attribute when implementing each fix.

## Layout

- `EditMode/Ability/Core`: deterministic tests for `Game.Ability`.
- `EditMode/Ability/Fakes`: test doubles for units, world queries, resources, and presentation.
- `Integration/Editor`: tests against the generated Luban tables in the project. These remain in
  `Assembly-CSharp-Editor` while the game layer still lives in `Assembly-CSharp`.

## Initial known defects

- Nested configured actions overwrite the shared `ActionRunner` target list.
- Cast-point completion does not revalidate caster states or custom filters.
- Modifier-granted magic immunity is not used by damage and targeting.
- Configured modifier events react to unrelated global events.
- Non-deleting linear projectiles repeatedly hit the same unit.
- Charge restoration starts immediately instead of after the configured duration.
- Poison Dot references a non-damage periodic action group.

The baseline does not change gameplay code or configuration data.
