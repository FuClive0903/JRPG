# Save Safety Checks

Run from the project root:

```powershell
dotnet run --project Tests/SaveSafety/SaveSafety.Tests.proj
```

Runs the production save, runtime state and battle session code against isolated
`JRPG-SaveSafety-<GUID>` directories under the OS temporary directory. Each test
removes only its own directory. Actual player saves are never accessed.

Coverage: read-only legacy migration, failed reads, failed manual activation,
preservation of the manual source slot, duplicate results and Retry, inventory
cleanup on exit, and invalid HP/MP rejection.

Filesystem operations are real. Unity APIs use test doubles and JsonUtility is
adapted to System.Text.Json. These checks do not validate Unity serialization,
native scene transitions, UI focus or Play Mode behavior. Run the BattlePlayback
suite as well for the controller's terminal-state and experience-award guard.
