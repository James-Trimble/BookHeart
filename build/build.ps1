# Builds BookHeart for Windows: runs the tests, checks the app starts, and makes
#   release/<Name>-<Version>-Setup.exe     the installer
#   release/<Name>-<Version>-Portable.zip  the portable copy (no installing; keeps its data beside it)
# Usage: pwsh build/build.ps1 [-Version 0.1.0]
param([string]$Version)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code ${LASTEXITCODE}: $Command" }
}

[xml]$props = Get-Content Directory.Build.props
$name = $props.Project.PropertyGroup.AppName
if (-not $Version) { $Version = $props.Project.PropertyGroup.Version }
Write-Host "Building $name $Version"

Remove-Item publish, release -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory release | Out-Null

Invoke-Native { dotnet test tests/BookHeart.Core.Tests -c Release }

$project = 'src/BookHeart/BookHeart.csproj'
Invoke-Native { dotnet publish $project -c Release -r win-x64 --self-contained "-p:Version=$Version" -o publish/installer }
Invoke-Native { dotnet publish $project -c Release -r win-x64 --self-contained "-p:Version=$Version" `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish/portable }

# The manual, as a web page beside the program.
& "$PSScriptRoot/manual.ps1" -Output "publish/installer/$name Manual.html" -Version $Version

function Test-Starts([string]$Exe) {
    $p = Start-Process -FilePath $Exe -ArgumentList '--smoke-test' -PassThru
    if (-not $p.WaitForExit(90000)) { $p.Kill(); throw "$Exe did not finish starting within 90 seconds." }
    if ($p.ExitCode -ne 0) {
        Get-Content "$env:LOCALAPPDATA/$name/error.log" -ErrorAction SilentlyContinue
        throw "$Exe failed to start (exit code $($p.ExitCode))."
    }
    Write-Host "$Exe starts correctly."
}
Test-Starts "publish/installer/$name.exe"
Test-Starts "publish/portable/$name.exe"

# Portable zip: the program, the marker that keeps data beside it, and a short read-me.
$portable = "publish/portable-zip/$name"
New-Item -ItemType Directory $portable -Force | Out-Null
Get-ChildItem publish/portable -Exclude *.pdb | Copy-Item -Destination $portable
Copy-Item "publish/installer/$name Manual.html" $portable
Set-Content "$portable/$name.portable" "This file makes $name keep its library in the Data folder next to it. Delete it to use your user folder instead."
Set-Content "$portable/Read me.txt" @"
$name $Version, portable copy

Run $name.exe. "$name Manual.html" explains everything it does. Nothing needs installing, and your library, places and bookmarks are kept in the
Data folder next to it, so you can carry the whole folder on a USB stick.

To install $name with Start menu and desktop shortcuts and file associations, use $name-$Version-Setup.exe instead.
"@
Compress-Archive -Path $portable -DestinationPath "release/$name-$Version-Portable.zip"

# Installer, built with Inno Setup.
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    Invoke-Native { choco install innosetup -y --no-progress }
}
Invoke-Native { & $iscc "/DAppVersion=$Version" "/DSourceDir=..\publish\installer" installer\BookHeart.iss }

Get-ChildItem release | Format-Table Name, Length
