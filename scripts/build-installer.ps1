param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$VersionTag = "",
    [ValidateSet("Stable", "Beta")]
    [string]$Channel = "Stable",
    [string]$InnoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\VTOLVRWorkshopProfileSwitcher\VTOLVRWorkshopProfileSwitcher.csproj"
$publishRoot = Join-Path $PSScriptRoot "..\publish"
$publishDir = Join-Path $publishRoot ("win-x64-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$issPath = Join-Path $PSScriptRoot "..\installer\VTOLVRWorkshopProfileSwitcher.iss"
$iconPath = Join-Path $PSScriptRoot "..\src\VTOLVRWorkshopProfileSwitcher\Assets\AppIcon.ico"
$licensePath = Join-Path $PSScriptRoot "..\LICENSE"
$isBeta = $Channel -eq "Beta"
$outputBaseFilename = if ($isBeta) { "VTOLVRSwitcher-Beta-Setup" } else { "VTOLVRSwitcher-Setup" }
$appName = if ($isBeta) { "VTOL VR Switcher Beta" } else { "VTOL VR Switcher" }
$defaultDirName = "{autopf}\VTOL VR Switcher"
$defaultGroupName = if ($isBeta) { "VTOL VR Switcher Beta" } else { "VTOL VR Switcher" }
$appId = '{{6AB2D1C3-8D31-45E8-8B3F-AC5C8C1A7E12}}'
$defaultIncludeBetaUpdates = if ($isBeta) { "true" } else { "false" }
$showHeaderBetaBadgeForStableBuilds = if ($isBeta) { "true" } else { "false" }

$versionText = [string]$Version
if ($null -eq $versionText) {
    $versionText = string.Empty
}
$versionText = $versionText.Trim()

$versionTagText = [string]$VersionTag
if ($null -eq $versionTagText) {
    $versionTagText = string.Empty
}
$versionTagText = $versionTagText.Trim()

if ([string]::IsNullOrWhiteSpace($versionText) -and [string]::IsNullOrWhiteSpace($versionTagText)) {
    $prompt = if ($isBeta) {
        "Beta version tag (example: 1.1.9-beta.1)"
    }
    else {
        "Version tag (example: 1.1.9)"
    }

    $versionTagText = [string](Read-Host $prompt)
    if ($null -eq $versionTagText) {
        $versionTagText = string.Empty
    }
    $versionTagText = $versionTagText.Trim()
}

if ([string]::IsNullOrWhiteSpace($versionText) -and -not [string]::IsNullOrWhiteSpace($versionTagText)) {
    $versionText = ($versionTagText.TrimStart("v", "V") -split "-", 2)[0].Trim()
}

if ([string]::IsNullOrWhiteSpace($versionText)) {
    [xml]$projectXml = Get-Content -LiteralPath $project
    $versionText = [string]$projectXml.Project.PropertyGroup.Version
    if ($null -eq $versionText) {
        $versionText = string.Empty
    }
    $versionText = $versionText.Trim()
}

if ([string]::IsNullOrWhiteSpace($versionText)) {
    throw "Version cannot be empty."
}

if ([string]::IsNullOrWhiteSpace($versionTagText)) {
    $versionTagText = $versionText
}

$parsedVersion = $null
if (-not [System.Version]::TryParse($versionText, [ref]$parsedVersion)) {
    throw "Version '$Version' is invalid. Use MAJOR.MINOR.PATCH or MAJOR.MINOR.PATCH.REVISION."
}

if ($parsedVersion.Build -lt 0) {
    throw "Version '$Version' must include at least MAJOR.MINOR.PATCH."
}

$assemblyFileVersion = if ($parsedVersion.Revision -ge 0) {
    "{0}.{1}.{2}.{3}" -f $parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build, $parsedVersion.Revision
}
else {
    "{0}.{1}.{2}.0" -f $parsedVersion.Major, $parsedVersion.Minor, $parsedVersion.Build
}

Write-Host "Stopping running app if present..."
Get-Process VTOLVRWorkshopProfileSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "Restoring packages for runtime $Runtime ..."
dotnet restore $project -r $Runtime
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

Write-Host "Publishing app to $publishDir ..."
dotnet publish $project -c $Configuration -r $Runtime --self-contained true /p:PublishSingleFile=true /p:Version=$versionText /p:InformationalVersion=$versionTagText /p:AssemblyVersion=$assemblyFileVersion /p:FileVersion=$assemblyFileVersion /p:ShowHeaderBetaBadgeForStableBuilds=$showHeaderBetaBadgeForStableBuilds -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishedExePath = Join-Path $publishDir "VTOLVRWorkshopProfileSwitcher.exe"
if (-not (Test-Path $publishedExePath)) {
    throw "Publish output is missing '$publishedExePath'."
}

$publishedFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($publishedExePath).FileVersion
if (-not [string]::Equals($publishedFileVersion, $assemblyFileVersion, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Published exe version '$publishedFileVersion' does not match requested version '$assemblyFileVersion'."
}

if (-not (Test-Path $InnoCompiler)) {
    Write-Warning "Inno Setup compiler not found: $InnoCompiler"
    Write-Warning "Install Inno Setup 6, then run this script again."
    Write-Host "Published app is ready at: $publishDir"
    exit 0
}

Write-Host "Building installer..."
& $InnoCompiler "/DMyAppVersion=$versionText" "/DSourceDir=$publishDir" "/DIconFile=$iconPath" "/DLicenseFile=$licensePath" "/DAppChannel=$Channel" "/DMyAppName=$appName" "/DMyAppId=$appId" "/DMyOutputBaseFilename=$outputBaseFilename" "/DMyDefaultDirName=$defaultDirName" "/DMyDefaultGroupName=$defaultGroupName" "/DDefaultIncludeBetaUpdates=$defaultIncludeBetaUpdates" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compile failed with exit code $LASTEXITCODE"
}

Write-Host "Done. Installer output: installer\\output"
