# Smart-X IoT Mesh Ecosystem — Parts 1 and 2

Student number: **ST10435415**
Module: **PROG7312 / AAPD7112 — Programming 3B / Advanced Application Development**

Smart-X simulates a South African hydroponic facility whose environmental sensors, power meters and ESP32 actuators communicate with a .NET 10 gateway through HTTP. React displays native telemetry, contextual anomalies, connection health, operational commands and suggestions learned from operator behaviour. SQL Server stores the durable records.

## Implemented scope

| Home module | Status |
|---|---|
| Sensor Data Ingestion and Telemetry | Implemented and enabled |
| Real-Time Command Stream and History | Implemented and enabled |
| Network Topology and Mesh Routing | Visible and disabled; reserved for the final PoE |

Part 1 registration, typed ingestion, deployment validation, attachments and anomaly investigation are retained. Part 2 adds FIFO and priority processing, a live device registry, ordered recent history, unique incidents and recovery, acknowledged commands, Stack-backed Undo, and learned suggestions. Module navigation preserves filters, selections and registration drafts within the current browser application session.

## Setup

Required: .NET 10 SDK, Node.js **22.12 or later in the 22.x line, or a supported newer release**, npm, Git, and SQL Server Express LocalDB (`MSSQLLocalDB`) or another configured SQL Server. Use `npm.cmd` in Windows PowerShell to avoid the `npm.ps1` execution-policy issue.

Run from the repository root:

```powershell
dotnet restore .\SmartX.sln
dotnet build .\SmartX.sln -c Release --no-restore -t:Rebuild -warnaserror
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

Push-Location .\src\SmartX.Client
try {
    npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw "Client dependency installation failed." }
} finally { Pop-Location }

if (-not (Test-Path .\src\SmartX.Client\.env)) {
    Copy-Item .\src\SmartX.Client\.env.example .\src\SmartX.Client\.env
}
```

The client configuration must point to `http://localhost:5075`. CORS permits `http://localhost:5173`; use that browser origin.

### Terminal 1: API and test database

```powershell
$env:ConnectionStrings__SmartXDatabase = "Server=(localdb)\MSSQLLocalDB;Database=SmartXTestDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
dotnet run --project .\src\SmartX.Api -c Release --no-build --launch-profile http
```

The HTTP launch profile selects Development. Development startup applies the committed EF Core migrations and seeds an empty deployment hierarchy. The baseline contains 9 deployment locations, 12 sensors and 3,456 historical typed readings, including deliberate anomalies. Existing deployment data is preserved. Use a separate test database because simulator scenarios register devices and persist readings, commands and interactions.

Health: `http://localhost:5075/api/health`. OpenAPI JSON: `http://localhost:5075/openapi/v1.json`. A Swagger UI is not installed. For another SQL Server instance, change the environment connection string. Production requires explicit configuration and database preparation; Development seeding does not run there.

### Terminal 2: React

```powershell
cd .\src\SmartX.Client
npm.cmd run dev -- --host localhost --port 5173 --strictPort
```

Open **http://localhost:5173**. Restart Vite after changing `.env`.

## Automated verification

From the repository root:

```powershell
dotnet test .\SmartX.sln -c Release --no-build --no-restore
```

From `src/SmartX.Client`:

```powershell
npm.cmd test
npm.cmd run test:integration
npm.cmd run lint
npm.cmd run build
npm.cmd audit
```

The integration suite renders React through Vite and JSDOM using controlled HTTP responses. Backend tests include real HTTP/controller tests with test persistence providers. These checks complement live SQL-backed scenarios; they do not all use SQL Server.

An additional browser suite is available:

```powershell
npx.cmd playwright install chromium
npm.cmd run test:browser
```

It starts its own Vite server on `127.0.0.1:4174`, intercepts API responses, and requires that port to be free. Its execution is not included in the 241-test result below.

## Live Part 2 scenarios

Keep the API running. Run scripts from the repository root in a third terminal. Every reading enters the HTTP API; the scenarios do not insert telemetry directly into SQL.

```powershell
node .\scripts\simulate-operations.mjs http://localhost:5075 normal
node .\scripts\simulate-operations.mjs http://localhost:5075 lifecycle
node .\scripts\simulate-operations.mjs http://localhost:5075 learning
```

| Scenario | Assertions |
|---|---|
| `normal` | Native float/integer/Boolean ingestion, preserved zero/false, acknowledged actuator command, Undo, wrong-type rejection and valid-reading recovery |
| `lifecycle` | Repeated abnormal readings retain one incident ID; real timeout disconnects the device; heartbeat restores connection; range recovery clears the incident; recurrence creates a new ID |
| `learning` | New target starts without learned recommendations; three matching historical actions produce a suggestion; subsequent conflicting behaviour changes the top action or its confidence |

