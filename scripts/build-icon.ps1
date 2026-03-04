param(
    [string]$SvgPath = "src/VTOLVRWorkshopProfileSwitcher/Assets/AppIcon.svg",
    [string]$IcoPath = "src/VTOLVRWorkshopProfileSwitcher/Assets/AppIcon.ico",
    [string]$OutputDir = "scripts/.icon-build",
    [switch]$KeepFrames
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$svgFullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $SvgPath))
$icoFullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $IcoPath))
$tmpDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDir))
$sizes = @(16, 24, 32, 48, 64, 128, 256)

if (-not (Test-Path $svgFullPath)) {
    throw "SVG source not found: $svgFullPath"
}

if (Test-Path $tmpDir) {
    Remove-Item -Path $tmpDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tmpDir | Out-Null

$magick = Get-Command magick -ErrorAction SilentlyContinue
if (-not $magick) {
    $defaultMagick = "C:\Program Files\ImageMagick-7.1.2-Q16-HDRI\magick.exe"
    if (Test-Path $defaultMagick) {
        $magick = [pscustomobject]@{ Source = $defaultMagick }
    }
}

$iconBuilderProj = Join-Path $PSScriptRoot "IconBuilder/IconBuilder.csproj"
if (-not (Test-Path $iconBuilderProj)) {
    throw "IconBuilder project not found: $iconBuilderProj"
}

Write-Host "Rendering PNG frames from SVG..."
$pngPaths = @()
if ($magick) {
    foreach ($size in $sizes) {
        $pngPath = Join-Path $tmpDir ("AppIcon_{0}.png" -f $size)
        & $magick.Source -background none $svgFullPath -resize ("{0}x{0}" -f $size) $pngPath
        if ($LASTEXITCODE -ne 0) {
            throw "ImageMagick render failed for size ${size}px."
        }
        $pngPaths += $pngPath
    }
}
else {
    $sizeCsv = ($sizes -join ",")
    & dotnet run --project $iconBuilderProj --configuration Release -- render $svgFullPath $tmpDir $sizeCsv
    if ($LASTEXITCODE -ne 0) {
        throw "IconBuilder SVG render failed."
    }
    foreach ($size in $sizes) {
        $pngPaths += (Join-Path $tmpDir ("AppIcon_{0}.png" -f $size))
    }
}

Write-Host "Packing multi-size ICO..."
$packedWithMagick = $false
if ($magick) {
    & $magick.Source @pngPaths $icoFullPath
    if ($LASTEXITCODE -eq 0) {
        $packedWithMagick = $true
    }
    else {
        Write-Warning "ImageMagick ICO pack failed; falling back to IconBuilder."
    }
}

if (-not $packedWithMagick) {
    & dotnet run --project $iconBuilderProj --configuration Release -- pack $icoFullPath $pngPaths
    if ($LASTEXITCODE -ne 0) {
        throw "IconBuilder ICO pack failed."
    }
}

Write-Host "Created icon: $icoFullPath"
Write-Host "PNG frames: $tmpDir"

if (-not $KeepFrames -and (Test-Path $tmpDir)) {
    Remove-Item -Path $tmpDir -Recurse -Force
}
