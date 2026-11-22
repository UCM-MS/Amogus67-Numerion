# Shared pet battle rules

`dotnet run --project Tools/BattleHarness/BattleHarness.csproj`

Tests full deterministic duels, affinity and topic bonuses, weak attacks without questions,
finite PP/energy/healing, shield timing, swaps and knockouts, invalid/replayed commands,
serialized states, capped loadouts, ten-pet collections, AI completion and one-time rewards.

Export engine-independent content (no Unity API dependency):

`dotnet run --project Tools/BattleHarness/BattleHarness.csproj -- --export Assets/Lumen/Resources/Content/pets.json`

The online service must validate ownership, a question's answer and its `Subject` topic before
constructing a command. `Correct` and `Topic` must never be trusted directly from an online
client. A weak `QuickStrike` does not require a question and never increases the mathematical
participation counter. Winning only through weak attacks does not grant learning rewards.

Action IDs are `battleId:round:side`. The server accepts at most one command per side and round,
copies the committed command, and resolves only when both sides have committed.

`SetLoadoutBonus` accepts already-validated artifact bonuses before play; it caps additions at
20 HP and 3 attack per pet. The default party remains normalized to 100 HP and 26 attack.

Data export is for interoperability. The C# rules and a separately tested server implementation
remain responsible for validating actions and calculating results; JSON alone is not an engine.
