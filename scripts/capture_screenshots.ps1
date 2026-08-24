param(
    [string]$PdfExe = "publish/PDFDual/PDFDual.exe",
    [string]$IcoExe = "publish/IconCraft/IconCraft.exe",
    [string]$AssetsDir = "assets"
)

New-Item -ItemType Directory -Path $AssetsDir -Force | Out-Null

if (Test-Path $PdfExe) {
    Write-Host "Capturing PDFDual window..."
    Start-Process -FilePath (Resolve-Path $PdfExe) -ArgumentList '--screenshot', "$AssetsDir/pdfdual_preview.jpg" -Wait
}

if (Test-Path $IcoExe) {
    Write-Host "Capturing IconCraft window..."
    Start-Process -FilePath (Resolve-Path $IcoExe) -ArgumentList '--screenshot', "$AssetsDir/iconcraft_preview.jpg" -Wait
}

Write-Host "Automated screenshots captured successfully!"
