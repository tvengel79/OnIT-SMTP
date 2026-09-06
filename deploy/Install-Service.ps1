#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the OnIT-SMTP Windows Service from a published build.
.DESCRIPTION
    Scripted alternative to the "Install" button on the config tool's Service Status tab --
    useful for unattended/automated deployment (e.g. via a deployment pipeline or GPO).
.PARAMETER ServiceExePath
    Path to the published OnIT.Smtp.Service.exe (see publish.ps1).
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ServiceExePath
)

$ErrorActionPreference = "Stop"
$ServiceName = "OnIT-SMTP"

if (-not (Test-Path $ServiceExePath)) {
    throw "Service executable not found at '$ServiceExePath'. Publish the service first (see deploy/publish.ps1)."
}
$ServiceExePath = (Resolve-Path $ServiceExePath).Path

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service '$ServiceName' already exists. Stopping and removing it first."
    if ($existing.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
    }
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating service '$ServiceName'..."
sc.exe create $ServiceName binPath= "`"$ServiceExePath`"" start= auto DisplayName= "OnIT-SMTP Bridge" | Write-Host
sc.exe description $ServiceName "Relays LAN SMTP mail through Microsoft Graph." | Out-Null

New-Item -ItemType Directory -Force -Path "$env:ProgramData\OnIT-SMTP\logs" | Out-Null

Write-Host "Starting service..."
Start-Service -Name $ServiceName
Write-Host "Done. Current status: $((Get-Service -Name $ServiceName).Status)"
