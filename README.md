# Smart-X IoT Mesh Ecosystem - Parts 1 and 2

Student number: **ST10435415**
Module: **PROG7312 / AAPD7112 - Programming 3B / Advanced Application Development**

Smart-X is a simulated IoT data-ingestion and validation gateway for a South African smart hydroponic facility. It registers typed sensors, receives and validates high-volume telemetry, stores data in SQL Server, manages sensor attachments, and helps a developer identify abnormal readings and disconnected devices.

## Current implementation scope

The startup interface presents the three planned Smart-X pillars:

| Pillar | Current status |
|---|---|
| Sensor Data Ingestion and Telemetry | Implemented and enabled |
| Real-Time Command Stream and History | Enabled; Package 1 workspace shell and retained UI choices |
| Network Topology and Mesh Routing | Visible but disabled until the final PoE |

The complete Part 1 ingestion module remains in place. Part 2 is being added in seven packages. Package 1 enables navigation and the Command Stream shell; its operational data and controls are completed in later packages. Topology remains final-PoE work.

## Main capabilities

- Register sensors using a unique MAC address, friendly name, category, measured property, telemetry type, unit, expected range and Node-level deployment location.
- Receive strongly typed `float`, `int` and `bool` readings through separate API routes.
- Validate readings against the registered sensor type and expected range.
- Store sensors, deployment nodes, telemetry and attachment metadata in SQL Server through Entity Framework Core.
- Process mixed telemetry batches atomically, with a maximum of 500 readings per request.
- Upload configuration files, deployment photographs and hardware logs against a specific sensor.
- Display sensor search, filtering, profiles, telemetry history, timestamps, units and connection status.
- Present connected, stale, disconnected, no-data and invalid fleet information.
- Plot telemetry with a labelled expected-range band and selectable anomaly markers.
- Return structured HTTP errors with suitable `400`, `404`, `409` and `413` statuses.

## Architecture

```text
React client (:5173)
        |
        | HTTP/JSON and multipart requests
        v
ASP.NET Core .NET 10 API (:5075)
        |
        +--> Application and domain rules
        |
        +--> Entity Framework Core --> SQL Server LocalDB
        |
        +--> Protected local attachment storage
```

The React client never connects directly to SQL Server or the attachment directory. Controllers handle HTTP concerns, application/domain code owns processing rules, and infrastructure code owns database and file-system access.

## Repository structure

| Path | Responsibility |
|---|---|
| `SmartX.sln` | Groups the .NET projects |
| `src/SmartX.Domain` | Sensors, deployment hierarchy, typed telemetry and core validation rules |
| `src/SmartX.Application` | Telemetry processing and application-level behaviour |
| `src/SmartX.Infrastructure` | EF Core persistence, SQL Server seed data and attachment storage |
| `src/SmartX.Api` | Controller-based HTTP API, configuration, filters and OpenAPI |
| `src/SmartX.Client` | React 19 and Vite dashboard |
| `tests/SmartX.Tests` | Domain, application, infrastructure and API tests |

## Prerequisites

Install the following before running the project:

- .NET 10 SDK
- Node.js 20.19+ or 22.12+ and npm (Package 1 checked with Node.js 24.19.0)
- SQL Server Express LocalDB (`MSSQLLocalDB`) or another configured SQL Server instance
- EF Core command-line tool version 10 (`dotnet-ef`)
- Git

Confirm the main tools:

```powershell
dotnet --version
dotnet ef --version
node --version
npm.cmd --version
```

If `npm` is blocked by the PowerShell execution policy, use `npm.cmd` as shown throughout this guide. Changing the machine execution policy is not required.

## Configuration

The API uses normal ASP.NET Core configuration. Development settings and the SQL Server connection are read from the API configuration files and environment. Do not commit passwords, production connection strings or local secrets.

The React client obtains its API base address from its Vite environment configuration. The supplied development configuration targets:

```text
http://localhost:5075
```

From the repository root, create the client configuration if it is missing:

