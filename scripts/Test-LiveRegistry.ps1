[CmdletBinding()]
param([string]$ApiBaseUrl = 'http://localhost:5075')
$ErrorActionPreference = 'Stop'
$api = $ApiBaseUrl.TrimEnd('/')

# This is a simulation: new readings are submitted through the original HTTP
# bulk route and persisted. No database rows are deleted or edited directly.
$devices = @(Invoke-RestMethod -Uri "$api/api/live/devices")
if ($devices.Count -eq 0) { throw 'Register at least one sensor before running this demonstration.' }
$initialHistory = Invoke-RestMethod -Uri "$api/api/live/history?limit=1"
if ([int]$initialHistory.capacity -lt 6) { throw 'This demo needs a recent-history capacity of at least 6. Use automated tests for smaller windows.' }
$primary = $devices | Where-Object { $_.valueKind -eq 'Integer' } | Select-Object -First 1
if ($null -eq $primary) { $primary = $devices[0] }
$start = [DateTimeOffset]::UtcNow.AddSeconds(-40)

function New-DemoReading($Device, [DateTimeOffset]$RecordedAt) {
    $reading = [ordered]@{
        id = [Guid]::NewGuid().ToString()
        sensorId = [string]$Device.id
        valueKind = [string]$Device.valueKind
        floatValue = $null
        integerValue = $null
        booleanValue = $null
        recordedAtUtc = $RecordedAt.ToString('O')
        receivedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $numericValue = 20.0
    if ($null -ne $Device.expectedMinimum -and $null -ne $Device.expectedMaximum) {
        $numericValue = ([double]$Device.expectedMinimum + [double]$Device.expectedMaximum) / 2.0
    }
    switch ([string]$Device.valueKind) {
        'Float' { $reading.floatValue = [single]$numericValue }
        'Integer' { $reading.integerValue = [int][Math]::Floor($numericValue) }
        'Boolean' { $reading.booleanValue = $false }
        default { throw "Unsupported native telemetry type: $($Device.valueKind)" }
    }
    return [pscustomobject]$reading
}

# Deliberately deliver 20s, 30s, 10s, 20s: an out-of-order arrival and two
# distinct readings for the same sensor with exactly the same recorded time.
$readings = @(
    New-DemoReading $primary ($start.AddSeconds(20))
    New-DemoReading $primary ($start.AddSeconds(30))
    New-DemoReading $primary ($start.AddSeconds(10))
    New-DemoReading $primary ($start.AddSeconds(20))
)
foreach ($kind in @('Float', 'Boolean')) {
    $extra = $devices | Where-Object { $_.valueKind -eq $kind -and $_.id -ne $primary.id } | Select-Object -First 1
    if ($null -ne $extra) { $readings += New-DemoReading $extra ($start.AddSeconds(20)) }
}
$body = @{ readings = $readings } | ConvertTo-Json -Depth 6
$stored = Invoke-RestMethod -Uri "$api/api/telemetry/bulk" -Method Post -ContentType 'application/json' -Body $body
if ([int]$stored.storedCount -ne $readings.Count) { throw 'The bulk API did not confirm every submitted reading.' }
$encodedMac = [Uri]::EscapeDataString(([string]$primary.macAddress).ToLowerInvariant())
$device = Invoke-RestMethod -Uri "$api/api/live/devices/$encodedMac"
if ($device.id -ne $primary.id) { throw 'Canonical MAC lookup resolved the wrong device.' }
$recent = Invoke-RestMethod -Uri "$api/api/live/history?limit=500"
$byId = @{}
$previous = $null
foreach ($reading in $recent.readings) {
    $recorded = [DateTimeOffset]::Parse($reading.recordedAtUtc)
    if ($null -ne $previous -and $recorded -lt $previous) { throw 'Recent history is not chronologically ordered.' }
    $previous = $recorded
    $byId[[string]$reading.id] = $reading
}
foreach ($reading in $readings) {
    if (-not $byId.ContainsKey([string]$reading.id)) { throw 'A submitted reading is missing from the recent window. Run against a quiet local demo API.' }
}
if ([int]$recent.retainedCount -gt [int]$recent.capacity) { throw 'Recent history exceeded its configured capacity.' }
$full = Invoke-RestMethod -Uri "$api/api/telemetry/sensors/$($primary.id)?pageSize=500"
Write-Host "PASS: $($stored.storedCount) native readings submitted through HTTP and found in ordered recent history."
Write-Host 'PASS: Same-timestamp IDs are preserved and lowercase MAC lookup resolves the registered device.'
Write-Host "Recent window: $($recent.retainedCount)/$($recent.capacity). Primary device full SQL history: $($full.totalCount) readings."
$recent.readings | Where-Object { $byId.ContainsKey([string]$_.id) -and $_.id -in @($readings.id) } |
    Select-Object recordedAtUtc, sensorId, valueKind, floatValue, integerValue, booleanValue, id | Format-Table -AutoSize
