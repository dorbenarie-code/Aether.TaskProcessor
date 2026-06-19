param(
    [string] $OutputDirectory = "C:\Temp\aether-wpf-single-file"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)

if (-not $isWindowsHost) {
    throw "This publish script must be run from Windows PowerShell because WPF publishing requires the WindowsDesktop SDK."
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..\..")).ProviderPath

$projectPathCandidate = Join-Path $repositoryRoot "src\Aether.WpfApp\Aether.WpfApp.csproj"

if (-not (Test-Path $projectPathCandidate)) {
    throw "WPF project was not found: $projectPathCandidate"
}

$projectPath = (Resolve-Path $projectPathCandidate).ProviderPath

if (Test-Path $OutputDirectory) {
    Remove-Item -Recurse -Force $OutputDirectory
}

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

& dotnet publish $projectPath `
    -p:PublishProfile=win-x64-single-file `
    -p:DebugSymbols=false `
    -p:DebugType=none `
    -p:CopyOutputSymbolsToPublishDirectory=false `
    -o $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exeFiles = @(Get-ChildItem $OutputDirectory -Filter "*.exe" -File)
$unexpectedFiles = @(Get-ChildItem $OutputDirectory -File | Where-Object {
    $_.Extension -in @(".dll", ".pdb", ".json", ".deps", ".runtimeconfig")
})

if ($exeFiles.Count -ne 1) {
    throw "Expected exactly one EXE in publish output, but found $($exeFiles.Count)."
}

if ($unexpectedFiles.Count -gt 0) {
    $unexpectedFileNames = $unexpectedFiles | Select-Object -ExpandProperty Name
    throw "Unexpected publish output files: $($unexpectedFileNames -join ', ')"
}

Write-Host "WPF single-file publish completed."
Write-Host "Output directory: $OutputDirectory"
Write-Host "Executable: $($exeFiles[0].FullName)"
Write-Host "Size bytes: $($exeFiles[0].Length)"
