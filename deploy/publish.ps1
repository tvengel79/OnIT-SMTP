<#
.SYNOPSIS
    Publishes both Windows apps (service + config tool) as self-contained, win-x64 builds.
#>
param(
    [string]$OutputRoot = "$PSScriptRoot\..\publish"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Resolve-Path "$PSScriptRoot\.."

function Publish-Project([string]$ProjectPath, [string]$OutDir) {
    Write-Host "Publishing $ProjectPath -> $OutDir"
    dotnet publish $ProjectPath -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -o $OutDir
}

Publish-Project "$RepoRoot\src\OnIT.Smtp.Service\OnIT.Smtp.Service.csproj" "$OutputRoot\Service"
Publish-Project "$RepoRoot\src\OnIT.Smtp.ConfigTool\OnIT.Smtp.ConfigTool.csproj" "$OutputRoot\ConfigTool"

Write-Host "Done. Output in $OutputRoot"
