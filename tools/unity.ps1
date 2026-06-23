<#
.SYNOPSIS
  Headless Unity runner for the proto pipeline. Drives the editor in batchmode so Claude can
  compile, test, run editor tools, and build WebGL without anyone touching the GUI.

.DESCRIPTION
  Resolves the Unity editor from ProjectSettings/ProjectVersion.txt (override with -UnityPath or
  $env:UNITY_EDITOR), runs the requested command in -batchmode, logs to Logs/claude/, and returns a
  clean exit code (0 = success, 1 = failure). The Unity editor must be CLOSED -- batchmode cannot open a
  project a GUI instance already has locked.

.PARAMETER Command
  compile      CS error check (the hard gate). Imports + compiles, scans the log for 'error CS'.
  test         EditMode tests via the Unity Test Framework. -TestFilter narrows; results parsed from NUnit XML.
  exec         Run a static editor method headless:  -Method <Namespace.Class.Method>
  build-webgl  WebGL build via Proto.EditorTools.BuildScript.BuildWebGL -> Builds\<name>-web\

.EXAMPLE
  .\tools\unity.ps1 compile
  .\tools\unity.ps1 test -TestFilter SessionTests
  .\tools\unity.ps1 exec -Method Proto.EditorTools.SceneSetup.Build
  .\tools\unity.ps1 build-webgl
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('compile', 'test', 'exec', 'build-webgl')]
    [string]$Command,

    [string]$Method,
    [string]$TestFilter,
    [string]$TestPlatform = 'EditMode',
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'

# --- Paths -------------------------------------------------------------------
$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$LogDir = Join-Path $ProjectRoot 'Logs\claude'
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$LogFile = Join-Path $LogDir "$Command-$stamp.log"

function Fail($msg) {
    Write-Host "ERROR: $msg" -ForegroundColor Red
    exit 1
}

# --- Resolve the Unity editor ------------------------------------------------
function Resolve-Unity {
    if ($UnityPath) {
        if (Test-Path $UnityPath) { return $UnityPath }
        Fail "UnityPath '$UnityPath' does not exist."
    }
    if ($env:UNITY_EDITOR -and (Test-Path $env:UNITY_EDITOR)) { return $env:UNITY_EDITOR }

    $versionFile = Join-Path $ProjectRoot 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) { Fail "ProjectVersion.txt not found at $versionFile" }
    $line = Get-Content $versionFile | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
    if (-not $line) { Fail "Could not read m_EditorVersion from ProjectVersion.txt" }
    $version = ($line -replace '^m_EditorVersion:\s*', '').Trim()

    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        (Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$version\Editor\Unity.exe"),
        "C:\Program Files\Unity\Editor\Unity.exe"
    ) | Where-Object { $_ } | Select-Object -Unique
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }

    # Last resort: any installed Hub editor (warn -- version mismatch is on the caller).
    $hubRoot = "C:\Program Files\Unity\Hub\Editor"
    if (Test-Path $hubRoot) {
        $any = Get-ChildItem $hubRoot -Directory -ErrorAction SilentlyContinue |
               ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
               Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($any) {
            Write-Host "WARNING: Unity $version not found; falling back to $any" -ForegroundColor Yellow
            return $any
        }
    }
    Fail "Unity editor $version not found. Install it via Unity Hub, set `$env:UNITY_EDITOR, or pass -UnityPath."
}

# --- Guard: editor must be closed -------------------------------------------
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    Fail "The Unity editor appears to be running. Close the project, then re-run (batchmode needs an exclusive lock)."
}

$Unity = Resolve-Unity
$common = @('-batchmode', '-nographics', '-projectPath', $ProjectRoot, '-logFile', $LogFile)

function Invoke-Unity([string[]]$extraArgs) {
    $allArgs = $common + $extraArgs
    Write-Host "> Unity $($extraArgs -join ' ')" -ForegroundColor Cyan
    Write-Host "  editor: $Unity"
    Write-Host "  log:    $LogFile"
    $p = Start-Process -FilePath $Unity -ArgumentList $allArgs -NoNewWindow -PassThru -Wait
    return $p.ExitCode
}

function Get-Log { if (Test-Path $LogFile) { Get-Content $LogFile } else { @() } }

