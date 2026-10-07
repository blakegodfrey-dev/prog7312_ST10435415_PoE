# BLAKE 2 - Live device registry and ordered recent history

## Purpose and dependency

Package: **`blake_2`**, the second of seven Smart-X Part 2 packages. Input is your newly uploaded `209f83e0-9060-4a7d-9fbd-00737f45befa.zip`, with Package 1 source present.

Target repository:

```text
C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE
```

Primary rubric target: **Lookup Structures: Dictionaries and Key-Value Maps [15 marks]**. This adds actual MAC dictionary lookup in ingestion and ordered recent-history APIs. Complete dashboard integration and later processing still belong to the remaining packages.

The React / ASP.NET Core .NET 10 / SQL Server / EF Core layered architecture remains. Package 1 navigation and state retention remain unchanged. The upload omits its old root instruction file; it is not required to install Package 2.

## What changes

- Share Part 1's colon-separated, trimmed uppercase MAC rules between sensor registration and cache lookup.
- Add immutable registered-device and native float/integer/Boolean telemetry snapshots.
- Add a process-wide `Dictionary<string, DeviceSnapshot>` and GUID-to-MAC index. Existing GUID ingestion routes remain compatible and resolve cached device configurations.
- Add `SortedDictionary<DateTimeOffset, List<TelemetrySnapshot>>`, preserving separate IDs with equal timestamps and ordering late arrivals correctly.
- Hydrate all registered SQL devices, each latest persisted reading and maximum received time, and a bounded recent window on the first live request/ingestion.
- Merge successful new registrations into an already-running registry. Initialization is asynchronous and retryable, and does not erase writes made while loading.
- Publish single/bulk telemetry only after SQL saves succeed. Current values use recorded time; an older late arrival cannot regress the latest value.
- Retain at most 2,000 recent readings globally by default. Full history stays in SQL; each device's latest state survives history eviction.
- Expose live-device and recent-history HTTP endpoints with typed responses and validation.
- Add 47 meaningful backend cases, an HTTP simulation/check script, README updates and a verification/complexity document.

**No database migration or new dependency is required.** There is no queue, priority worker, incident tracker, command/Undo or recommendation engine in this package. The frontend remains the Package 1 shell; full dashboard data integration is Package 6 work.

`lastReceivedAtUtc` is a persisted telemetry timestamp, which the existing simulator may supply. Gateway heartbeat evidence and operational connection alerts are Package 4 work.

## Apply the package

1. Extract `blake_2.zip` to its own temporary folder, such as `C:\Users\User\Downloads\blake_2`.
2. In that folder you must see `BLAKE_2_INSTRUCTIONS.md`, `README.md`, `src`, `tests`, `scripts`, `docs` and `_package` together.
3. Check your local `git status` and finish unrelated work as needed.
4. Run the block below in PowerShell. Paste the extracted package folder when asked.

```powershell
$blake2Package = (Read-Host 'Paste the extracted blake_2 folder').Trim().Trim('"')
$blake2Installer = Join-Path $blake2Package '_package\Apply-Blake2.ps1'
if (-not (Test-Path $blake2Installer)) { throw 'The selected folder does not contain the package installer.' }
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $blake2Installer -Repository 'C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE'
if ($LASTEXITCODE -ne 0) { throw 'Package application stopped. Read the error before continuing.' }
```

The process-only execution-policy setting does not change your machine policy. The installer checks all payload checksums, changed-file baselines and the unchanged Package 1 navigation prerequisites before writing. Normal Git line-ending/BOM differences are accepted. Conflicting local files stop the copy before any writes. Original changed files are backed up in a dated sibling folder; attempted writes are rolled back if copying fails. A second application skips files already installed.

`_package` contains ZIP tooling and is not copied into the repository. The installer does not stage, commit, push or reset a database. If a baseline check fails, preserve local changes and provide the updated repository snapshot rather than force-copying over them.

## Files included

All paths are relative to the repository root; no source ZIP wrapper is included.

