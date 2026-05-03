#Requires -Version 5.1
<#
.SYNOPSIS
  Runs Unity EditMode tests (Forbes.Tests.EditMode) via batchmode.

.NOTES
  Unity cannot batch-open a project while another Unity instance has it open --
  close the Editor or you'll get exit code 1 and no results file.

.USAGE
  powershell -ExecutionPolicy Bypass -File .\tools\Run-EditModeTests.ps1
#>

param(
  [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\.."),
  [string]$UnityPath   = "",
  [string]$ResultsPath = "",
  [string]$AssemblyNames = "Forbes.Tests.EditMode"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $ResultsPath) {
  $ResultsPath = Join-Path $ProjectPath "TestResults-EditMode.xml"
}

if (-not $UnityPath) {
  $hubRoot = "${env:ProgramFiles}\Unity\Hub\Editor"
  if (-not (Test-Path $hubRoot)) {
    Write-Error "Unity Hub editors folder not found: $hubRoot. Pass -UnityPath explicitly."
  }
  $latest = Get-ChildItem $hubRoot -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Select-Object -First 1
  if (-not $latest) { Write-Error "No Unity versions under Hub: $hubRoot" }
  $UnityPath = Join-Path $latest.FullName "Editor\Unity.exe"
}

if (-not (Test-Path $UnityPath)) { Write-Error "Unity.exe not found: $UnityPath" }

Write-Host "Unity:       $UnityPath"
Write-Host "Project:    $ProjectPath"
Write-Host "Results:    $ResultsPath"
Write-Host "Assemblies: $AssemblyNames"

$argsUnity = @(
  "-batchmode", "-nographics", "-quit",
  "-projectPath", $ProjectPath,
  "-runTests",
  "-testPlatform", "editmode",
  "-assemblyNames", $AssemblyNames,
  "-testResults", $ResultsPath,
  "-logFile", "-"
)

$p = Start-Process -FilePath $UnityPath -ArgumentList $argsUnity -Wait -PassThru -NoNewWindow

if ($p.ExitCode -ne 0) {
  Write-Error "Unity tests failed with exit code $($p.ExitCode). If log says another instance has the project open, close Unity Editor."
}

Write-Host "OK - see $ResultsPath"
