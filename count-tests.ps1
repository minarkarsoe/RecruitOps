<#
.SYNOPSIS
    Counts backend test cases per assembly from a CI log or a local docker build log.

.DESCRIPTION
    Exists because "how many tests actually ran" has been unanswerable three times in this
    repo, and the last attempt to answer it was a Ctrl+F in the GitHub Actions log viewer.
    That viewer is virtualised: it only searches the portion currently rendered, so a match
    count from it is a lower bound with no relationship to the real number. A 5,000-line log
    will happily report "40 results" for something that occurs 117 times.

    This reads the whole file, so its number is the number.

.PARAMETER Path
    A log file. Either:
      - the archive from GitHub Actions (Actions -> the run -> "..." -> Download log archive,
        then unzip; point this at the extracted backend job .txt), or
      - a local build log:
          docker build --target test --progress=plain --no-cache-filter=build,test `
            ./backend 2>&1 | Tee-Object -FilePath build.log

.EXAMPLE
    .\count-tests.ps1 -Path .\build.log

.EXAMPLE
    .\count-tests.ps1 -Path '.\logs\1_Backend — build + test (.NET 10, Docker).txt'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    # What FEATURE-STATUS.md currently claims, so the script can say "as documented" or
    # "documentation is wrong" rather than leaving you to compare two numbers by eye.
    [int]$ExpectedDomain = 39,
    [int]$ExpectedApi = 128
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path)) {
    throw "No such log file: $Path"
}

$lines = Get-Content -LiteralPath $Path

# Counted off the per-test output rather than the runner's summary line, and keyed on the
# assembly namespace. A solution-wide `dotnet test` interleaves two runs' stdout, so an
# aggregate "Total tests: N" cannot be attributed to an assembly — these can.
$domain = ($lines | Select-String -Pattern 'Passed RecruitOps\.Domain\.Tests\.' -SimpleMatch:$false).Count
$api    = ($lines | Select-String -Pattern 'Passed RecruitOps\.Api\.Tests\.'    -SimpleMatch:$false).Count
$failed = ($lines | Select-String -Pattern '^\s*(#\d+ [\d.]+\s+)?\s*Failed RecruitOps\.').Count

# The runner's own summaries, kept for cross-checking. There is one per test project.
$summaries = $lines | Select-String -Pattern 'Test Run Successful|Test Run Failed|Total tests:'

Write-Host ''
Write-Host 'Backend test cases, counted from the whole log' -ForegroundColor Cyan
Write-Host '---------------------------------------------'

[PSCustomObject]@{
    Assembly = 'RecruitOps.Domain.Tests'
    Passed   = $domain
    Expected = $ExpectedDomain
    Verdict  = if ($domain -eq $ExpectedDomain) { 'as documented' }
               elseif ($domain -eq 0) { 'RAN NOTHING' }
               else { 'MISMATCH' }
},
[PSCustomObject]@{
    Assembly = 'RecruitOps.Api.Tests'
    Passed   = $api
    Expected = $ExpectedApi
    Verdict  = if ($api -eq $ExpectedApi) { 'as documented' }
               elseif ($api -eq 0) { 'RAN NOTHING' }
               else { 'MISMATCH' }
} | Format-Table -AutoSize

if ($failed -gt 0) {
    Write-Host "$failed failing case(s) in this log." -ForegroundColor Red
}

Write-Host 'Runner summaries found in the log:' -ForegroundColor DarkGray
if ($summaries) {
    $summaries | ForEach-Object { '  ' + ($_.Line -replace '^#\d+ +[\d.]+ +', '').Trim() }
} else {
    Write-Host '  none — the log is truncated, or the tests never reached a summary.' -ForegroundColor Yellow
}
Write-Host ''

# A truncated log is the likeliest reason for a low count, and it is worth saying out loud
# rather than letting the number be believed.
if ($summaries.Count -lt 2) {
    Write-Host 'WARNING: fewer than two runner summaries. A complete run of this solution' -ForegroundColor Yellow
    Write-Host '         produces one per test project, so this log is probably truncated' -ForegroundColor Yellow
    Write-Host '         and the counts above are lower bounds, not totals.' -ForegroundColor Yellow
    Write-Host ''
}

if ($domain -eq 0 -or $api -eq 0) { exit 1 }
