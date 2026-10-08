param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$projectPath = Join-Path $PSScriptRoot 'src\Claudgar.App\Claudgar.App.csproj'
$bridgeProjectPath = Join-Path $PSScriptRoot 'src\Claudgar.McpBridge\Claudgar.McpBridge.csproj'
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '.tmp'))
$bridgeOutputPath = Join-Path $temporaryRoot 'claude-bridge'
$bridgeExecutablePath = Join-Path $bridgeOutputPath 'Claudgar.McpBridge.exe'
[xml]$versionDocument = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Version.props') -Raw
$releaseVersion = [string]$versionDocument.Project.PropertyGroup.Version
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Version.props must contain a stable X.Y.Z version.' }

& dotnet publish $bridgeProjectPath -c Release -r $Runtime --self-contained true -o $bridgeOutputPath
if ($LASTEXITCODE -ne 0) { throw 'Claude connection helper publish failed.' }

# Stage and stamp addon metadata without changing source or touching a live game installation.
$addonSourcePath = Join-Path $PSScriptRoot 'addon\Claudgar'
$addonPayloadPath = [IO.Path]::GetFullPath((Join-Path $temporaryRoot 'addon-payload'))
if (-not $addonPayloadPath.StartsWith($temporaryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Addon staging must remain under .tmp.'
}
if (Test-Path -LiteralPath $addonPayloadPath) {
    $stagingItems = @(Get-Item -LiteralPath $temporaryRoot) + @(Get-Item -LiteralPath $addonPayloadPath) + @(Get-ChildItem -LiteralPath $addonPayloadPath -Recurse -Force)
    if ($stagingItems | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
        throw 'Addon staging must not contain directory links.'
    }
    Remove-Item -LiteralPath $addonPayloadPath -Recurse -Force
}
New-Item -ItemType Directory -Path $addonPayloadPath -Force | Out-Null
foreach ($addonFile in Get-ChildItem -LiteralPath $addonSourcePath -File -Recurse) {
    $relativeAddonPath = $addonFile.FullName.Substring($addonSourcePath.Length).TrimStart([char[]]@('\', '/'))
    $payloadFilePath = Join-Path $addonPayloadPath $relativeAddonPath
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($payloadFilePath)) -Force | Out-Null
    if ($addonFile.Extension -eq '.toc') {
        $contents = Get-Content -LiteralPath $addonFile.FullName -Raw
        $contents = [regex]::Replace($contents, '(?m)^## Version:.*$', "## Version: $releaseVersion")
        [IO.File]::WriteAllText($payloadFilePath, $contents, [Text.UTF8Encoding]::new($false))
    } else { Copy-Item -LiteralPath $addonFile.FullName -Destination $payloadFilePath -Force }
}
# Keep one permanent location for the runnable Windows build.
$outputPath = Join-Path $PSScriptRoot 'dist\win-x64'
& dotnet publish $projectPath -c Release -r $Runtime --self-contained true -o $outputPath "-p:ClaudgarBridgePath=$bridgeExecutablePath" "-p:ClaudgarAddonPath=$addonPayloadPath"
if ($LASTEXITCODE -ne 0) { throw 'Claudgar publish failed.' }
$publishedExecutablePath = Join-Path $outputPath 'Claudgar.exe'
$checksum = (Get-FileHash -LiteralPath $publishedExecutablePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $outputPath 'Claudgar.exe.sha256'), "$checksum  Claudgar.exe`n", [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $outputPath 'LICENSE') -Force
Write-Host "Release version: $releaseVersion"
Write-Host "Portable executable: $outputPath\Claudgar.exe"
$releaseSizeMiB = [Math]::Round((Get-Item -LiteralPath $publishedExecutablePath).Length / 1MB, 1)
Write-Host "Release build, self-contained: $releaseSizeMiB MiB"
