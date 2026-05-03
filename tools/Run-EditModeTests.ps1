#Requires -Version 5.1
<#
.SYNOPSIS
  Back-compat entry point: delegates to tools/run-editmode-tests.ps1.

.NOTES
  Prefer: powershell -ExecutionPolicy Bypass -File .\tools\run-editmode-tests.ps1
#>

param(
  [string]$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path,
  [string]$UnityPath = "",
  [string]$ResultsPath = "",
  [string]$AssemblyNames = "Forbes.Tests.EditMode"
)

$delegateScript = Join-Path $PSScriptRoot "run-editmode-tests.ps1"
if (-not $ResultsPath) {
  & $delegateScript -ProjectPath $ProjectPath -UnityPath $UnityPath -AssemblyNames $AssemblyNames
} else {
  & $delegateScript -ProjectPath $ProjectPath -UnityPath $UnityPath -ResultsFile $ResultsPath -AssemblyNames $AssemblyNames
}
