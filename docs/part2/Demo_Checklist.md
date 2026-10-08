# Smart-X Part 2 demo checklist

Student **ST10435415**. This plan follows the Part 2 brief and rubric. The supplied documents do not state a mandatory video duration; use any duration/format separately prescribed by the lecturer. A focused recording of about **8–10 minutes**, plus the actual lifecycle wait if shown in full, is a suggested target rather than a requirement.

## Prepare before recording

1. Apply the updated README and retain the final dependency lockfile produced by the successful audit fix.
2. Keep the separate test database and existing test records. Open three terminals: API, frontend, and simulations. Use the README setup commands.
3. Have the Boolean pump available. Its MAC is `02:08:10:26:00:02`; its ID in the verified database is `c7658197-4658-41af-b0b8-9b86a1e9228b`. Resolve by MAC when using another database; do not assume this GUID is portable.
4. Keep only deliberate telemetry/heartbeat producers running. A background process that repeatedly forces pump values can interfere with commands and Undo.
5. Prepare a small hardware log and open the five source files listed below in the editor. Have the recorded passing test output ready.
6. Use readable browser/editor zoom. Avoid displaying secrets or unrelated personal windows. Verify microphone and screen capture with a short practice clip.

## Recording sequence

| Scene | Show | Explain |
|---|---|---|
| Introduction and Home | Three pillars; Telemetry and Command Stream enabled, Topology disabled | A simulated hydroponic IoT facility. Devices send native telemetry to a .NET 10 API backed by SQL Server. Topology is final-PoE scope. |
| Part 1 integration and navigation | Telemetry search, sensor detail, anomaly chart and attachment; switch to Command Stream/Home and return | Part 1 remains functional. Asynchronous module changes retain filters, selected devices and drafts. Browser reload resets UI choices; server data persists. |
| Native values and MAC lookup | Zero power and false pump values; the README's MAC lookup commands | Missing is distinct from zero/false. Canonical MAC Dictionary lookup resolves the registered device. |
| Chronological history | Search `7F704F`, select the meter, choose Selected device readings; show 0/1,000/200 W | A SortedDictionary keeps recent history in timestamp order. Buckets retain distinct IDs with identical timestamps; full history remains in SQL. |
| Commands and Stack Undo | Boolean pump Connected, Set On, successful command history, Undo back to false | Acknowledged manual commands are pushed onto a real Stack only after durable commit. Undo validates current state and device availability. |
| FIFO and priority queue | Run the priority scenario with the temporary delay; show PASS and the Processing status panel | Ordinary telemetry uses FIFO. Critical packets take priority over waiting ordinary packets; an in-flight write completes first. |
| Unique incidents and recovery | Active alert/observation count, lifecycle script output and gateway last-seen; saved diagnostic note | HashSet-backed active keys prevent duplicate episodes. Timeout, heartbeat, normal-reading recovery and recurrence have different meanings. |
| Learned suggestions | Run learning, search its returned suffix, show Suggested Actions and console evidence | Recommendations use persisted searches/views and successful commands in matching contexts, with support and confidence. An inspection recommendation is valid; command recommendations need approval. |
| Verification and conclusion | 210 + 16 + 15 passed, lint/build success and zero audit vulnerabilities; repository page | The tested implementation has 241 passing cases plus live SQL/API and manual browser checks. Provide the actual final repository and demo links. |

Do not claim timing or scale measurements that were not recorded. The brief's wording about fast collections is demonstrated by actual structure use and responsive behaviour; expected O(1) Dictionary lookup does not mean a zero-latency HTTP request.

## Keep the Boolean pump connected for browser commands

Run this in the simulation terminal. It submits one false reading, then only heartbeats, so it will not overwrite manual command states:

