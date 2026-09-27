# Battle Playback Tests

Run from the project root with the .NET 9 SDK:

```powershell
dotnet run --project Tests/BattlePlayback/BattlePlayback.Tests.proj
```

The tests compile the actual controller, actor/field views, combatants, turn order,
session and reward code. Gated actor views hold each stage until explicitly released
to verify that combat waits for playback completion, not a fixed duration.

Coverage includes duplicate input, damage timing, next-turn timing, victory after
all defeats, poison death, single item consumption, cancellation, retry snapshot
preservation, revival appearance, missing views and stun expiry.
Two additional checks cover foot alignment across sprite sizes and keeping ground
and selection renderers separate from actor fading.
Transition checks verify command locking through the intro fade and movement to
an independently authored dock after the outcome has appeared in the center.

UnityDoubles.cs supplies a small nested-iterator scheduler and stand-ins for Unity
APIs. It is outside Assets and never enters the game build. These tests do not
verify Unity lifecycle dispatch, actual rendering, Animator events, scene wiring,
or random drop distribution. Run the Play Mode checklist in TODO.md as well.

## Background Checks

The actual BattleEnvironmentView.Awake is invoked with an Instantiate recorder.
Checks cover map-over-default priority, an unspecified map or direct battle using
the default, both missing (no instance), and defeat/Retry followed by another map.
The recorder verifies one instantiation, its source, parent and local-space flag;
it does not clone or render a real Unity prefab. Session reset/cleanup and the
actual pause-menu restart/title methods also verify background reference lifetime.

## Pause Menu Checks

The same command also runs 12 checks in PauseMenuChecks.cs against the actual
BattlePauseMenu, MenuInput and MenuInputState scripts. They cover time and renderer
restoration, slot-page cancel/disable/Esc, X taking priority over Z, original input
settings, retry entry preservation, title cleanup, missing scene/data failures,
preserving a new page's focus, and keeping the old battle frozen during scene exit.

PauseMenuDoubles.cs replaces the slot UI, save services and scene manager. These
checks do not load real saves, write player files, dispatch native UI events or
activate Unity scenes. The data-load failure check exercises the catch path before
save activation; it is not a disk corruption or successful load test.
