param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$VersionTag = "",
    [string]$InnoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

if ([string]::IsNullOrWhiteSpace($VersionTag) -and [string]::IsNullOrWhiteSpace($Version)) {
    $VersionTag = Read-Host "Beta version tag (example: 1.1.9-beta.1)"
}

$scriptPath = Join-Path $PSScriptRoot "build-installer.ps1"
& $scriptPath -Configuration $Configuration -Runtime $Runtime -Version $Version -VersionTag $VersionTag -Channel Beta -InnoCompiler $InnoCompiler
exit $LASTEXITCODE
