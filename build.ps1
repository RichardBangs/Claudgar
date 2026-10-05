param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'src\Claudgar.App\Claudgar.App.csproj'
$outputPath = Join-Path $PSScriptRoot "dist\$Runtime"
& dotnet publish $projectPath -c Release -r $Runtime --self-contained true -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Claudgar publish failed.' }
Write-Host "Portable executable: $outputPath\Claudgar.exe"