```powershell
if (-not (Test-Path .\src\SmartX.Client\.env)) {
    Copy-Item .\src\SmartX.Client\.env.example .\src\SmartX.Client\.env
}
```

Restart Vite after changing this value. Local `.env` files must remain excluded from Git. API CORS currently permits `http://localhost:5173`; use that exact client origin. A Production API run needs an explicit `ConnectionStrings__SmartXDatabase` value because the supplied database configuration is Development-only.

## Restore dependencies

From the repository root:

```powershell
dotnet restore .\SmartX.sln

cd .\src\SmartX.Client
npm.cmd install
cd ..\..
```

For a reproducible install using the committed lock file, `npm.cmd ci` may be used instead of `npm.cmd install`.

## Database setup

Install the EF Core CLI if it is not already available:

```powershell
dotnet tool install --global dotnet-ef --version 10.*
```

Apply the committed migration from the repository root:

```powershell
dotnet ef database update `
  --project .\src\SmartX.Infrastructure `
  --startup-project .\src\SmartX.Api
```

The initial migration is `20260902131429_InitialCreate`. When the API starts in Development, it checks the database and creates the deterministic hydroponic seed dataset only when the deployment hierarchy is empty.

The baseline seed contains:

- 9 hierarchical deployment locations
- 12 sensors across Environmental, Power Consumption and Actuator categories
- 3,456 typed telemetry readings
- deliberate invalid readings for anomaly demonstration

## Run the application

### 1. Start the API

