# UI regression data

The UI integration tests read the owner's DD1 data. Select the intended install for a test process with `DD3_TEST_DD1`:

```powershell
$env:DD3_TEST_DD1 = 'C:\Users\Piral\darkestwithdlc\game'
dotnet test tests/DarkestDungeon3.Ui.Tests -c Release
```

Run builds from a clean checkout or a reviewed frozen source tree. Preserve concurrent working files.

When the variable is unset, the previous Steam discovery behavior remains. An invalid explicit directory fails before discovery; it cannot silently select another install. Tests that create a miniature data fixture keep their explicit fixture path. This setting changes test data selection only; production discovery and game configuration are unchanged.

The31October2026 regression used the healthy C: data after default discovery selected an E: install with CRC read errors. All297existing tests and two new configuration checks passed. The E: install was not modified or repaired.
