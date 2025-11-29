#!/usr/bin/env pwsh

<#
.DESCRIPTION
Bootstraps the Cake build runner and executes the build script.
#>

[CmdletBinding()]
Param(
    [string]$Script = "build.cake",
    [string]$Target = "Default",
    [string]$Configuration = "Release",
    [string]$Verbosity = "Normal",
    [switch]$Experimental,
    [switch]$Mono,
    [switch]$SkipToolPackageRestore,
    [switch]$ScriptArgs
)

Write-Host "PowerShell $($PSVersionTable.PSEdition) version $($PSVersionTable.PSVersion)" -ForegroundColor Green

$CakePath = Join-Path $PSScriptRoot "tools" "Cake.Tool" ".store" "cake.tool" "*" "tools" "net6.0" "any" "cake.exe"
$CakeToolPath = $CakePath
$Addins = Join-Path $PSScriptRoot ".cake" "addins"
$Modules = Join-Path $PSScriptRoot ".cake" "modules"

if ((Resolve-Path $PSScriptRoot).Path -cmatch "^([a-z])+:") {
    Write-Warning "Running build script from a drive that is not the system drive might lead to errors when using NuGet tools. Please consider running from the system drive."
}

if ($Mono) {
    $ToolPath = Join-Path $PSScriptRoot "tools" ".store" "cake.tool" "*" "tools" "net6.0" "any" "cake.exe"
    $ExePath = Get-ChildItem $ToolPath | Select-Object -First 1 | ForEach-Object{ $_.FullName }
    if ($null -eq $ExePath) {
        $ExePath = "mono"
    }
    $CakeExePath = $ExePath
} else {
    $CakeExePath = $CakeToolPath
}

$UseDryRun = $Experimental.IsPresent
$UseMono = $Mono.IsPresent
$SkipToolPackageRestore = $SkipToolPackageRestore.IsPresent
$ScriptArgs = @()

if ($UseMono) {
    [string[]]$ScriptArgs = @($Script, "--target=""$Target""", "--configuration=""$Configuration""", "--verbosity=""$Verbosity""")
    if ($UseDryRun) {
        $ScriptArgs += "--experimental"
    }
    if ($SkipToolPackageRestore) {
        $ScriptArgs += "--skip-package-restore"
    }
    & mono "$CakeExePath" $ScriptArgs
} else {
    [string[]]$ScriptArgs = @("--target=""$Target""", "--configuration=""$Configuration""", "--verbosity=""$Verbosity""", "--bootstrap")
    if ($UseDryRun) {
        $ScriptArgs += "--experimental"
    }
    if ($SkipToolPackageRestore) {
        $ScriptArgs += "--skip-package-restore"
    }
    & "$CakeExePath" $Script $ScriptArgs
}

exit $LASTEXITCODE