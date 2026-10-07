# Smart-X Package 2: verification and lookup-structure traceability

## Baseline and scope

Input: `209f83e0-9060-4a7d-9fbd-00737f45befa.zip`, rooted at `prog7312_ST10435415_PoE`. Package 1's source and verification document match the previously delivered overlay. The upload omits its root instruction file; Package 2 does not depend on that file or add it back.

Target: `C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE`.

The implementation follows the live-registry and ordered-history sections of `Work_Report(2).md` and the Package 2 plan. The official Smart-X brief and Part 2 rubric take priority. Primary criterion: **Lookup Structures: Dictionaries and Key-Value Maps [15 marks]**. The complete dashboard, streaming worker and later integration still matter to the final rubric assessment; no numerical mark or latency result is assumed.

No EF model, migration, seed dataset, existing HTTP contract, frontend file or dependency changes. The existing typed ingestion routes now use cached device configurations and publish committed readings after SQL saves.

## Implementation and evidence

| Requirement | Implementation | Evidence |
|---|---|---|
| Canonical MAC keys | Shared domain `MacAddressNormalizer`; `Dictionary<string, DeviceSnapshot>` | Lowercase/whitespace equivalence, invalid formats, conflicting MAC tests |
| Lookup in real ingestion | GUID-to-MAC index, then the MAC dictionary; detached `Sensor` configuration for original validators | Single/bulk integration tests and actual HTTP ingestion |
| Existing registered devices | Scoped SQL hydrator around the shared singleton store | No-data devices, latest values outside the recent window, received-time and restart tests |
| Ordered recent history | `SortedDictionary<DateTimeOffset, List<TelemetrySnapshot>>` | Out-of-order, equal-time, timezone-equivalence and API timeline tests |
| Preserve equal timestamps | Separate reading IDs in each timestamp bucket | Both same-device and cross-device equal-time tests |
| Bounded memory | Global reading-count capacity, oldest-first eviction, bounded ID index | Old-arrival eviction, large single-time bucket, concurrent publication tests |
| Keep full history | Unchanged SQL history endpoint | Evicted readings still returned by the full-history endpoint |
| Native typed values | Nullable float/integer/Boolean columns; original generic routes and validators | Zero/false, mixed-type, invalid-reading flags and JSON tests |
| SQL precedes cache publication | Publication only after successful single/bulk/registration saves | Failed saves, invalid batches, failed registration and duplicate HTTP request checks |
| Safe shared access | Short collection locks; asynchronous initialization semaphore outside SQL I/O | Parallel writes, one-loader initialization, failed-load retry, writes during hydration, snapshot-isolation tests |
| Service lifetime | Singleton store with scoped EF reader/publisher | Cross-scope identity and controller activation test; real HTTP host |
| SQL provider compatibility | Reusable actual hydration queries | SQL Server `ToQueryString()` checks for latest-per-device, received-time aggregate and bounded recent selection |

Latest value uses recorded UTC time, then received UTC time, then canonical reading-ID text. This deterministic tie rule is also used by SQL hydration. `lastReceivedAtUtc` is separately the maximum persisted received timestamp. It is not a heartbeat or gateway-owned last-seen signal.

The live history window is **global**, not a separate 2,000-reading allowance for every device. A busy device can push a quiet device's readings out of the window; the quiet device still retains its independent latest snapshot. Request limits choose the newest matching retained readings and return them chronologically. Full SQL history remains available.

## Complexity

Let `D` be registered devices, `H` retained readings, `T` distinct retained timestamps and `B` readings in one timestamp bucket. `H` is bounded by configuration.