| Repository-relative file | Operation | Commit group |
|---|---|---|
| `src/SmartX.Domain/ValueObjects/MacAddressNormalizer.cs` | Add | 1 |
| `src/SmartX.Domain/Entities/Sensor.cs` | Change | 1 |
| `src/SmartX.Application/Live/DeviceSnapshot.cs` | Add | 1 |
| `src/SmartX.Application/Live/TelemetrySnapshot.cs` | Add | 1 |
| `src/SmartX.Application/Live/LiveTelemetrySeed.cs` | Add | 1 |
| `src/SmartX.Application/Live/RecentHistorySnapshot.cs` | Add | 1 |
| `src/SmartX.Application/Live/LiveTelemetryStore.cs` | Add | 2 |
| `src/SmartX.Infrastructure/Live/LiveTelemetryQueries.cs` | Add | 3 |
| `src/SmartX.Infrastructure/Live/LiveTelemetryService.cs` | Add | 3 |
| `src/SmartX.Infrastructure/DependencyInjection.cs` | Change | 3 |
| `src/SmartX.Api/appsettings.json` | Change | 3 |
| `src/SmartX.Api/Controllers/SensorsController.cs` | Change | 3 |
| `src/SmartX.Api/Controllers/TelemetryController.cs` | Change | 3 |
| `src/SmartX.Api/Contracts/Live/LiveDeviceResponse.cs` | Add | 4 |
| `src/SmartX.Api/Contracts/Live/LiveTelemetryReadingResponse.cs` | Add | 4 |
| `src/SmartX.Api/Contracts/Live/RecentLiveHistoryResponse.cs` | Add | 4 |
| `src/SmartX.Api/Controllers/LiveDevicesController.cs` | Add | 4 |
| `tests/SmartX.Tests/Live/LiveTestData.cs` | Add | 5 |
| `tests/SmartX.Tests/Live/LiveTelemetryStoreTests.cs` | Add | 5 |
| `tests/SmartX.Tests/Live/LiveTelemetryIntegrationTests.cs` | Add | 5 |
| `tests/SmartX.Tests/Live/LiveDevicesControllerTests.cs` | Add | 5 |
| `tests/SmartX.Tests/Live/LiveTelemetrySqlTranslationTests.cs` | Add | 5 |
| `tests/SmartX.Tests/Live/LiveHttpTests.cs` | Add | 5 |
| `scripts/Test-LiveRegistry.ps1` | Add | 5 |
| `README.md` | Change | 5 |
| `docs/part2/Package_2_Verification.md` | Add | 5 |
| `BLAKE_2_INSTRUCTIONS.md` | Add | 5 |

Package-only helpers:

| Path | Purpose |
|---|---|
| `_package/Apply-Blake2.ps1` | Checked copy, backup and failure rollback |
| `_package/manifest.json` | Baseline/output hashes, prerequisite hashes and commit groups |

## Verify the installed project

Run from the exact repository root:

```powershell
Set-Location 'C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE'
dotnet restore .\SmartX.sln
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed' }
dotnet build .\SmartX.sln -c Release -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
dotnet test .\SmartX.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed' }
```

Expected: **184 backend tests**, including all 137 existing tests and 47 new Package 2 cases. These tests use controlled in-memory persistence for integration, plus actual SQL Server query translation without a database connection. The HTTP test runs Kestrel on a dynamically assigned loopback port and exercises actual controllers with production service lifetimes.

Optional focused run:

```powershell
dotnet test .\SmartX.sln -c Release --no-build --no-restore --filter 'FullyQualifiedName~SmartX.Tests.Live'
```

Frontend regression checks:

```powershell
Push-Location .\src\SmartX.Client
try {
    npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
    npm.cmd test
    if ($LASTEXITCODE -ne 0) { throw 'Frontend unit tests failed' }
    npm.cmd run test:integration
    if ($LASTEXITCODE -ne 0) { throw 'Frontend integration tests failed' }
    npm.cmd run lint
    if ($LASTEXITCODE -ne 0) { throw 'Frontend lint failed' }
    npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
} finally { Pop-Location }
```

