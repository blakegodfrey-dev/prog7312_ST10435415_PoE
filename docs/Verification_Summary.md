# Smart-X verification summary

**Status as at 11 November 2026:** Pillars 1-6 and the Pillar 7 feedback/startup changes have passing implementation evidence. Pillar 6 provides the [combined PDF](reports/Smart-X_Final_PoE_Report.pdf) and [editable report](reports/Smart-X_Final_PoE_Report.docx); preserve the author's separately reviewed final report. The [feedback ledger](../Feedback_Updates.md) is prepared and updated with the latest Windows acceptance. Final GitHub publication/history/contributor records and the demo remain external submission checks. **The user will record the video after Pillar 7 work; there is no video URL yet.**

## Provenance and scope

The results below come from the user's supplied Windows PowerShell output and live screenshots, not a newly claimed execution in the preparation environment. The latest complete final-check run is:

```text
artifacts/pillar4-checks/2026-11-11T07-16-32-078Z-5320/
```

The console reports .NET SDK **10.0.400**, npm **11.13.0**, .NET runtime **10.0.11** in earlier local test output, and the current lockfile's Vite **8.2.6**. The exact Windows Node version was not printed in the supplied console log; `summary.json` records it for later report use. No unreported hardware specification is attached to these measurements.

Earlier complete Windows runs on 10 November also passed and had different timings. The tables below consistently use the 11 November completed run; they do not combine the fastest rows across runs. The [10 November Windows CSV](pillar4/Benchmarks_Windows.csv) remains historical evidence. Earlier Linux/test-provider measurements remain in [the original Pillar 4 record](pillar4/Verification.md) and [Linux CSV](pillar4/Benchmarks.csv), separately labelled.

The raw local logs/TRX/summary remain under ignored `artifacts/`; this documentation patch contains the factual summary and an earlier dated [CSV transcription](pillar4/Benchmarks_Windows.csv), not the user's database, local manifests, screenshots or secrets. Screenshots supplied in conversation remain supporting visual observations, not a recording or a committed report asset.

## Automated checks

| Stage | Latest observed Windows result |
| --- | --- |
| Solution restore / Release rebuild, warnings as errors | Passed; **0 build warnings / 0 errors**. |
| Backend tests | **242 passed / 0 failed / 0 skipped**. |
| Separate launcher lifecycle suite | **9 passed / 0 failed / 0 skipped** in a separate supplied Windows command. |
| Client unit | **28 passed / 0 failed / 0 skipped**. |
| Rendered integration | **22 passed / 0 failed / 0 skipped**. |
| Playwright browser | **13 passed / 0 failed / 0 skipped**. |
| ESLint / production client build | Passed; 58 modules transformed. |
| Full npm audit | **0 known vulnerabilities**, all reported severity totals zero. |
| NuGet audit with transitive dependencies | Five solution projects returned; **no vulnerability/problem entries**. |
| Benchmark restore/build | Passed; 0 build warnings/errors. |
| Final-check command | All sixteen stages reached the final **PASS automated final checks**. |

The final-run suites total 305 cases; with the separate 9 launcher cases, **314 passed**. These are separate suites, not 314 independent real-SQL end-to-end scenarios. Client integration/browser tests use controlled API responses. Backend tests include actual HTTP/controller flows using test providers and SQL model/query-translation checks. Real SQL demonstrations below verify the installed local application separately.

Node emitted `DEP0190` for the Windows npm shell launcher, and installation emitted a deprecated `whatwg-encoding` notice. Neither failed the supplied run; they are distinct from the zero C# build-warning count and zero-known-findings audit result. The existing launcher uses fixed npm arguments. The feedback patch does not change or suppress the existing final-check runner notices. Track launcher cleanup as maintenance rather than representing the entire console as warning-free.

## Live SQL and browser observations