Each mode first performs the normal checks and registers three new simulation sensors. The final suffix identifies their names in the UI. `lifecycle` waits approximately **93 seconds** with the default disconnection threshold. After a simulator exits, devices naturally become stale and disconnected unless another process sends heartbeats.

### FIFO backlog and critical priority bypass

Stop the API with Ctrl+C. In the same API terminal, retain the test database configuration and start with a temporary 100 ms processing delay:

```powershell
$env:Operations__ProcessingDelayMilliseconds = "100"
dotnet run --project .\src\SmartX.Api -c Release --no-build --launch-profile http
```

Run in the third terminal:

```powershell
node .\scripts\simulate-operations.mjs http://localhost:5075 priority
```

The script sends 40 concurrent ordinary readings, observes a backlog, then submits a critical power reading. It asserts that the critical packet finishes while ordinary packets still wait, verifies the critical incident, and submits a normal recovery value. Ordinary packets use FIFO order. Critical work bypasses waiting ordinary work; it does not interrupt an in-flight database write.

The queue capacity must be at least 100; the default is 5,000. After the demonstration, stop the API, remove the temporary delay, and restart:

```powershell
Remove-Item Env:\Operations__ProcessingDelayMilliseconds -ErrorAction SilentlyContinue
dotnet run --project .\src\SmartX.Api -c Release --no-build --launch-profile http
```

### MAC dictionary lookup and ordered history

With the API running, select one registered device and resolve its lowercase MAC through the live endpoint:

```powershell
$devices = Invoke-RestMethod "http://localhost:5075/api/live/devices"
$device = $devices | Select-Object -First 1
if (-not $device) { throw "Register a device first." }
$mac = [uri]::EscapeDataString($device.macAddress.ToLowerInvariant())
$resolved = Invoke-RestMethod "http://localhost:5075/api/live/devices/$mac"
if ($resolved.id -ne $device.id) { throw "MAC lookup returned the wrong device." }
$history = Invoke-RestMethod "http://localhost:5075/api/live/history?macAddress=$mac&limit=100"
$history.readings | Format-Table recordedAtUtc, valueKind, floatValue, integerValue, booleanValue
```

Canonical MACs key a `Dictionary<string, DeviceSnapshot>`, with an ID-to-MAC dictionary for GUID ingestion routes. Lookups have expected O(1) complexity; the HTTP/database response time is not claimed to be constant. Run collection regression tests from the repository root:

```powershell
dotnet test .\SmartX.sln -c Release --no-build --no-restore --filter 'FullyQualifiedName~SmartX.Tests.Live'
```

The live tests cover registry lookup, initialization, chronological ordering, equal-timestamp records, out-of-order arrivals and bounded retention. Recent history uses `SortedDictionary<DateTimeOffset, List<TelemetrySnapshot>>`; each timestamp bucket preserves separate packet IDs. The newest recorded reading remains current when an older reading arrives later.

Recent memory defaults to **2,000 readings globally**, with a maximum response limit of **500**. Full paged SQL history remains available in Telemetry. Queue and recent-history limits are configured separately in `src/SmartX.Api/appsettings.json`.

## Connection health and command rules

Connection health uses **actual gateway contact**, including heartbeats, rather than the sensor's recorded measurement timestamp. Default thresholds are:

| Time since gateway contact | State |
|---|---|
| Less than 30 seconds | Connected |
| 30 seconds to less than 90 seconds | Stale |
| 90 seconds or more | Disconnected |
| No known gateway contact | Unknown |

The monitor scans every 5 seconds; browser operational health refreshes periodically. Recorded time and gateway last-seen time are displayed separately. A device with no readings retains a missing value, not an invented zero. Invalid reading totals can overlap connection states. Seeded historical readings are not evidence of current gateway connectivity.

Commands require a connected Boolean Actuator with a valid known state. Only acknowledged, durably committed manual commands enter the Undo Stack. Undo targets the latest eligible successful command and validates current device state and availability before reverting it. The eligible Stack is reconstructed from SQL command history after restart. This is a simulated ESP32 acknowledgement, not a physical hardware claim.

## Learned actions

The recommendation engine groups distinct persisted searches/views and successful manual commands by telemetry context, target and action. It learns conditional frequencies, requiring at least **three matching observations** and **60% confidence**. Ranking uses confidence multiplied by `log(1 + support)`; only the highest-ranked action per device is returned. Failed commands and Undo do not train successful manual-action patterns.

Suggested Actions shows evidence counts, confidence and contextual reasons. Command recommendations require operator approval. Inspection recommendations open the device. Unavailable targets and commands that would repeat the current actuator state are excluded. This is a frequency-based learning algorithm, not a trained neural model or a promise to predict future faults.

