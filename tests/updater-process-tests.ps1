param()
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$temporaryRoot = Join-Path $repository '.tmp\updater-fixtures'
$oldOutput = Join-Path $temporaryRoot 'old'
$newOutput = Join-Path $temporaryRoot 'new'
Push-Location $repository
try {
    dotnet publish tests/Claudgar.Updater.Fixture/Claudgar.Updater.Fixture.csproj -c Release -r win-x64 --self-contained false -o $oldOutput -p:Version=0.1.0 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'The old updater fixture could not be built.' }
    dotnet publish tests/Claudgar.Updater.Fixture/Claudgar.Updater.Fixture.csproj -c Release -r win-x64 --self-contained false --no-restore -o $newOutput -p:Version=0.2.0 -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw 'The new updater fixture could not be built.' }
    $oldFixture = Join-Path $oldOutput 'Claudgar.Updater.Fixture.exe'
    $newFixture = Join-Path $newOutput 'Claudgar.Updater.Fixture.exe'
    dotnet run --project tests/Claudgar.Updater.Tests/Claudgar.Updater.Tests.csproj -c Release -- $oldFixture $newFixture
    if ($LASTEXITCODE -ne 0) { throw 'Isolated updater process checks failed.' }
}
finally { Pop-Location }