From the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\src\SmartX.Api
```

The development API listens on:

```text
http://localhost:5075
```

Health endpoint:

```text
GET http://localhost:5075/api/health
```

OpenAPI document:

```text
GET http://localhost:5075/openapi/v1.json
```

The project exposes the OpenAPI JSON document but does not install a separate Swagger UI at `/swagger`.

### 2. Start the React client

Open a second PowerShell window:

```powershell
cd C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE\src\SmartX.Client
npm.cmd run dev
```

Open the Vite address, normally:

```text
http://localhost:5173
```

## Build and test

Run the .NET Release build and automated suite:

```powershell
cd C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE
dotnet build .\SmartX.sln -c Release -warnaserror
dotnet test .\SmartX.sln -c Release --no-restore
```

Run frontend checks:

```powershell
cd .\src\SmartX.Client
npm.cmd run lint
npm.cmd run build
```

Package 1 adds frontend tests:

```powershell
npm.cmd test
npm.cmd run test:integration
```

These check the state model and render the actual React modules through Vite in JSDOM with controlled HTTP responses. They do not require SQL Server. Optional Chromium checks also exercise the UI in a browser:

```powershell
npx.cmd playwright install chromium
npm.cmd run test:browser
```

The browser tests start their own Vite instance on `127.0.0.1:4174` and intercept device API responses. Keep that port free. Neither test suite is a replacement for the live SQL-backed regression checklist.

## Historical Part 1 Phase 6 results

The following results were observed on 5 September 2026:

| Check | Verified result |
|---|---|
| Complete automated suite | 137 passed, 0 failed, 0 skipped |
| Targeted batch/security/status suite | 71 passed, 0 failed, 0 skipped |
| React lint | Passed with no reported errors |
| Vite production build | Passed; 41 modules transformed in 441 ms |
| Valid sensor registration | Persisted and reopened successfully |
| Duplicate MAC address | Rejected with `409 Conflict` |
| Valid float reading | Stored as `Float` and marked valid |
| Out-of-range float reading | Stored and marked invalid with an explanation |
| Duplicate telemetry packet | Rejected with `409 Conflict` |
| Wrong telemetry type | Rejected with `400 Bad Request` |
| Unknown sensor | Rejected with `404 Not Found` |
| Maximum bulk request | 500/500 readings stored atomically |
| Oversized bulk request | 501 readings rejected; no partial save |
| Live 500-reading measurement | 259,943-byte request in 1,829 ms, about 273 readings/second |
| Allowed configuration attachment | Uploaded and listed against the correct sensor |
| Unsupported `.exe` attachment | Rejected with an allowed-extension message |
| API unavailable | React displayed an actionable connection error |
| API recovery | Refresh succeeded without restarting React |

Performance values are a local development measurement, not a universal benchmark or service-level guarantee.

## API overview

Important route groups include:

| Area | Typical routes |
|---|---|
| Health | `GET /api/health` |
| Sensors | Register, list and retrieve sensor profiles under `/api/sensors` |
| Deployment | Retrieve valid deployment locations under `/api/deployment-nodes` |
| Typed telemetry | `POST /api/Telemetry/float`, `/integer`, `/boolean` |
| Bulk telemetry | `POST /api/Telemetry/bulk` |
| History | `GET /api/Telemetry/sensors/{sensorId}` |
| Diagnostics | `/api/telemetry/diagnostics/*` |
| Attachments | Sensor-specific list, upload, download and delete routes |

Refer to `/openapi/v1.json` for the authoritative request/response schemas.

## Engagement strategy

The implemented strategy matches the Task 1 research choice: **Real-Time Anomaly Visualisation and Contextual Drill-Down**.

The workflow is deliberately anomaly-first:

1. The directory starts with fleet-health totals.
2. Users identify connected, stale, disconnected and invalid sensors without reading raw rows.
3. A sensor profile shows its typed history, unit and connection status.
4. The compact trend chart overlays the configured expected range.
5. Invalid points remain distinguishable through markers, labels and text, rather than colour alone.
6. Selecting an anomaly exposes its value, timestamp, expected range and validation reason.

Connection thresholds are server-owned:

- Connected: latest reading no older than 5 minutes
- Stale: older than 5 minutes but no older than 15 minutes
- Disconnected: older than 15 minutes
- No data: registered sensor with no reading

Invalid status is diagnostic and may overlap a connection state.

## Attachment security

Attachments are linked to an existing sensor and are not stored in the React client or SQL binary columns. SQL Server retains useful metadata while file contents are stored through protected server-side storage.

Controls include:

- 5 MB maximum attachment size
- 6 MB global request ceiling
- purpose-specific extension and MIME-type checks
- empty-file rejection
- existing-sensor and ownership checks
- generated safe storage names
- no trust in user-supplied paths
- controlled download and deletion
- structured errors for invalid or oversized requests

Supported purposes shown by the client:

| Purpose | Extensions |
|---|---|
| Configuration file | `.json`, `.txt`, `.csv`, `.pdf` |
| Deployment photo | `.png`, `.jpg`, `.jpeg` |
| Hardware log | `.log`, `.txt`, `.csv` |

## Assessed-concept traceability

| Assessed concept | Smart-X use | Main implementation | Main tests |
|---|---|---|---|
| Generics | Preserves native float, integer and boolean telemetry without reducing values to strings | `TelemetryPacket<T>` in `SmartX.Domain`; type guard and processing in `SmartX.Application` | `TelemetryPacketTests`, `TelemetryPacketTypeGuardTests`, `TelemetryBatchProcessingTests` |
| Operator overloading | Aggregates simultaneous smart-meter loads meaningfully | `PowerReading` and its overloaded `+` operator in `SmartX.Domain` | `PowerReadingTests` |
| Advanced arrays and lists | Inspects variable-length raw sequential batches with a jagged array, then returns accepted packets in a `List<T>` | `RawTelemetryBatchProcessor` and telemetry batch processing in `SmartX.Application` | `RawTelemetryBatchProcessorTests`, `TelemetryBatchProcessingTests` |
| Recursion | Validates Facility -> Zone -> Sub-zone -> Node hierarchies with a base case, depth protection and cycle detection | `DeploymentHierarchyValidator` in `SmartX.Domain` | `DeploymentHierarchyValidatorTests` |
| Data structures and algorithms | Uses dictionaries/sets for efficient identity and cycle checks, lists for ordered results and paged database queries for history | Domain validation, batch processing, controllers and EF Core queries | Domain, application, bulk API and model test groups |

Pointer types and unsafe code are deliberately excluded because the managed, generic implementation meets the Part 1 requirements without introducing unnecessary memory-safety risk.

## Data structures and complexity

Let `n` be the number of readings in a batch and `h` the deployment-tree height.

| Operation | Structure/approach | Expected complexity | Reason |
|---|---|---:|---|
| Inspect a raw batch | Jagged array traversal | `O(n)` time | Every supplied reading must be inspected once |
| Collect accepted readings | `List<T>` | `O(1)` amortised append, `O(n)` space | Preserves ordered validated output efficiently |
| Resolve unique IDs | Hash-based lookup/set | `O(1)` average lookup, `O(n)` space | Efficient duplicate detection and sensor resolution |
| Validate hierarchy | Recursive depth-first traversal | `O(n)` time, `O(h)` call stack | Each node is visited while the active path detects cycles |
| Retrieve recent history | Indexed, ordered, paged EF query | Approximately `O(log n + k)` with a suitable index | Locates the ordered range and returns only page size `k` |
| Find latest reading per sensor | Grouped/index-supported database query | Dependent on SQL plan; avoids loading all history into React | Keeps fleet evaluation on the server |

Actual SQL performance depends on row counts, indexes, machine resources and the generated query plan.

## User workflow

1. Open Sensor Data Ingestion and Telemetry from the startup page.
2. Review facility-health totals.
3. Search or filter the sensor directory.
4. Open a sensor to inspect its configuration, deployment location and history.
5. Register a new sensor against a valid Node-level location when required.
6. Submit typed readings through the API or bulk ingestion route.
7. Refresh the sensor to view timestamps, units, validation state and trend.
8. Select abnormal points for contextual investigation.
9. Upload supporting configuration, deployment or hardware-log evidence.

## Troubleshooting

### `npm.ps1` cannot be loaded

PowerShell may block script wrappers. Use:

```powershell
npm.cmd run dev
npm.cmd run lint
npm.cmd run build
```

### React reports that it cannot connect to the API

- Confirm the API is running on `http://localhost:5075`.
- Call `http://localhost:5075/api/health` directly.
- Check the Vite API base URL and CORS configuration.
- Restart the API and use the client retry/refresh action.

### Database connection or migration fails

- Confirm SQL Server LocalDB is installed and running.
- Check the development connection string.
- Run `dotnet ef database update` with both project arguments shown above.
- Confirm the installed `dotnet-ef` major version matches .NET/EF Core 10.

### `/swagger` returns 404

This project exposes OpenAPI JSON without a separate Swagger UI. Use:

```text
http://localhost:5075/openapi/v1.json
```

### HTTPS redirection warning during local HTTP development

The API may warn that it cannot determine an HTTPS port while using the configured HTTP development URL. This does not prevent `http://localhost:5075` from serving the application.

### Seed data does not appear

The Development seed is designed to avoid duplicating an existing hierarchy. Confirm the environment is `Development` and check whether the configured database already contains deployment records.

## Current limitations

- Devices and telemetry are simulated; physical ESP32 hardware is optional and not included.
- The dashboard uses request/refresh interactions rather than WebSockets.
- The React client does not provide a manual telemetry-ingestion form; simulated devices use the API.
- Command Stream navigation and its shell are enabled. Live registry/history, processing queues, incidents, commands/Undo, complete dashboard and learned suggestions are developed in Packages 2-7.
- Network Topology and Mesh Routing remains deferred to the final PoE.
- Local attachment storage is suitable for this assessment environment and can later be replaced by managed cloud/object storage.

## Submission checks

Before submitting:

```powershell
cd C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE

dotnet build .\SmartX.sln -c Release -warnaserror
dotnet test .\SmartX.sln -c Release --no-restore

cd .\src\SmartX.Client
npm.cmd run lint
npm.cmd run build

cd ..\..
git diff --check
git status --short
```

Confirm that no secrets, local `.env` files, database files, uploaded test files, `bin`, `obj`, `node_modules` or generated `dist` output are staged. Commit meaningful source and documentation changes, push them to GitHub, and verify that the remote repository contains the final commit.


## Part 2 Package 1: navigation and stability

Use the consistent **Home / Telemetry / Command Stream** navigation at any depth in the Telemetry module, including sensor details and registration. Module changes use the existing App-based view selection and React rendering, without a router dependency or browser reload.

A small React Context/reducer above the modules retains:

- Telemetry search, category, location, selected sensor, registration draft and submission status.
- History validity, page and selected anomaly independently for each sensor.
- Command Stream search, category, alert filter, selected-device slot and history-view choice.

Modules unmount when inactive. Their read requests are aborted, and late results are ignored before updating state. On return, the saved view choices are reused and API data is requested again. A submitted registration continues through the normal API request; its pending status prevents a second submission on returning, and success updates the Telemetry selection without changing the currently active module.

State lasts for the current application session. A browser reload resets UI choices. Uploaded files remain on the server; an unsubmitted browser file selection is not retained across module changes.

The shell contains Live devices, Selected device, Active alerts, Processing status, Command history and Telemetry history sections. Its filters currently retain choices; data filtering becomes functional when live data is connected. Command and Undo controls are visibly unavailable until the command workflow is implemented. No operational counters or live readings are invented for this phase.

Three focused Part 1 fixes accompany the navigation:

1. Invalid chart markers now open their investigation details by click or keyboard.
2. Null expected bounds no longer become a misleading zero-to-zero chart band.
3. Switching registration to Boolean ignores disabled numeric range fields, consistent with the existing null-bound API contract.

### Package 1 verification, 7 October 2026

Package 1 was rebuilt and reverified from `prog7312-IoT(3).zip` for `C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE`. This is the restart baseline for the seven-package sequence. No earlier Part 2 package is needed.

| Check | Result in the package preparation environment |
|---|---|
| Frontend state/registration tests | 11 passed |
| Rendered React navigation/stability tests | 9 passed |
| Existing .NET regression suite | 137 passed, 0 failed, 0 skipped |
| .NET 10 Release solution build with warnings as errors | Passed; 0 warnings, 0 errors |
| ESLint | Passed |
| Vite production build | Passed; 47 modules |
| API startup and health | Started in Production; 100 health requests using 8 concurrent callers succeeded; healthy after navigation tests |
| Chromium browser suite | Included, not executed: browser download unavailable in this environment |
| SQL Server/LocalDB end-to-end UI regression | Requires the local verification checklist |

The .NET checks used SDK 10.0.401 and serial MSBuild (`-m:1 -p:UseSharedCompilation=false`) because compiler/build-server IPC was unavailable here. Backend source, API contracts, migrations and existing backend tests are byte-for-byte unchanged from the supplied Part 1 source. The live API smoke check used its health endpoint without SQL I/O; the rendered React tests use controlled device responses. These checks do not claim live SQL ingestion, attachments or load testing.

See [BLAKE_1_INSTRUCTIONS.md](BLAKE_1_INSTRUCTIONS.md) for installation, files and five recommended commit groups, and [Package 1 verification and traceability](docs/part2/Package_1_Verification.md) for the exact demo and local regression checklist.

### Remaining Part 2 packages

| Package | Scope |
|---|---|
| `blake_2` | MAC-keyed live registry and timestamp-ordered recent history |
| `blake_3` | FIFO/priority telemetry buffers and background processing |
| `blake_4` | Unique incidents, recovery and heartbeat/connection lifecycle |
| `blake_5` | Persisted commands, acknowledgement and Stack-backed Undo |
| `blake_6` | Complete operational dashboard and deterministic simulator scenarios |
| `blake_7` | Persisted user history, learned suggestions and final traceability/regression |

No database migration is required for Package 1. Existing database and seed setup remains as documented above. Part 2's 30-mark navigation criterion concerns the final integrated application; this package establishes its navigation/state foundation, rather than claiming a completed 100-mark Part 2 submission.
