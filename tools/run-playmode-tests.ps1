#Requires -Version 5.1
<#
.SYNOPSIS
  Runs Unity PlayMode tests via batchmode; writes XML results and a log under TestResults/.

.NOTES
  Close the Unity Editor for this project first — batchmode cannot open a locked project.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\tools\run-playmode-tests.ps1
#>

param(
  [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path,
  [string]$UnityPath = "",
  [string]$ResultsFile = "",
  [string]$LogFile = "",
  [string]$AssemblyNames = "Forbes.Tests.PlayMode"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-ProcessPathEnvironment {
  $vars = [System.Environment]::GetEnvironmentVariables("Process")
  $pathKeys = @($vars.Keys | Where-Object { $_ -ieq "Path" })

  if ($pathKeys.Count -le 1) {
    return
  }

  $pathValue = [string]$vars["Path"]
  if (-not $pathValue) {
    $pathValue = [string]$vars["PATH"]
  }

  [System.Environment]::SetEnvironmentVariable("PATH", $null, "Process")
  [System.Environment]::SetEnvironmentVariable("Path", $pathValue, "Process")
}

$testResultsDir = Join-Path $ProjectPath "TestResults"
if (-not (Test-Path $testResultsDir)) {
  New-Item -ItemType Directory -Path $testResultsDir | Out-Null
}

if (-not $ResultsFile) {
  $ResultsFile = Join-Path $testResultsDir "playmode.xml"
}

if (-not $LogFile) {
  $LogFile = Join-Path $testResultsDir "unity-playmode.log"
}

if (-not [System.IO.Path]::IsPathRooted($ResultsFile)) {
  $ResultsFile = Join-Path $ProjectPath $ResultsFile
}

if (-not [System.IO.Path]::IsPathRooted($LogFile)) {
  $LogFile = Join-Path $ProjectPath $LogFile
}

if (-not $UnityPath) {
  $hubRoot = "${env:ProgramFiles}\Unity\Hub\Editor"
  if (-not (Test-Path $hubRoot)) {
    Write-Error "Unity Hub editors folder not found: $hubRoot. Pass -UnityPath to Unity.exe."
  }
  $latest = Get-ChildItem $hubRoot -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Select-Object -First 1
  if (-not $latest) { Write-Error "No Unity versions under Hub: $hubRoot" }
  $UnityPath = Join-Path $latest.FullName "Editor\Unity.exe"
}

if (-not (Test-Path $UnityPath)) { Write-Error "Unity.exe not found: $UnityPath" }

Write-Host "Unity:       $UnityPath"
Write-Host "Project:     $ProjectPath"
Write-Host "Results XML: $ResultsFile"
Write-Host "Log:         $LogFile"
Write-Host "Assemblies:  $AssemblyNames"

Normalize-ProcessPathEnvironment
$maxAttempts = 2
$exitCode = 0
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
  if (Test-Path $ResultsFile) {
    Remove-Item $ResultsFile -Force
  }

  $argsUnity = @(
    "-batchmode", "-nographics",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", "PlayMode",
    "-assemblyNames", $AssemblyNames,
    "-testResults", $ResultsFile,
    "-logFile", $LogFile
  )

  if ($attempt -gt 1) {
    Write-Warning "Unity did not write PlayMode results on attempt $($attempt - 1); retrying once."
  }

  $p = Start-Process -FilePath $UnityPath -ArgumentList $argsUnity -Wait -PassThru -NoNewWindow
  $exitCode = $p.ExitCode

  if ($exitCode -ne 0) {
    Write-Error "Unity PlayMode tests failed with exit code $exitCode. See $LogFile and $ResultsFile."
  }

  if (Test-Path $ResultsFile) {
    break
  }
}

if (-not (Test-Path $ResultsFile)) {
  Write-Error "Unity PlayMode tests did not write a results file. See $LogFile."
}

[xml]$resultsXml = Get-Content $ResultsFile
$testRun = $resultsXml.SelectSingleNode("/test-run")
if (-not $testRun) {
  Write-Error "Unity PlayMode tests wrote an unreadable results file: $ResultsFile."
}

$failed = [int]$testRun.failed
if ($testRun.result -ne "Passed" -or $failed -gt 0) {
  Write-Error "Unity PlayMode tests failed: result=$($testRun.result), failed=$failed. See $ResultsFile and $LogFile."
}

Write-Host "OK - results: $ResultsFile"
