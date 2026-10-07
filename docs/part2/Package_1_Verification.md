# Smart-X Package 1 - Verification and rubric traceability

## Scope and sources

`blake_1` extends the uploaded `prog7312-IoT(3).zip` Part 1 source. It follows the Main Menu and Navigation section of `Work_Report(2).md` and the seven-package plan. The official `Part_2_Rubric.pdf` and `Smart-X_PoE_Instructions.md` take priority. `Part_1_Report.pdf` and `Task1(1).pdf` inform the preserved functionality and focused anomaly/validation corrections.

The sequence restarts at Package 1 for `C:\Dev\prog7312-IoT_2\prog7312_ST10435415_PoE`. All 18 existing files replaced by this overlay matched the uploaded baseline before implementation. The installer uses checksums calculated from this archive, and the checks below were rerun against the corrected upload.

The architecture remains React, ASP.NET Core .NET 10, SQL Server/EF Core and the existing layered .NET solution. No backend file, migration, seed dataset or public API contract changes in this phase.

## Traceability

| Requirement | Implementation | Evidence |
|---|---|---|
| Part 1 remains available | Existing `SensorDirectory`, details, health, history, registration and attachments; App routes to the same module | Existing 137 backend tests; rendered UI regression tests; local checklist below |
| Enable Command Stream | `src/SmartX.Client/src/App.jsx`, `features/commands/CommandStreamWorkspace.jsx` | Main-menu and rendered navigation tests |
| Topology visible, disabled | Existing third App pillar with disabled Final PoE button | State test disallows unsupported view; rendered main-menu test |
| Retain state across Home/module changes | `state/workspaceState.js`, provider, context and hook; Telemetry components consume shared choices | State and rendered round-trip tests preserve filters, selected profile, validity, history page and anomaly |
| Consistent navigation at all module depths | `components/ModuleNavigation.jsx`, outside conditional modules | Tests switch from directory, profile and registration |
| Asynchronous navigation and safe late responses | Existing async API layer; read hooks abort on cleanup and guard result/error updates | Superseded request and repeated switching tests; fake transport deliberately resolves after abort |
| Preserve meaningful in-flight registration | Shared draft, pending/error state in the provider and `useSensorRegistration` | In-flight test proves one POST, pending status on return, successful profile selection and no forced module change |
| Continue Task1 contextual drill-down | Corrected invalid-marker guard; shared selected anomaly; omitted range when bounds are null | Click and Enter tests; history/anomaly round-trip test |
| Responsive layout | Extended `App.css`, two-column shell becoming one column on narrow screens | Chromium mobile test included; visual/local verification pending |

This phase primarily targets Main Menu Navigation and Active Pillar State [30]. The collection and prediction marks belong to later phases. The Command Stream is intentionally a shell at this point.

## Checks executed during preparation

Date: 7 October 2026.

| Check | Outcome |
|---|---|
| Original Part 1 frontend lint and build before changes | Passed; 41 modules in original build |
| Modified client `npm test` | 11 passed, 0 failed |
| Modified client `npm run test:integration` | 9 passed, 0 failed |
| Modified client `npm run lint` | Passed |
| Modified client `npm run build` | Passed; 47 modules |
| Recommended commit groups 1, 2 and 3 applied incrementally | Each source snapshot built successfully |
| Full overlay applied to a fresh Part 1 source copy | All 30 output checksums matched; lint, unit and rendered integration suites passed |
| `.NET Release build -warnaserror -m:1 -p:UseSharedCompilation=false` | Passed; 0 warnings, 0 errors |
| `.NET Release test -m:1 -p:UseSharedCompilation=false` | 137 passed, 0 failed, 0 skipped |
| Production API smoke check | Started successfully with explicit non-secret connection configuration; health requests completed without SQL I/O |
| Concurrent health check | 100/100 returned HTTP 200 and Healthy, with 8 concurrent callers |
| API health while rendered navigation tests ran | Real health endpoint used by App; remained healthy after all 9 UI tests |

The frontend integration suite loads the actual App and components through Vite and renders them in React StrictMode/JSDOM. Sensor, history, health-summary and registration requests use deterministic HTTP responses. The optional live-health setting connects only App's health request to a running API. JSDOM verifies DOM/state/event behaviour; it is not a layout engine or a real-browser measurement.

The original README's September measurements are retained as historical evidence. They are not new Package 1 results.

## Checks still needed on the user's PC