function Show-CompileErrors {
    $log = Get-Log
    $errs = $log | Where-Object { $_ -match 'error CS\d+' -or $_ -match 'Compilation failed' }
    if ($errs) {
        Write-Host "`n--- Compiler errors ---" -ForegroundColor Red
        $errs | Select-Object -Unique | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    }
    return [bool]$errs
}

function Show-LogTail([int]$n = 40) {
    Write-Host "`n--- log tail ($n lines) ---" -ForegroundColor DarkGray
    Get-Log | Select-Object -Last $n | ForEach-Object { Write-Host $_ }
    Write-Host "Full log: $LogFile" -ForegroundColor DarkGray
}

# --- Commands ----------------------------------------------------------------
switch ($Command) {

    'compile' {
        # Import + compile, then scan the log. -quit makes Unity exit after the import/compile pass.
        $code = Invoke-Unity @('-quit')
        $hasErrors = Show-CompileErrors
        if ($hasErrors) { Show-LogTail 25; Fail "Compilation failed." }
        if ($code -ne 0) {
            $log = Get-Log
            if ($log | Where-Object { $_ -match 'Multiple Unity instances|already open|another Unity instance' }) {
                Fail "Unity could not open the project (already locked). Close the editor and retry."
            }
            Show-LogTail 25
            Fail "Unity exited with code $code (no 'error CS' found -- check the log)."
        }
        Write-Host "COMPILE OK" -ForegroundColor Green
        exit 0
    }

    'test' {
        $results = Join-Path $LogDir "test-results-$stamp.xml"
        $args = @('-runTests', '-testPlatform', $TestPlatform, '-testResults', $results)
        if ($TestFilter) { $args += @('-testFilter', $TestFilter) }
        $code = Invoke-Unity $args

        if (Show-CompileErrors) { Show-LogTail 25; Fail "Tests could not run -- compilation failed." }
        if (-not (Test-Path $results)) { Show-LogTail 40; Fail "No test results produced (exit $code). See log." }

        [xml]$xml = Get-Content $results
        $run = $xml.'test-run'
        $total = [int]$run.total; $passed = [int]$run.passed; $failed = [int]$run.failed; $skipped = [int]$run.skipped
        if ($failed -gt 0 -or $code -ne 0) {
            Write-Host "TESTS FAIL  $passed/$total passed, $failed failed, $skipped skipped" -ForegroundColor Red
            $xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
                Write-Host ("  x " + $_.fullname) -ForegroundColor Red
                if ($_.failure -and $_.failure.message) { Write-Host ("      " + ($_.failure.message.'#cdata-section' -replace '\s+', ' ').Trim()) -ForegroundColor DarkRed }
            }
            Fail "Test run had failures."
        }
        Write-Host "TESTS PASS  $passed/$total passed, $skipped skipped" -ForegroundColor Green
        Write-Host "Results: $results" -ForegroundColor DarkGray
        exit 0
    }

    'exec' {
        if (-not $Method) { Fail "exec needs -Method <Namespace.Class.Method>" }
        $code = Invoke-Unity @('-quit', '-executeMethod', $Method)
        if (Show-CompileErrors) { Show-LogTail 25; Fail "Compilation failed -- method not run." }
        if ($code -ne 0) { Show-LogTail 40; Fail "executeMethod $Method exited with code $code." }
        Write-Host "EXEC OK  ($Method)" -ForegroundColor Green
        Show-LogTail 15
        exit 0
    }

    'build-webgl' {
        $code = Invoke-Unity @('-quit', '-executeMethod', 'Proto.EditorTools.BuildScript.BuildWebGL')
        if (Show-CompileErrors) { Show-LogTail 25; Fail "Compilation failed -- build aborted." }
        if ($code -ne 0) { Show-LogTail 40; Fail "WebGL build exited with code $code." }
        $built = Get-ChildItem (Join-Path $ProjectRoot 'Builds') -Recurse -Filter 'index.html' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $built) { Show-LogTail 40; Fail "Build finished but no index.html found under Builds\." }
        Write-Host "BUILD OK  -> $($built.Directory.FullName)" -ForegroundColor Green
        exit 0
    }
}