| Operation | Expected cost | Details |
|---|---|---|
| MAC lookup | Average `O(1)` | Canonical key then hash lookup; hashing worst cases are not guaranteed constant |
| Existing GUID ingestion lookup | Average `O(1)` | ID-to-MAC dictionary then MAC dictionary |
| Resolve `K` distinct bulk devices | Average `O(K)` | No repeated linear sensor search |
| Add a reading | `O(log T + B)` | Tree lookup plus binary position search and possible list shifting |
| Evict one oldest reading | `O(log T + B)` | Earliest timestamp traversal/removal and list shifting |
| Get filtered recent timeline | `O(H)` time/temporary space | Walks already-ordered bounded buckets; no new chronological sort |
| Return sorted device directory | `O(D log D)` time | Defensive snapshot sorted by friendly name |
| Persistent cache space | `O(D + H)` | Two identity dictionaries, current snapshots, timestamp buckets and bounded reading-ID index |
| Cold hydration | SQL-plan dependent | Server selects one latest reading per device, receipt aggregates and at most `H` recent rows; full telemetry is not materialized |

The bucket list preserves equal timestamps; inserting into a list is not claimed to be constant time. These are algorithmic costs, not measured service-level guarantees. A cold request performs SQL hydration before dictionary lookup becomes warm.

## Preparation results

Date: 7 October 2026. .NET SDK 10.0.401; Node.js 24.19.0.

| Check | Result |
|---|---|
| Complete backend suite | 184 passed, 0 failed, 0 skipped |
| Existing Part 1 backend tests | All 137 retained and passing |
| Added Package 2 cases | 47 passing |
| Release solution build with warnings as errors | 0 warnings, 0 errors |
| Frontend unit tests | 11 passed |
| Rendered React navigation tests | 9 passed |
| Frontend lint/build | Passed |
| Kestrel HTTP test | Registration, mixed bulk writes, lowercase MAC lookup, chronological bounded history, full-history fallback and request errors passed |
| SQL Server query translation | Actual hydration queries translated without SQL I/O |
| Incremental package/manifest checks | Fresh overlay and payload checksums verified |

The preparation build uses `-m:1 -p:UseSharedCompilation=false` for the local execution environment. Standard Windows commands are supplied in the package instructions.

The HTTP test keeps production registrations/service lifetimes and actual controllers, replacing SQL persistence with EF InMemory. SQL translation verifies generated queries, not server execution. Windows PowerShell and live SQL Server are unavailable here; neither installer/demo execution on Windows nor live SQL end-to-end results are claimed. Package 1 browser layout checks remain local work.

## Local verification

1. Apply the package with its checked installer and run the full build/test blocks in `BLAKE_2_INSTRUCTIONS.md`.
2. Start the real API in Development against the existing database. No migration or database reset is needed.
3. `GET /api/live/devices`: confirm all registered devices appear, including no-data devices; compare names/MACs with `/api/sensors`.
4. Run `scripts/Test-LiveRegistry.ps1` against a quiet API. Its four to six new native readings enter through HTTP, including late arrivals and distinct equal-time IDs.
5. Look up the primary MAC in lowercase. Confirm the same device ID and canonical uppercase MAC return.
6. Check `/api/live/history` timestamps are ascending. Both same-device equal-time IDs must remain present with the normal capacity. `fromUtc`/`toUtc` are inclusive; malformed MACs/ranges/limits return 400, unknown MACs return 404.
7. Restart the API. Latest device values and the recent window rebuild from committed SQL records. Current values remain distinct from maximum received time.
8. For a small-window demonstration, stop the API, set `$env:LiveTelemetry__RecentHistoryCapacity = '3'`, restart and inspect live history. It must contain at most three readings, while `/api/telemetry/sensors/{sensorId}` retains full SQL history. Remove that environment variable and restart to restore the default. The supplied demo script requires capacity at least 6.
9. Repeat the Part 1 registration, typed ingestion, history, health and attachment checklist. Switch modules to confirm Package 1 state retention remains intact.

## Limits and later packages

- One API process, writes through the API. Direct SQL edits or another process require restart/rebuild of this cache. SQL commit and memory publication are not a distributed transaction; SQL remains durable and restart hydration recovers committed data.
- The frontend remains the Package 1 shell. Full live-device filtering, timeline rendering and controls are wired in Package 6.
- Package 3 adds the real FIFO/priority processing buffers and worker.
- Package 4 adds operational incidents and gateway heartbeat/connection evidence. Existing Part 1 recorded-time health behaviour remains.
- Packages 5-7 add commands/Undo, complete dashboard integration and learned suggestions in the agreed order.
