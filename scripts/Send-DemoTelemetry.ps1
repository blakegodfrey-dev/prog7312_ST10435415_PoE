param(
    [ValidateSet("Snapshot", "Continuous")]
    [string]$Mode = "Snapshot",

    [string]$ApiBaseUrl = "http://localhost:5075",

    [ValidateRange(10, 300)]
    [int]$IntervalSeconds = 30
)

$ErrorActionPreference = "Stop"
$random = [System.Random]::new()

function Get-TelemetryRoute {
    param($ValueKind)

    switch ([string]$ValueKind) {
        "1"       { return "float" }
        "Float"   { return "float" }
        "2"       { return "integer" }
        "Integer" { return "integer" }
        "3"       { return "boolean" }
        "Boolean" { return "boolean" }
        default   { throw "Unsupported telemetry value kind: $ValueKind" }
    }
}

function Get-NormalValue {
    param($Sensor)

    $kind = [string]$Sensor.valueKind

    if ($kind -in @("3", "Boolean")) {
        return ($random.Next(0, 2) -eq 1)
    }

    $hasMinimum = $null -ne $Sensor.expectedMinimum
    $hasMaximum = $null -ne $Sensor.expectedMaximum

    if ($kind -in @("2", "Integer")) {
        if ($hasMinimum -and $hasMaximum) {
            $minimum = [int][Math]::Ceiling(
                [double]$Sensor.expectedMinimum
            )

            $maximum = [int][Math]::Floor(
                [double]$Sensor.expectedMaximum
            )

            if ($maximum -ge $minimum) {
                return $random.Next($minimum, $maximum + 1)
            }
        }

        return $random.Next(1, 101)
    }

    if ($hasMinimum -and $hasMaximum) {
        $minimum = [double]$Sensor.expectedMinimum
        $maximum = [double]$Sensor.expectedMaximum
        $value = $minimum + ($random.NextDouble() * ($maximum - $minimum))

        return [single][Math]::Round($value, 2)
    }

    return [single][Math]::Round(
        10 + ($random.NextDouble() * 10),
        2
    )
}

function Get-InvalidValue {
    param($Sensor)

    $kind = [string]$Sensor.valueKind

    if ($kind -notin @("1", "Float", "2", "Integer")) {
        throw "An invalid demonstration value requires a numeric sensor."
    }

    if ($null -ne $Sensor.expectedMaximum) {
        $maximum = [double]$Sensor.expectedMaximum

        if ($null -ne $Sensor.expectedMinimum) {
            $range = [Math]::Abs(
                $maximum - [double]$Sensor.expectedMinimum
            )
        }
        else {
            $range = 10
        }

        $invalidValue = $maximum + [Math]::Max(1, $range * 0.25)
    }
    else {
        $invalidValue = 999999
    }

    if ($kind -in @("2", "Integer")) {
        return [int][Math]::Ceiling($invalidValue)
    }

    return [single][Math]::Round($invalidValue, 2)
}

function Send-Telemetry {
    param(
        $Sensor,
        [DateTimeOffset]$Timestamp,
        [bool]$MakeInvalid
    )

    $route = Get-TelemetryRoute $Sensor.valueKind

    if ($MakeInvalid) {
        $value = Get-InvalidValue $Sensor
    }
    else {
        $value = Get-NormalValue $Sensor
    }

    $body = @{
        id            = [Guid]::NewGuid()
        sensorId      = $Sensor.id
        value         = $value
        recordedAtUtc = $Timestamp.ToString("O")
        receivedAtUtc = $Timestamp.ToString("O")
    } | ConvertTo-Json

    Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/telemetry/$route" `
        -Method Post `
        -ContentType "application/json" `
        -Body $body | Out-Null

    $condition = if ($MakeInvalid) { "INVALID" } else { "valid" }

    Write-Host (
        "Sent {0} {1} reading for {2}" -f
        $condition,
        $route,
        $Sensor.friendlyName
    )
}

function Send-DemonstrationSnapshot {
    $sensors = @(
        Invoke-RestMethod -Uri "$ApiBaseUrl/api/sensors"
    ) | Sort-Object friendlyName

    if ($sensors.Count -lt 4) {
        throw "At least four registered sensors are required."
    }

    # Reserve one sensor as stale and two as disconnected.
    $connectedCount = $sensors.Count - 3

    $connectedSensors = @(
        $sensors | Select-Object -First $connectedCount
    )

    $staleSensor = $sensors[$connectedCount]

    $disconnectedSensors = @(
        $sensors | Select-Object -Skip ($connectedCount + 1)
    )

    $invalidSensor = $connectedSensors |
        Where-Object {
            ([string]$_.valueKind) -in @(
                "1",
                "Float",
                "2",
                "Integer"
            )
        } |
        Select-Object -First 1

    if ($null -eq $invalidSensor) {
        throw "No numeric sensor is available for the invalid reading."
    }

    $now = [DateTimeOffset]::UtcNow
    $staleTimestamp = $now.AddMinutes(-8)

    foreach ($sensor in $connectedSensors) {
        $makeInvalid = $sensor.id -eq $invalidSensor.id

        Send-Telemetry `
            -Sensor $sensor `
            -Timestamp $now `
            -MakeInvalid $makeInvalid
    }

    Send-Telemetry `
        -Sensor $staleSensor `
        -Timestamp $staleTimestamp `
        -MakeInvalid $false

    foreach ($sensor in $disconnectedSensors) {
        Write-Host (
            "Left disconnected: {0}" -f $sensor.friendlyName
        )
    }

    $health = Invoke-RestMethod `
        -Uri "$ApiBaseUrl/api/telemetry/diagnostics/health-summary"

    Write-Host ""
    Write-Host "Dashboard health snapshot"
    Write-Host "-------------------------"
    Write-Host "Total:        $($health.totalSensorCount)"
    Write-Host "Connected:    $($health.connectedSensorCount)"
    Write-Host "Stale:        $($health.staleSensorCount)"
    Write-Host "Disconnected: $($health.disconnectedSensorCount)"
    Write-Host "No data:      $($health.noDataSensorCount)"
    Write-Host "Invalid:      $($health.invalidLatestReadingCount)"
}

if ($Mode -eq "Snapshot") {
    Send-DemonstrationSnapshot
    return
}

Write-Host (
    "Continuous simulator started. Sending every {0} seconds." -f
    $IntervalSeconds
)
Write-Host "Press Ctrl+C to stop."

while ($true) {
    Send-DemonstrationSnapshot
    Write-Host ""
    Write-Host "Waiting $IntervalSeconds seconds..."
    Start-Sleep -Seconds $IntervalSeconds
}