| Scenario | Supplied observation |
| --- | --- |
| Typed ingestion and command/Undo | Native float/int/bool, wrong-type rejection/recovery and real acknowledged command/Undo all PASS. |
| Critical priority | Critical power packet completed while normal backlog remained; PASS. The temporary processing delay was part of the demonstration setup. |
| Incident lifecycle | The real configured **90-second** timeout, unique episode, heartbeat recovery, range recovery and recurrence all PASS. |
| Learned suggestions | Cold start had no suggestion; matching history produced command support 3/confidence 1; later evidence revised the top action to view. |
| Node A SAST example | 8 November 2026 14:00-16:00 SAST returned five exact readings from **10,007 retained** packets. Four valid / one invalid; duplicates, out-of-order arrival, inclusive endpoints, sensor isolation, pagination and offset/validation checks PASS. |
| Historical tree evidence | AVL height **16**, **41 visited nodes** for the five-reading verification. This differs from the algorithm-only 121-match benchmark below. |
| Mesh baseline and heap | Twelve configured sensors reach the gateway, forest cost **1.002 simulated mJ**; **18 enqueues / 18 dequeues / max frontier 7**. Four components at that demo instant included an online isolated node. |
| Route/failure/recovery | MAC/ID equivalence, relay failure alternative cost **0.212 simulated mJ**, link alternative, disconnected target, failed gateway and recovery all PASS; saved-manifest verify again returned cost 1.002. |
| Real client | Screenshots show retained command/Undo history, selected sensor/attachments, actual graph and unavailable unlinked target, plus the five-reading date-filtered chart/table. The corrected duplicate 15:00 points share X. User confirmed the application works. |

The original Pillar 1 process restart was previously confirmed and signed off. The latest history script again proves the retained records/IDs at query time; it prints a reminder because the script itself does not stop/restart the API. A new restart is not independently inferred from that line alone. Mesh failure overrides intentionally reset on process restart, while configuration IDs/SQL records remain.

Component counts vary as extra unlinked scenario devices time out. The later UI showing two available components rather than the demo's four is consistent with live availability, not evidence that edges were fabricated or persisted failures claimed. The model uses simulated links/power estimates and HTTP-fed data; no physical radio result is asserted.

## AVL measurements

The following table is transcribed from stage 16 of the **11 November** run. [Benchmarks_Windows.csv](pillar4/Benchmarks_Windows.csv) retains the earlier **10 November** measurements:

| Readings | Height | Build ms | Matches | Visits | Median query µs | Mean linear count scan µs |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 10,000 | 14 | 39.020 | 121 | 157 | 3.800 | 89.821 |
| 50,000 | 16 | 89.442 | 121 | 161 | 5.800 | 554.009 |
| 100,000 | 17 | 83.889 | 121 | 167 | 7.000 | 980.631 |

The [benchmark source](../tools/SmartX.HistoryBenchmarks/Program.cs) inserts sorted arrivals for one sensor, validates invariants and checks exact result IDs against an independent sorted scan. Each query requests the same-size inclusive interval, returning 121 readings. After 100 warmup queries it reports a median of 501 samples. The comparison is a mean of 100 count-only linear scans. SQL, HTTP, hydration and UI rendering are excluded. Build timing includes JIT/GC effects and need not grow monotonically.

The structural result is the clearest scaling evidence: multiplying stored readings by ten changes height from 14 to 17 and visits from 157 to 167 for fixed output size. Reported visits include rank/page traversal visits, possibly revisiting nodes. These timings are local observations, not a guaranteed latency or a fair end-to-end speedup factor between identical workloads. No graph-latency or distributed-load measurement is claimed.

## Reproduce and retain evidence

Use [the root README](../README.md#automated-verification) for the automated command and [Pillar 4 guide](../START_HERE_PILLAR_4.md) for the separate SQL/real-restart procedure. Retain both local demo manifests; a fresh seed creates a different identified scenario and is not proof of persistence of the earlier one.

The [evidence map](Evidence_Map.md) links each implementation claim to its actual files and tests. The final report is prepared in [Pillar 6](pillar6/Verification.md). The separate lecturer-feedback mapping is complete in [Feedback_Updates.md](../Feedback_Updates.md). [Current feedback acceptance](pillar7/Verification.md) records the resolved DLL lock/database mismatch and restored 13/21, 18/18, 1.002 mJ mesh screenshot. Final GitHub URL/hash/contributor checks and video publication remain external records to verify. **Recording is planned after Pillar 7, not marked complete by this verification record.**
