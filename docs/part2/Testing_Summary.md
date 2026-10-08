# Smart-X Part 2 testing summary

Date: **8 October 2026** (South Africa). Student: **ST10435415**.

These results combine supplied Windows terminal output and the user's manual browser confirmations. They describe the tested development setup; they are not claims of physical ESP32 operation or production load capacity. A final Git commit hash has not yet been supplied.

## Automated results

| Check | Reported result |
|---|---|
| .NET Release rebuild, warnings as errors | Passed |
| Backend tests | 210 passed; 0 failed; 0 skipped |
| Frontend unit tests | 16 passed; 0 failed; 0 skipped |
| Rendered React integration tests | 15 passed; 0 failed; 0 skipped |
| ESLint | Passed |
| Vite production build | Passed |
| npm audit after remediation | 0 vulnerabilities |

Total executed automated cases in these suites: **241**. Frontend unit/integration tests, lint and build passed again after the dependency update. The optional `test:browser` suite is not included in this total. The .NET test suite uses configured test providers; live SQL Server behaviour was checked separately below.

## Live HTTP and SQL-backed scenarios

The API ran locally at `http://localhost:5075`, with a separate LocalDB database configured for testing. The frontend ran at `http://localhost:5173`.

| Scenario | Result and retained device suffix |
|---|---|
| Normal typed readings, commands/Undo, type rejection/recovery | PASS; `FC10CC` |
| Incident uniqueness, 90-second timeout, heartbeat/range recovery and recurrence | PASS; `D10EE5` |
| Learned evidence and revised recommendation | PASS; `36C747`; command support 3/confidence 1, revised top action view/confidence 1 |
| Critical priority bypass with temporary 100 ms worker delay | PASS; `7F704F` |

The revised learning result demonstrates a changed action; it does not demonstrate decreased confidence in this specific run. Critical priority bypasses waiting ordinary traffic, not the transaction already running.

## Manual browser confirmations

- Simulation devices could be searched, selected and inspected.
- Meter history retained 0, 1,000 and 200 W in chronological order.
- Command Stream search, selected device and history view survived module navigation.
- Empty registration produced validation errors; a draft survived navigation; registration persisted after refresh.
- A corrected Boolean Actuator, **Browser Test Pump Boolean**, used MAC `02:08:10:26:00:02`. The earlier `Browser Test Pump` had been registered with a numeric type and was not used to claim Boolean command success.
- The Boolean pump accepted native false telemetry, stayed Connected on heartbeats, and passed Set On, Undo, Set On and Set Off through the browser.
- Stopping heartbeats produced Stale/Disconnected status and disabled live command controls.
- Stopping the API displayed an error while navigation remained usable; restart/retry recovered the UI and retained server records.
- Incident label/note survived refresh.
- Hardware-log attachment upload, refresh persistence, download content, cancellation of deletion and confirmed deletion passed.

The first manual PowerShell helper returned an invalid sensor selection; the corrected helper selected exactly one matching MAC and validated its Boolean type before submitting. This was a test-helper issue, followed by a separate device configuration correction.

## Submission evidence still to record

Retain the terminal outputs and useful screenshots. Record the final Git commit, confirm it is pushed to the submission repository, and link the final video/presentation. This summary does not substitute for raw logs or independently verify a future commit.