Expected: 11 unit tests and 9 rendered navigation tests; clean lint and production build. Package 1's optional Chromium suite remains available using `npx.cmd playwright install chromium` and `npm.cmd run test:browser`; browser layout assertions are not verified in this environment.

## Run the live SQL demonstration

Keep your existing database configuration and migration. This package adds no migration and does not reset or reseed an existing hierarchy. In an API window at the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project .\src\SmartX.Api
```

In a second window:

```powershell
Set-Location 'C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-LiveRegistry.ps1
if ($LASTEXITCODE -ne 0) { throw 'Live registry demonstration failed; inspect the output.' }
```

The demo adds four to six simulation readings to your database through the existing bulk HTTP route. It deliberately submits recorded times in the order 20s, 30s, 10s, 20s, and adds other available native types at the equal timestamp. It verifies lowercase MAC resolution, preserved reading IDs, chronological results and the configured memory bound. Run against a quiet API with capacity at least 6.

Inspect the actual responses:

```powershell
$blake2Devices = @(Invoke-RestMethod 'http://localhost:5075/api/live/devices')
$blake2Mac = [Uri]::EscapeDataString(([string]$blake2Devices[0].macAddress).ToLowerInvariant())
Invoke-RestMethod "http://localhost:5075/api/live/devices/$blake2Mac"
Invoke-RestMethod "http://localhost:5075/api/live/history?macAddress=$blake2Mac&limit=100"
Invoke-RestMethod "http://localhost:5075/api/telemetry/sensors/$($blake2Devices[0].id)?pageSize=100"
```

For the existing UI, run `npm.cmd run dev` in `src\SmartX.Client` and open `http://localhost:5173`; API remains at `http://localhost:5075`. All Part 1 features and module state choices remain available. The Command Stream panels are still the shell, so use the APIs/demo to demonstrate this phase's collections.

## History and cache semantics

- Capacity is `LiveTelemetry:RecentHistoryCapacity` in `src/SmartX.Api/appsettings.json`: default 2,000, allowed range 1-100,000. This is a global count window. Restart after changing configuration.
- Recent history accepts a MAC, inclusive `fromUtc`/`toUtc`, and a limit of 1-500. It chooses the newest matching retained readings and returns them chronologically.
- `source = RecentMemory`; retained count and oldest/newest timestamps describe the global window. Matching count is before the request limit. `isLimited` reports request-limit truncation and does not describe older SQL data.
- Same recorded instants share a bucket but keep distinct reading IDs. Received time and canonical ID text give deterministic tie ordering.
- Latest device values survive history eviction. Late older recorded values do not replace a newer current value; maximum persisted received time is tracked separately.
- Warm identity lookup is average O(1). Ordered insertion also accounts for timestamp-tree lookup and shifting an equal-time list. Full complexity is documented in `docs/part2/Package_2_Verification.md`.

## Recommended genuine commits

Apply the complete overlay, inspect and verify it, then stage only the files for each group. Begin with no unrelated files staged and review each staged diff. These are five real logical changes; the ZIP does not manufacture history or create commits for you.

```powershell
Set-Location 'C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE'
git status --short
```

### 1. Share canonical MAC rules and immutable live snapshot models

```powershell
git add -- 'src/SmartX.Domain/ValueObjects/MacAddressNormalizer.cs'
git add -- 'src/SmartX.Domain/Entities/Sensor.cs'
git add -- 'src/SmartX.Application/Live/DeviceSnapshot.cs'
git add -- 'src/SmartX.Application/Live/TelemetrySnapshot.cs'
git add -- 'src/SmartX.Application/Live/LiveTelemetrySeed.cs'
git add -- 'src/SmartX.Application/Live/RecentHistorySnapshot.cs'
git diff --cached --stat
# Review the staged diff before committing.
git commit -m 'Share canonical MAC rules and immutable live snapshot models'
if ($LASTEXITCODE -ne 0) { throw 'Commit stopped; inspect the Git output.' }
```

### 2. Implement MAC registry and bounded sorted telemetry history

