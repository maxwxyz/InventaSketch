# PowerPoint UI scenarios

Scripted checks of the installed COM add-in in real PowerPoint (Windows PowerShell 5.1, PowerPoint desktop).
Close PowerPoint, install the current build, then:

```powershell
.\tests\powerpoint-com\Invoke-IskScenarios.ps1 -List
.\tests\powerpoint-com\Invoke-IskScenarios.ps1 -Name explode-linkages
```

Screenshots go to `local-resources/powerpoint-tests/` (git-ignored). Details: [docs/en/hosts/powerpoint-com.md](../../docs/en/hosts/powerpoint-com.md#ui-scenarios-powerpoint-run-locally)
(German: [docs/de/hosts/powerpoint-com.md](../../docs/de/hosts/powerpoint-com.md#ui-szenarien-powerpoint-lokal-ausführen)).

| File | Purpose |
| --- | --- |
| `IskPowerPoint.psm1` | session start/stop (never touches a running PowerPoint), mouse, screenshots, model reading, checks |
| `Invoke-IskScenarios.ps1` | runs the scenarios, one fresh PowerPoint each, prints a summary |
| `scenarios/*.ps1` | one scenario per file; first line `# Scenario: …` describes it |
