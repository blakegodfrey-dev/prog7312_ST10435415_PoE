# Smart-X IoT Mesh Ecosystem

**PROG7312 / AAPD7112 - Final PoE | ST10435415**

Smart-X simulates a hydroponic facility with ESP32-style environmental sensors, power meters, pumps, valves and relays. React provides three connected workspaces; an ASP.NET Core .NET 10 API validates telemetry, processes commands and analyses routes; SQL Server/EF Core stores durable records. Custom AVL trees serve historical ranges, while a weighted graph, BFS and Prim with a min-heap analyse the simulated mesh.

| Workspace | Main use |
| --- | --- |
| Sensor Data Ingestion and Telemetry | Register typed sensors; investigate health, anomalies, date-filtered history and attachments. |
| Real-Time Command Stream and History | Inspect live activity, FIFO/critical processing, incident labels/notes, commands, Undo and history-driven suggestions. |
| Network Topology and Mesh Routing | View the gateway/links, find a sensor by MAC/ID, trace its tree route, simulate failures and open linked history. |

**Verified milestone, 10 November 2026:** Pillars 1-4 are signed off. Windows checks passed **242 backend, 28 unit, 22 integration and 13 browser tests**, builds/lint and both dependency audits. Pillar 5 supplies the documentation below; final report/recommendations and feedback/submission preparation remain Pillars 6/7. **The demo video has not been recorded. It is planned after Pillar 7 work; its URL is pending.**

## Documentation

