# Smart-X feedback updates

PROG7312 / AAPD7112 Final PoE · ST10435415 · 10 November 2026

This separate ledger records the supplied lecturer feedback and the resulting changes. It supports the brief’s feedback-file requirement and the rubric’s Iterative Feedback Incorporation criterion. Status describes the actual implementation; clarification is not presented as a completed multi-user redesign.

## Feedback received

| Assessment | Feedback supplied |
| --- | --- |
| Part 1 | **85%**. No further Part 1-specific written comment was supplied with this feedback. The mark is retained; no reassessment is claimed. |
| Part 2 | **93%** — “An outstanding and flawless submission”. |
| Additional supplied comment | “Heavy setup and a single global operator. It needs the .NET SDK, Node, SQL Server LocalDB and two terminals, and it has one shared undo stack and history with no per-user identity.” |

The additional comment was supplied alongside the assessment results without a separate part label. It is treated as cross-cutting feedback rather than assigned to an invented Part 1 or Part 2 subcriterion.

## Responses and evidence

| Feedback | Change and files | Verification | Status and limitation |
| --- | --- | --- | --- |
| Heavy setup and two terminals | Added [Start-SmartX.ps1](Start-SmartX.ps1) and [startup supervisor](scripts/start-smartx.mjs), using [owned process management](scripts/lib/local-session.mjs). One command checks Node/npm, the .NET 10 SDK, source layout, SQL configuration/LocalDB instance and ports; restores/builds the API, installs locked client dependencies and starts API/UI in one console. Readiness is checked before printing READY. Existing configuration is used without rewriting connection settings or `.env`. README supplies setup, fast restart and troubleshooting commands. | Nine [launcher tests](scripts/tests/start-smartx.test.mjs) passed here, including real child-server readiness/cancellation, startup failures, occupied-port rejection and missing-SDK failure. A port already in use is rejected without stopping its unrelated listener. Windows steps are in [START_HERE_PILLAR_7.md](START_HERE_PILLAR_7.md). | **Startup workflow simplified; prerequisites retained.** This does not remove the SDK, Node/npm or SQL requirement. Windows PowerShell/LocalDB startup and Ctrl+C cleanup still require the local check; Linux process-fixture tests are not a real-SQL/Windows pass. |
| One global operator, Undo stack and history | Updated [CommandStreamWorkspace.jsx](src/SmartX.Client/src/features/commands/CommandStreamWorkspace.jsx): explicit single-operator notice, **Facility command history**, **Facility-wide Undo entries**, and **Undo latest successful facility command**. Accessible help explains that selected-device filters do not scope Undo. Existing command acknowledgement, durable history, state checks, semaphore and Stack behaviour remain intact. | The existing [integration Undo scenario](src/SmartX.Client/tests/integration/navigation.test.mjs) now filters the displayed device list before Undo and verifies the scope/help plus the validated API request. All 22 rendered integration tests passed. Browser assertions use the updated accessible button name. | **Scope clarified; identity limitation retained.** No accounts, authenticated operator attribution, per-user history, authorisation or per-user Undo were implemented. These remain a future multi-operator extension. Facility telemetry remains shared. |
| Preserve the successful earlier submission | Changes are limited to local startup, operator-facing wording, the corresponding client tests and documentation. Backend/domain/application algorithms, SQL schema, command/Undo implementation, packages and lockfile are unchanged. | 28 client unit tests, 22 rendered integration tests, lint and production build passed here. Earlier Windows backend/SQL evidence is retained separately in [Verification_Summary.md](docs/Verification_Summary.md). | Earlier functions are preserved. No new backend execution or lecturer mark is claimed by this patch. Current browser/Windows limitations are recorded below. |

## Further structural refinements carried into Part 3

These are evidenced project improvements, **not additional quoted lecturer requests**:

- **History:** a per-sensor AVL with composite UTC/packet-ID ordering, subtree counts, balanced insertion and committed SQL hydration/publication addresses duplicate/out-of-order history and selective ranges. See [TelemetryAvlTree](src/SmartX.Application/History/TelemetryAvlTree.cs), [HistoricalTelemetryIndex](src/SmartX.Application/History/HistoricalTelemetryIndex.cs) and [history tests](tests/SmartX.Tests/History/).
- **Routing:** immutable declared adjacency, BFS, lazy Prim/min-heap and valid selected-tree paths handle unavailable nodes, disconnected forests and gateway failure. See [WeightedMeshGraph](src/SmartX.Domain/Mesh/WeightedMeshGraph.cs), [MeshAlgorithms](src/SmartX.Application/Mesh/MeshAlgorithms.cs) and [mesh tests](tests/SmartX.Tests/Mesh/).
- **Integration:** shared workspace state, obsolete-read cancellation and same-analysis graph/route responses preserve navigation and prevent mismatched highlights. The chart uses recorded-time X coordinates. See [the implementation guide](docs/Implementation_Guide.md) and [criterion evidence map](docs/Evidence_Map.md).

## Verification boundary

Current preparation checks used Linux / Node 24.22.1. The 13-case Playwright suite could not execute its scenarios because the installed Chromium process failed during browser startup with `socket() failed: Operation not permitted`. This is recorded as **blocked**, not passed. The same suite and the actual PowerShell/LocalDB launcher must be checked on the Windows machine using the included instructions.

Multi-user identity would require a deliberate authentication/authorisation, persistence and command-ownership design with regression tests. A display name or a browser-specific stack would not resolve that requirement. The current submission preserves the single-operator facility model and makes that boundary explicit.
