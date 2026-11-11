<#
.SYNOPSIS
Starts the local Smart-X API and client from one console. Ctrl+C stops both.
.EXAMPLE
.\Start-SmartX.ps1 -CheckOnly
.EXAMPLE
.\Start-SmartX.ps1
.EXAMPLE
.\Start-SmartX.ps1 -SkipPrepare
#>
[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$SkipPrepare
)

$ErrorActionPreference = 'Stop'
$smartXNode = Get-Command node -CommandType Application -ErrorAction SilentlyContinue
if (-not $smartXNode) {
    throw 'Node is not on PATH. Install Node 22.13+ in the 22.x line or Node 24+, then open a new PowerShell console.'
}

$smartXEntry = Join-Path $PSScriptRoot 'scripts/start-smartx.mjs'
if (-not (Test-Path -LiteralPath $smartXEntry -PathType Leaf)) {
    throw 'Missing scripts/start-smartx.mjs. Copy the whole patch into the folder containing SmartX.sln.'
}
$smartXArguments = @()
if ($CheckOnly) { $smartXArguments += '--check' }
if ($SkipPrepare) { $smartXArguments += '--skip-prepare' }
& $smartXNode.Source $smartXEntry @smartXArguments
if ($LASTEXITCODE -ne 0) {
    throw 'Smart-X stopped with an error. Review the message above; no existing API/client process was stopped by the launcher.'
}
