#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Stops and removes the OnIT-SMTP Windows Service.
.PARAMETER RemoveConfig
    Also deletes %ProgramData%\OnIT-SMTP (configuration and logs). Off by default so a
    reinstall doesn't lose the Entra app registration details and IP allow list.
#>
param(
    [switch]$RemoveConfig
)

$ErrorActionPreference = "Stop"
$ServiceName = "OnIT-SMTP"

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne "Stopped") {
        Write-Host "Stopping service..."
        Stop-Service -Name $ServiceName -Force
    }
    sc.exe delete $ServiceName | Write-Host
} else {
    Write-Host "Service '$ServiceName' is not installed."
}

if ($RemoveConfig) {
    $configDir = "$env:ProgramData\OnIT-SMTP"
    if (Test-Path $configDir) {
        Write-Host "Removing $configDir ..."
        Remove-Item -Recurse -Force $configDir
    }
}

Write-Host "Done."