## Attachments and diagnostic notes

Incident labels and diagnostic notes persist against the incident. Per-sensor attachments support configuration files (`.json`, `.txt`, `.csv`, `.pdf`), deployment photos (`.png`, `.jpg`, `.jpeg`) and hardware logs (`.log`, `.txt`, `.csv`). Maximum file size is **5 MB**. Metadata is stored in SQL; file contents use protected local storage with generated names and controlled download/delete routes. File selections are not retained across module unmounts.

## Assessed implementation

| Concept | Implementation |
|---|---|
| Part 1 generics, operators, arrays/lists and recursion | `TelemetryPacket<T>`, `PowerReading`, raw batch processing and deployment hierarchy validation |
| FIFO and priority queues | `src/SmartX.Application/Operations/TelemetryWorkQueue.cs` |
| Command Stack and durable Undo | `src/SmartX.Infrastructure/Operations/CommandService.cs` |
| MAC dictionaries and timestamp-sorted recent history | `src/SmartX.Application/Live/LiveTelemetryStore.cs` |
| Unique active incidents and connection lifecycle | `src/SmartX.Application/Operations/IncidentTracker.cs` |
| Learned recommendations | `src/SmartX.Application/Operations/SuggestionEngine.cs` |
| Real-time operational UI | `src/SmartX.Client/src/features/commands/CommandStreamWorkspace.jsx` |

## API entry points

| Purpose | Route |
|---|---|
| Registration/directory | `/api/sensors` |
| Deployment locations | `/api/deployment-nodes` |
| Native telemetry | `/api/telemetry/float`, `/integer`, `/boolean` |
| Atomic mixed batches | `/api/telemetry/bulk` |
| Full sensor history | `/api/telemetry/sensors/{sensorId}` |
| Live MAC registry/history | `/api/live/devices`, `/api/live/devices/{macAddress}`, `/api/live/history` |
| Operational snapshot | `/api/operations/dashboard` |
| Commands and Undo | `/api/operations/commands`, `/api/operations/undo` |
| Heartbeat | `/api/operations/heartbeat/{sensorId}` |
| Operator behaviour | `/api/operations/interactions` |

Use the OpenAPI document for full contracts. Typed telemetry must contain an explicit `value`; missing/null values are rejected, while native `0` and `false` are accepted.

## Verification and submission

The Windows verification reported on **8 October 2026** completed **210 backend tests, 16 frontend unit tests and 15 rendered integration tests**, with no failures or skips. Release build with warnings as errors, frontend lint and production build passed. Following dependency remediation, `npm audit` reported **0 vulnerabilities**. The frontend tests/lint/build were rerun successfully after the lockfile update. Live SQL-backed simulator modes and manual browser workflows passed; see [Testing summary](docs/part2/Testing_Summary.md).

See [Demo checklist](docs/part2/Demo_Checklist.md) for the recording plan. The brief requires GitHub source, an updated README, and the lecturer's requested video or presentation. Keep meaningful genuine commits; do not manufacture contribution history. Network topology remains final-PoE scope. Hardware and Docker are optional.

Before submission, review `git diff --check`, `git status --short` and the staged diff. Keep local `.env`, credentials, SQL database files, uploaded files, `bin`, `obj`, `node_modules` and `dist` out of source commits. Push the final reviewed changes, verify the remote commit, and record its hash alongside test evidence and the demo URL. The current test results are not yet tied to a supplied final commit hash.

## Practical limits and troubleshooting

- One API process owns the live cache and queues. Direct SQL edits or another API writer require cache reinitialization; distributed gateway coordination is outside this assignment.
- The React UI uses asynchronous polling and requests rather than WebSockets. Browser reload resets workspace choices; server records persist.
- Simulators supply telemetry through HTTP; the UI does not include a manual telemetry-entry form.
- If an extracted patch appears ignored by incremental builds, force the Release rebuild shown above before using `--no-build`.
- If SQL startup fails, check LocalDB availability and the configured connection string. Development startup applies migrations automatically; an optional EF CLI workflow requires `dotnet-ef` version 10.
- If a command is disabled, confirm Actuator category, **On or off** value type, a valid Boolean reading, and current gateway heartbeats. A recorded reading alone does not keep a device connected indefinitely.
- If the API is unavailable, navigation remains usable; restart it and use Retry. Confirm the API URL, client `.env` and exact CORS origin.
- The original `Send-DemoTelemetry.ps1` predates gateway-contact health. Old recorded timestamps alone do not create Stale status; use the lifecycle scenario or stop heartbeats to demonstrate timeouts.