- Chromium suite: provided but not executed here because the browser download was unavailable. Install Chromium and run `npm.cmd run test:browser`.
- Real SQL Server/LocalDB-backed ingestion, registration and attachment persistence.
- Visual layout and keyboard navigation on a normal desktop viewport and a narrow viewport.
- PowerShell installer execution: script is reviewed and its manifests are validated here; Windows PowerShell is unavailable in this environment.

No browser, SQL-backed end-to-end or Windows installer pass is claimed.

## Exact navigation demonstration

1. Run the API in Development against the existing database and start Vite at `http://localhost:5173`.
2. Home must show Telemetry and Command Stream as enabled; Network Topology remains disabled and says Coming in Final PoE.
3. Open Telemetry. Enter `Nutrient`, choose Environmental and select the applicable deployment location.
4. Open Nutrient pH. Set Reading status to Invalid only. If enough readings exist, go to page 2. Otherwise use the available page and retain the validity selection.
5. Select an invalid chart marker; confirm its reason appears. Keyboard users can focus the marker and press Enter or Space.
6. Click Command Stream in the shared navigation. Enter `pump`, choose Actuator, set Active alerts and choose Selected device readings.
7. Click Home, then Telemetry. The selected profile, validity, page and selected anomaly should return when that reading remains on the returned page.
8. Use Back to sensors. Search, category and location must remain unchanged.
9. Return to Command Stream. Its independent choices must remain unchanged.
10. Use browser developer tools to throttle requests, then repeatedly switch modules. Navigation must remain usable; canceled old reads must not replace newer data or show an obsolete failure.

A chart selection is retained as a reading ID. If that reading no longer belongs to the refreshed page, the detail panel is absent rather than showing stale data. Changing history validity or page intentionally clears the old anomaly selection.

## Part 1 regression checklist

| Area | Local verification | Expected result |
|---|---|---|
| Directory | Search by name and MAC; change category/location; no-result case | Correct API-filtered results and actionable empty state |
| Profile/history | Open a sensor, page/filter history, switch away/back | Correct profile and saved UI choices, refreshed API data |
| Anomaly | Click and keyboard-select invalid marker | Diagnostic reason and reading shown |
| Missing ranges | Open a numeric sensor without both bounds | No fabricated 0-0 expected band |
| Registration | Register a new unused MAC at a valid Node location; reopen | Persisted profile, success message, canonical MAC |
| Boolean registration | Enter a reversed numeric range, then switch to Boolean | Disabled numeric bounds do not prevent submission; API receives null bounds |
| Pending registration | Use throttling; submit once, switch modules and return | Busy state remains; no second submit; result belongs to Telemetry |
| Duplicate MAC | Repeat a registered MAC | Existing 409 error remains visible |
| Typed telemetry | Submit float, integer and Boolean packets through the existing API/demo script | Original typed ingestion, validation and persistence retained |
| Invalid ingestion | Wrong type, unknown sensor, duplicate packet, oversized bulk request | Existing structured errors and atomic-save behaviour retained |
| Health | Refresh health; reopen profile after new traffic | Existing health and timestamp behaviour retained |
| Attachment | Upload allowed config; reject `.exe`; list/download/delete | Server-linked files and existing validation retained |
| API failure/recovery | Stop API; navigate; restart and retry the affected panel | Error UI remains usable; retry succeeds |
| App state lifetime | Reload browser | Default UI choices return; persisted SQL data remains |

For write operations, leaving a view does not undo a submitted server operation. Confirm the persisted result before retrying an uncertain network failure. File-picker contents and local panel error text are not guaranteed to survive module changes; saved uploads do.

## Work deferred deliberately

- `blake_2`: registry hydration and refresh, dictionary lookup, ordered bounded history and its APIs.
- `blake_3`: telemetry buffers, worker, priority bypass and persistence consistency.
- `blake_4`: incident episodes, HashSet uniqueness/recovery, actual last-seen/heartbeat evidence, silent-device checks and missing-reading gaps.
- `blake_5`: commands, actuator acknowledgement, durable command history and Stack Undo.
- `blake_6`: wire the shell into working live data, search/filter, selected-device controls, counters and deterministic demos.
- `blake_7`: persisted interactions, context statistics, learned suggestions, final documentation and complete integration.

The existing Part 1 health calculation still uses recorded telemetry time. Package 1 does not claim to add gateway heartbeat tracking or complete the wider engagement strategy. No Part 1 resubmission or amended assessment marks are assumed.
