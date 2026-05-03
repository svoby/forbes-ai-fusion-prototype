#Requires -Version 5.1
<#
.SYNOPSIS
  Runs Unity EditMode tests via batchmode; writes XML results and a log under TestResults/.

.NOTES
  Close the Unity Editor for this project first — batchmode cannot open a locked project.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\tools\run-editmode-tests.ps1
#>

param(
  [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path,
  [string]$UnityPath = "",
  [string]$ResultsFile = "",
  [string]$LogFile = "",
  [string]$AssemblyNames = "Forbes.Tests.EditMode"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$testResultsDir = Join-Path $ProjectPath "TestResults"
if (-not (Test-Path $testResultsDir)) {
  New-Item -ItemType Directory -Path $testResultsDir | Out-Null
}

if (-not $ResultsFile) {
  $ResultsFile = Join-Path $testResultsDir "editmode.xml"
}

if (-not $LogFile) {
  $LogFile = Join-Path $testResultsDir "unity-editmode.log"
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

$argsUnity = @(
  "-batchmode", "-nographics", "-quit",
  "-projectPath", $ProjectPath,
  "-runTests",
  "-testPlatform", "editmode",
  "-assemblyNames", $AssemblyNames,
  "-testResults", $ResultsFile,
  "-logFile", $LogFile
)

$p = Start-Process -FilePath $UnityPath -ArgumentList $argsUnity -Wait -PassThru -NoNewWindow

if ($p.ExitCode -ne 0) {
  Write-Error "Unity EditMode tests failed with exit code $($p.ExitCode). See $LogFile and $ResultsFile."
}

Write-Host "OK - results: $ResultsFile"
