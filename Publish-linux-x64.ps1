[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $buildName = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss-fff')
    $OutputDirectory = Join-Path $root "Publish/linux-x64/$buildName"
}
$destination = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) {
    throw "Choose a new release directory. Already exists: $destination"
}

$bin = Join-Path $destination 'Bin'
[IO.Directory]::CreateDirectory($bin) | Out-Null
if (-not $NoRestore) {
    dotnet msbuild (Join-Path $root 'Server/Server.sln') -t:Restore -m:1 -nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Release restore failed (exit $LASTEXITCODE)." }
}

foreach ($project in 'Server/Hotfix/Hotfix.csproj', 'Server/App/App.csproj') {
    dotnet msbuild (Join-Path $root $project) -t:Publish -p:Configuration=Release -p:SelfContained=false -p:UseAppHost=false `
        "-p:OutDir=$bin/" "-p:PublishDir=$bin/" -m:1 -nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Release build failed for $project (exit $LASTEXITCODE)." }
}

Copy-Item -LiteralPath (Join-Path $root 'Config') -Destination $destination -Recurse
foreach ($required in 'Bin/App.dll', 'Bin/Hotfix.dll', 'Bin/CapybaraRules.dll', 'Config/Generate/AdventureChallengeConfigCategory.bytes') {
    if (-not (Test-Path -LiteralPath (Join-Path $destination $required))) {
        throw "Release is missing $required."
    }
}

$prefix = $destination.TrimEnd([char[]]@('/', '\')) + [IO.Path]::DirectorySeparatorChar
$manifest = Get-ChildItem -LiteralPath $destination -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($prefix.Length).Replace('\', '/')
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $relative
}
[IO.File]::WriteAllLines((Join-Path $destination 'manifest.sha256'), [string[]]$manifest, [Text.UTF8Encoding]::new($false))

Write-Output "Release built at $destination"