```powershell
& {
    $ErrorActionPreference = "Stop"
    $apiBase = "http://localhost:5075"
    $devices = Invoke-RestMethod "$apiBase/api/sensors"
    $matches = @($devices | Where-Object {
        $_.macAddress -eq "02:08:10:26:00:02"
    })
    if ($matches.Count -ne 1) { throw "Expected one registered demo pump." }
    $pump = $matches[0]
    if ($pump.category -ne "Actuator" -or $pump.valueKind -ne "Boolean") {
        throw "Choose an Actuator registered as 'On or off'."
    }
    $pumpId = ([guid]$pump.id).ToString()
    $reading = @{
        id = [guid]::NewGuid().ToString()
        sensorId = $pumpId
        value = $false
        recordedAtUtc = [datetime]::UtcNow.ToString("o")
    } | ConvertTo-Json
    Invoke-RestMethod "$apiBase/api/telemetry/boolean" -Method Post `
        -ContentType "application/json" -Body $reading | Out-Null
    Write-Host "Pump connected; heartbeats running. Ctrl+C stops them."
    while ($true) {
        Invoke-RestMethod "$apiBase/api/operations/heartbeat/$pumpId" `
            -Method Post -ContentType "application/json" -Body "{}" | Out-Null
        Start-Sleep -Seconds 5
    }
}
```

Stop this loop before running another scenario in the same terminal. While it runs, show Set On → Undo → Set On → Set Off in the browser. To demonstrate timeout, stop it and allow at least 90 seconds plus monitor/polling time.

## Priority scene

Use the README's API restart commands for `Operations__ProcessingDelayMilliseconds=100`, then run:

```powershell
node .\scripts\simulate-operations.mjs http://localhost:5075 priority
```

Show the actual PASS output. Search the new suffix to inspect the power meter. The resulting critical incident is resolved by the scenario's recovery reading; show recovery events/history rather than claiming it should stay active. Restore normal processing after this scene.

## Lifecycle scene

```powershell
node .\scripts\simulate-operations.mjs http://localhost:5075 lifecycle
```

During the 90-second wait, search its moisture device in Command Stream if its name is visible. Repeated observations should belong to one OutOfRange incident. The scenario then checks Disconnected, heartbeat recovery, normal-range recovery and a new recurrent episode. If editing out the wait, label the elapsed time honestly and show the script's completed assertions.

## Learned-action scene

```powershell
node .\scripts\simulate-operations.mjs http://localhost:5075 learning
```

Immediately inspect the newly returned suffix while its gateway contact is fresh. Show the reasoning/support/confidence on Suggested Actions. This script deliberately adds conflicting behaviour after the initial learned command. Its final top recommendation can therefore be **Inspect device** rather than **Approve: set On**. Explain that the engine adapts to evidence; do not present this as a failure. Previously recorded output showed command support 3/confidence 1, followed by top action view/confidence 1. Unavailable devices are excluded, so waiting for disconnection can make suggestions disappear.

## Brief source-code evidence

Keep these files ready and briefly point to the actual structures:

- `src/SmartX.Application/Operations/TelemetryWorkQueue.cs`: `Queue<T>` and `PriorityQueue<TElement, TPriority>`; critical-before-normal dequeue.
- `src/SmartX.Infrastructure/Operations/CommandService.cs`: `Stack<CommandHistoryEntry>`; push only after commit and pop after successful Undo.
- `src/SmartX.Application/Live/LiveTelemetryStore.cs`: MAC Dictionary and timestamp SortedDictionary.
- `src/SmartX.Application/Operations/IncidentTracker.cs`: HashSet-backed active incident identity and recovery.
- `src/SmartX.Application/Operations/SuggestionEngine.cs`: distinct historical evidence, conditional counts, minimum support/confidence and ranking.

## Finish submission

- Commit only reviewed source/documentation changes, including the updated lockfile. Record the actual final commit hash and verify it on GitHub.
- The brief asks for more than 20 commits; the rubric's top GitHub band describes 25+ meaningful commits and collaboration evidence. Present genuine repository history and contributors accurately.
- Include the complete API/client source and updated README. Keep any existing Docker files if present; Docker and physical hardware are optional in the supplied brief.
- Upload the video or use the lecturer-approved presentation format. Test the final link in a signed-out/private browser to confirm the intended viewer can access it.
- Add the actual repository and video links to the submission. Retain dated test output and screenshots, and note the tested commit. This pack does not publish or push anything.