```powershell
git add -- 'src/SmartX.Application/Live/LiveTelemetryStore.cs'
git diff --cached --stat
# Review the staged diff before committing.
git commit -m 'Implement MAC registry and bounded sorted telemetry history'
if ($LASTEXITCODE -ne 0) { throw 'Commit stopped; inspect the Git output.' }
```

### 3. Hydrate live state from SQL and publish successful API writes

```powershell
git add -- 'src/SmartX.Infrastructure/Live/LiveTelemetryQueries.cs'
git add -- 'src/SmartX.Infrastructure/Live/LiveTelemetryService.cs'
git add -- 'src/SmartX.Infrastructure/DependencyInjection.cs'
git add -- 'src/SmartX.Api/appsettings.json'
git add -- 'src/SmartX.Api/Controllers/SensorsController.cs'
git add -- 'src/SmartX.Api/Controllers/TelemetryController.cs'
git diff --cached --stat
# Review the staged diff before committing.
git commit -m 'Hydrate live state from SQL and publish successful API writes'
if ($LASTEXITCODE -ne 0) { throw 'Commit stopped; inspect the Git output.' }
```

### 4. Expose live device lookup and ordered recent-history APIs

```powershell
git add -- 'src/SmartX.Api/Contracts/Live/LiveDeviceResponse.cs'
git add -- 'src/SmartX.Api/Contracts/Live/LiveTelemetryReadingResponse.cs'
git add -- 'src/SmartX.Api/Contracts/Live/RecentLiveHistoryResponse.cs'
git add -- 'src/SmartX.Api/Controllers/LiveDevicesController.cs'
git diff --cached --stat
# Review the staged diff before committing.
git commit -m 'Expose live device lookup and ordered recent-history APIs'
if ($LASTEXITCODE -ne 0) { throw 'Commit stopped; inspect the Git output.' }
```

### 5. Verify live collections and document Package 2 demonstrations

```powershell
git add -- 'tests/SmartX.Tests/Live/LiveTestData.cs'
git add -- 'tests/SmartX.Tests/Live/LiveTelemetryStoreTests.cs'
git add -- 'tests/SmartX.Tests/Live/LiveTelemetryIntegrationTests.cs'
git add -- 'tests/SmartX.Tests/Live/LiveDevicesControllerTests.cs'
git add -- 'tests/SmartX.Tests/Live/LiveTelemetrySqlTranslationTests.cs'
git add -- 'tests/SmartX.Tests/Live/LiveHttpTests.cs'
git add -- 'scripts/Test-LiveRegistry.ps1'
git add -- 'README.md'
git add -- 'docs/part2/Package_2_Verification.md'
git add -- 'BLAKE_2_INSTRUCTIONS.md'
git diff --cached --stat
# Review the staged diff before committing.
git commit -m 'Verify live collections and document Package 2 demonstrations'
if ($LASTEXITCODE -ne 0) { throw 'Commit stopped; inspect the Git output.' }
```

The source snapshots for groups 1-4 build in order. The groups intentionally keep the two closely related collections together, then separate SQL/ingestion integration from HTTP exposure. Group 5 adds verification, demonstration and documentation.

## Results and remaining work

Verified during preparation on 7 October 2026: 184 backend tests, 20 frontend tests, warnings-as-errors .NET Release build, frontend lint/build, SQL Server provider query translation, and a real HTTP registration/ingestion/live-history workflow with EF InMemory persistence.

Live SQL Server execution, Windows installer/demo execution and browser layout checks remain local verification. Query translation is not a SQL execution or latency benchmark. The store is for one API process; direct SQL edits or other API instances require restart/rebuild. SQL commit and cache publication are not a distributed transaction; restart hydration recovers durable committed data.

Next is `blake_3`: FIFO/priority telemetry buffers and background processing. Packages 4-7 add unique incidents/connection evidence, commands/Undo, full dashboard integration and learned suggestions. Before the next package, provide the latest source ZIP with `blake_2` applied so subsequent work follows your current repository.