- [Setup and use](#setup-and-use)
- [Demonstrations](#demonstrations)
- [Structures and algorithms](#structures-and-algorithms)
- [Verified results and performance](#verified-results-and-performance)
- [API entry points](#api-entry-points)
- [Troubleshooting and limits](#troubleshooting-and-limits)
- [Implementation guide](docs/Implementation_Guide.md): architecture, exact code excerpts, complexity, concurrency and integration.
- [Evidence map](docs/Evidence_Map.md): assessed requirements linked to code, API/UI, tests and observed results.
- [Verification summary](docs/Verification_Summary.md): dated Windows results and benchmark provenance.

## Setup and use

### Prerequisites and configuration

Use Windows with SQL Server Express LocalDB (`MSSQLLocalDB`), or another explicitly configured SQL Server; .NET **10 SDK**; Node **22.13+ in the 22.x line or 24+**; npm and Git. The reported Windows run used .NET SDK **10.0.400** and npm **11.13.0**. The committed lockfile supplies exact client dependencies. Physical radios and Docker are optional and are not part of these run instructions.

Clone the submitted GitHub repository or open its existing checkout. Run commands from the folder containing **`SmartX.sln`**. When applying a patch, copy its contents into that root, retaining `.git`, client `.env`, connection configuration, uploads and local `artifacts` manifests. API `Contracts` and `Configuration` belong directly under `src/SmartX.Api`, alongside `Controllers`.

In PowerShell, use `npm.cmd` to avoid the `npm.ps1` execution-policy issue. Stop API/Vite/watch before a rebuild to avoid locked files.

```powershell
dotnet restore .\SmartX.sln
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
dotnet build .\SmartX.sln -c Release --no-restore -t:Rebuild -warnaserror
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

Push-Location .\src\SmartX.Client
try {
    npm.cmd ci
    if ($LASTEXITCODE -ne 0) { throw "Client installation failed." }
    if (-not (Test-Path .env)) { Copy-Item .env.example .env }
} finally { Pop-Location }
```

The client `.env` must contain `VITE_API_BASE_URL=http://localhost:5075`. It is local configuration, not a secret to commit. Restart Vite after changing it. The API's configured CORS origin is **`http://localhost:5173`**; use that exact client origin.

### Terminal 1 - API and SQL

From the repository root, use a test database because the demonstration scripts retain sensors, readings, interactions and commands:

```powershell
$env:ConnectionStrings__SmartXDatabase = "Server=(localdb)\MSSQLLocalDB;Database=SmartXPillar1TestDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
dotnet run --project .\src\SmartX.Api -c Release --no-build --launch-profile http
```

The `http` profile selects Development and listens on **5075**. Development startup applies the committed EF migrations. If the deployment table is empty, seeding creates **9 deployment locations, 12 sensors and 3,456 typed historical readings** (288 per sensor, five-minute intervals), including deliberate anomalies. If any deployment nodes already exist, seeding is skipped; it does not replace an existing facility. Seeded historical records do not establish current gateway contact. Runtime simulator readings enter through the API.

Use your existing database to preserve the earlier 10,007-reading demonstration. A fresh database receives the baseline seed, not your local demonstration manifests or uploads. For another SQL Server, change the environment connection string. Production needs deliberate database/configuration preparation; Development migration/seeding and demonstration controls must not be assumed there.

Health: <http://localhost:5075/api/health>. Development OpenAPI JSON: <http://localhost:5075/openapi/v1.json>. A Swagger UI is not installed.

### Terminal 2 - client

From the repository root:

```powershell
Push-Location .\src\SmartX.Client
npm.cmd run dev -- --host localhost --port 5173 --strictPort
# After stopping Vite with Ctrl+C:
Pop-Location
```

Open **http://localhost:5173**. Home and the navigation expose all three workspaces. Choices survive module navigation within the current browser application; reloading creates fresh workspace choices while server records remain.

### Automated verification

Stop the API, Vite and watcher. Install the existing Playwright version's Chromium once if necessary, then run the final checks from the repository root:

```powershell
Push-Location .\src\SmartX.Client
npx.cmd playwright install chromium
Pop-Location
node .\scripts\verify-final.mjs
```

The command stops at the first failed stage and saves `summary.json`, per-stage stdout/stderr, backend TRX and `avl-benchmark.csv` under `artifacts/pillar4-checks/<timestamp>/`. It runs restore/Release rebuild, backend tests, transitive NuGet audit, `npm ci`/full npm audit, all client suites, lint/build and the 10k/50k/100k AVL benchmark. It inspects audit JSON and discovered test counts as well as exit codes. Registries must be reachable; browser tests start their own Vite server on **127.0.0.1:4174**, which must be free.

The integration/browser suites use controlled API responses. Backend HTTP tests use test persistence providers, plus SQL translation/model checks. They complement the separate real SQL demonstrations; not every automated test uses SQL Server. See [the final-check guide](START_HERE_PILLAR_4.md) for individual/live steps and [the dated dependency notes](docs/pillar4/Dependency_Notes.md) for the scoped minimatch override and zero-known-findings result.

## Demonstrations

Start the Development API before these commands. Run scripts from the repository root in terminal 3. Keep local manifests; do not commit machine-specific data/configuration.

| Goal | Command / behaviour |
| --- | --- |
| Create the identifiable Node A sample and 10k history | `node scripts/demo-history.mjs http://localhost:5075 seed 10000` - creates new demonstration sensors/readings through HTTP and saves `artifacts/pillar1-demo.json`. Run once for a fresh demonstration. |
| Verify retained history | `node scripts/demo-history.mjs http://localhost:5075 verify` - uses the saved manifest. For restart evidence, stop/restart the actual API against the same database before verifying; the script cannot restart it for you. Do not reseed first. |
| Exercise graph/BFS/Prim and failures | `node scripts/demo-mesh.mjs http://localhost:5075 demo` - validates reachability, forest costs against an independent oracle, MAC/ID paths, node/link isolation, gateway failure and recovery; saves `artifacts/pillar2-demo.json`. |
| Recheck saved mesh | `node scripts/demo-mesh.mjs http://localhost:5075 verify` - refreshes actual API heartbeats and checks the saved baseline/route. |
| Keep mesh demo sensors online | `node scripts/demo-mesh.mjs http://localhost:5075 watch` - genuine API heartbeat requests every ten seconds for saved mesh sensor IDs. Ctrl+C stops it; it does not clear failure overrides. |
| Clear interrupted routing failures | `node scripts/demo-mesh.mjs http://localhost:5075 reset` - clears only process-local routing overrides; actual gateway health still controls availability. |

For the verified original sample, search **`Pillar1 Node A moisture 11FD0444`** and set **8 November 2026, 14:00:00-16:00:00 SAST**. It returns five records: 45%, 48%, invalid 10%, 52%, 55%, with both 15:00 readings retained. The table is newest first; equal chart instants share X. A new seed prints its own date/name/IDs; use those rather than assuming this earlier manifest exists in a fresh clone.

In Network, select **Air Humidity** by name, MAC or ID. With fresh contacts its default tree path goes through Air Temperature to the gateway. Inspect a declared link/node, simulate a failure and clear it; the returned route recomputes through available links or explicitly reports no route. The selected sensor opens shared Telemetry history and its separate Command Stream context. Unlinked demonstration sensors remain isolated; registration does not invent wireless neighbours. Use text health labels and keyboard Enter/Space as well as graph colours.

### Original operational regression

The full operational scenario includes genuine disconnection, so allow approximately **93 seconds** for the default timeout phase. To observe a FIFO backlog, stop the API and start it in terminal 1 with the existing test database and a temporary 100 ms processing delay:

```powershell
$env:Operations__ProcessingDelayMilliseconds = "100"
dotnet run --project .\src\SmartX.Api -c Release --no-build --launch-profile http
```

In terminal 3:

```powershell
node .\scripts\simulate-operations.mjs http://localhost:5075 all
```

It registers three scenario sensors and verifies native float/int/bool, acknowledged command/Undo, type rejection, critical-priority bypass while ordinary work waits, unique incidents, timeout/heartbeat/range recovery and changing learned-action evidence. Default queue capacity is 5,000; the backlog demonstration requires at least 100. Individual `normal`, `priority`, `lifecycle` and `learning` modes are available.

Afterwards stop the API, remove the temporary delay in its terminal and restart against the same database:

```powershell
Remove-Item Env:\Operations__ProcessingDelayMilliseconds -ErrorAction SilentlyContinue
dotnet run --project .\src\SmartX.Api -c Release --no-build --launch-profile http
```

## Structures and algorithms

The [implementation guide](docs/Implementation_Guide.md) gives the complete explanation, source excerpts and qualified complexity table. These structures drive API results and UI behaviour:

| Structure / algorithm | Role and reason |
| --- | --- |
| Per-sensor custom AVL | Keeps chronological history balanced even for sorted/out-of-order arrival. Composite UTC-time/packet-ID keys preserve duplicates. Subtree counts enable inclusive ranges and paged retrieval without a full-history scan. |
| Weighted undirected adjacency graph | Represents registered/configured ESP32 vertices and actual declared wireless links. Unavailable endpoints/links are excluded from usable adjacency. |
| BFS with `Queue<Guid>` and visited set | Computes gateway reachability through available neighbours; visited-on-enqueue prevents cycle repetition. |
| Lazy Prim with `PriorityQueue` min-heap | Chooses the cheapest frontier link, skips stale cycle entries, and starts another component when needed. Returns an actual minimum spanning forest. |
| Dictionaries, queues, Stack and sets from Parts 1/2 | Preserve MAC/ID lookup, recent ordered history, ordinary FIFO/critical priority, successful-command Undo and unique recoverable incidents. |

For example, the actual AVL query counts the interval using two ranks before collecting its page:

<!-- source: src/SmartX.Application/History/TelemetryAvlTree.cs; excerpt: avl-ranks -->
```csharp
var lower = fromUtc.HasValue ? CountBefore(fromUtc.Value, false, isValid, ref visited) : 0;
var upper = toUtc.HasValue ? CountBefore(toUtc.Value, true, isValid, ref visited) : Size(_root, isValid);
var matched = upper - lower;
```

The Prim loop selects available cut edges from its heap; `Visit` then enqueues the next frontier:

<!-- source: src/SmartX.Application/Mesh/MeshAlgorithms.cs; excerpt: prim-cut-selection -->
```csharp
while (frontier.TryDequeue(out var edge, out _))
{
    token.ThrowIfCancellationRequested();
    dequeues++;
    if (visited.Contains(edge.SourceId) && visited.Contains(edge.TargetId)) continue; // Stale/cycle edge.
    var next = visited.Contains(edge.SourceId) ? edge.TargetId : edge.SourceId;
    selected.Add(edge);
    edges.Add(edge.Id);
    cost += edge.PowerCostMilliJoules;
    Visit(next);
}
```

These are excerpts from [TelemetryAvlTree](src/SmartX.Application/History/TelemetryAvlTree.cs) and [MeshAlgorithms](src/SmartX.Application/Mesh/MeshAlgorithms.cs), not standalone programs. With `n` readings, unfiltered tree insertion costs O(log n) and a page of `p` results costs O(log n + p); the sparse validity-filtered case is qualified in the guide. BFS core traversal is O(V + E); this implementation also sorts output IDs. Lazy Prim's heap work is O(V + E log(E + 1)), with additional deterministic root ordering. Graph construction/sorting and HTTP/SQL costs are counted separately.

**Routing meaning:** an MST minimises the sum of selected tree weights, not every sensor's individual route cost or hop count. Paths are reconstructed from selected forest edges. No shortest-path feature is claimed. Wireless weights use `(0.05 + 0.0005 × distance²) / deliveryProbability` simulated mJ per delivered packet, with explicitly assumed symmetric links, packet size and retries. These are pedagogical estimates, not physical RF/energy measurements.

## Verified results and performance

Latest complete supplied Windows run: **10 November 2026**, evidence folder `artifacts/pillar4-checks/2026-11-10T09-22-34-942Z-27616/`.

| Check | Observed result |
| --- | --- |
| Release rebuild, warnings as errors; lint; production build | Passed; Release build reported zero warnings/errors. |
| Backend / unit / rendered integration / browser tests | **242 / 28 / 22 / 13 passed**, no failures/skips. |
| Full npm audit / transitive NuGet audit | **Zero known findings**, dated 10 November 2026. |
| Live SQL operational modes | Normal, priority, lifecycle with the real 90-second threshold, and learning passed. |
| Retained Node A history | **10,007 indexed readings**, five exact matches, AVL height **16**, **41 visits**. |
| Configured mesh component | Twelve sensors plus gateway; minimum tree cost **1.002 simulated mJ**. Heap **18 enqueues / 18 dequeues / maximum frontier 7**; failure/recovery checks passed. |

Latest laptop algorithm measurements, transcribed from the same final-run output:

| Readings | Height | Matches | Visits | Median AVL query µs | Mean linear count scan µs |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 10,000 | 14 | 121 | 157 | 4.300 | 91.287 |
| 50,000 | 16 | 121 | 161 | 5.300 | 575.877 |
| 100,000 | 17 | 121 | 167 | 6.400 | 984.507 |

Raw [Windows CSV](docs/pillar4/Benchmarks_Windows.csv), [measurement method and provenance](docs/Verification_Summary.md), and [benchmark executable](tools/SmartX.HistoryBenchmarks/Program.cs). Sorted inserts deliberately exercise balancing. A tenfold history increase raises observed visits from 157 to 167 for the same 121 matches; height grows from 14 to 17. Visits can include repeated visits made by boundary/page traversals. Timings exclude SQL, HTTP, hydration and rendering. Query timing is a warm median; the linear comparison only counts matches and uses a mean, so this is illustrative algorithm scaling, not an application-wide speedup claim. JIT/GC/CPU conditions affect times.

## API entry points

| Method / purpose | Route |
| --- | --- |
| Sensor registration/directory and deployment lookup | `POST/GET /api/sensors`; `GET /api/deployment-nodes` |
| Native telemetry; atomic mixed batch | `POST /api/telemetry/float`, `/integer`, `/boolean`, `/bulk` |
| Full AVL history | `GET /api/telemetry/sensors/{sensorId}?fromUtc=...&toUtc=...&isValid=...&page=1&pageSize=100` |
| Mesh snapshot and optional selected route | `GET /api/mesh`, optionally `?nodeId=...` or `?macAddress=...`; optional configured `subnetId` |
| Separate selected tree route | `GET /api/mesh/route?nodeId=...` or `?macAddress=...` |
| Routing failure/reset | `PUT /api/mesh/nodes/{id}/availability`, `PUT /api/mesh/links/{id}/availability`, `DELETE /api/mesh/simulation` |
| Live registry/recent history | `GET /api/live/devices`, `/api/live/devices/{macAddress}`, `/api/live/history` |
| Operational snapshot; commands; Undo | `GET /api/operations/dashboard`; `POST /api/operations/commands`, `/api/operations/undo` |
| Gateway heartbeat; behaviour evidence | `POST /api/operations/heartbeat/{sensorId}`, `/api/operations/interactions` |

Use the Development OpenAPI JSON for complete payloads, responses and remaining attachment/diagnostic routes. Ingestion requires an explicit typed `value`: native `0`/`false` are valid, missing/null are rejected. History bounds are inclusive; omit either for an open range, permit equal endpoints, reject reversed ranges/invalid paging. SAST inputs are interpreted at UTC+02:00 and sent as UTC instants, independently of the browser's timezone. Use ISO timestamps with `Z` or an explicit offset; URL-encode a `+` offset.

## Troubleshooting and limits

| Symptom / boundary | Explanation or action |
| --- | --- |
| Duplicate types / multiple top-level programs on build | API contents may have been copied into `Controllers`. Restore the layout using the original source; retain a backup of misplaced files outside the project. The final-check command detects the known nested entries. |
| LocalDB startup failure | Check the SQL instance and environment connection string. Development startup runs migrations; a fresh deployment table is needed for automatic baseline seeding. |
| Client cannot reach API / CORS failure | Confirm API 5075, `.env`, exact client origin `http://localhost:5173`, and restart Vite after config changes. Navigation/retry remain usable during API errors. |
| HTTP launch reports no HTTPS redirect port | The HTTP development profile does not supply an HTTPS redirect destination. The recorded local HTTP scenario uses 5075; an optional HTTPS profile is available with separately prepared certificate/client/CORS configuration. |
| Nodes become unavailable after a pause | Health uses real gateway contact: Connected <30s, Stale 30-<90s, Disconnected ≥90s (scan every 5s). Mesh permits registered Connected/Stale nodes with known contact. Commands require Connected Boolean actuators with a valid state. Old recorded times are not heartbeats. |
| Clearing failures does not revive a device | Overrides only remove routing availability. A genuinely expired/unregistered device still needs a valid registration/contact; command/Undo state is independent. Overrides reset on process restart. |
| Extra isolated components / Degraded state | Scenario devices without declared links remain unlinked even while online. The forest spans available vertices only; failed gateway/disconnected targets return empty paths and null hop/cost, not invented routes. |
| No learned suggestion | Support must reach 3 and confidence 60% for the active context; unavailable targets/already-satisfied actions are excluded. Suggestions use conditional operator-history frequencies, not a neural or threshold-only predictor. Commands require approval. |
| Recent history shows fewer records | Recent memory is bounded to 2,000 globally, responses up to 500. Full paged Telemetry history uses the separate AVL hydrated from all persisted readings. |
| Attachment upload fails | Maximum 5 MB; configuration `.json/.txt/.csv/.pdf`, photo `.png/.jpg/.jpeg`, hardware log `.log/.txt/.csv`. Persisted metadata/content survive navigation; an unsubmitted browser file selection does not. |
| Incident notes after server restart | Labels/notes are process-local active-incident state. They can survive module navigation while the API runs, but are not rehydrated after API restart. Attachment metadata/content and command/interaction history are separate persisted records. |
| Node `DEP0190` during final checks | Observed non-blocking warning from the Windows npm launcher in `verify-final.mjs`. The supplied run completed; this documentation patch does not alter that launcher. Track it as maintenance rather than a failed application test. |
| Deployment beyond this assignment | One API process owns caches/queues; one subnet/gateway is configured. Direct SQL writers, deletions or multiple replicas need further invalidation/coordination. Full history has O(N) memory cost. Async polling is used, not WebSockets. |

The [evidence map](docs/Evidence_Map.md) separates implemented features from outstanding report, feedback and release items. The final PDF and demo video URL are still pending. Record the actual final GitHub commit and video URL when available; no contributor history, commit hash, recording or upload is inferred from test results.
