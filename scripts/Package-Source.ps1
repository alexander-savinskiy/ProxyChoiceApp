param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $workspace 'dist' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
[xml]$project = Get-Content -LiteralPath (Join-Path $workspace 'src\ProxyChoiceApp\ProxyChoiceApp.csproj')
$version = $project.Project.PropertyGroup.Version
$files = @('NuGet.Config','README.txt','README.md','CHANGELOG.md','LICENSE.txt','THIRD-PARTY-NOTICES.txt','.gitignore','vendor\singbox.zip','vendor\engine-manifest.json','vendor\upstream-source\sing-box-v1.14.3-source.zip')
foreach ($name in @('src','tests','installer','scripts','docs','.github')) {
    $files += Get-ChildItem -LiteralPath (Join-Path $workspace $name) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        ForEach-Object { $_.FullName.Substring($workspace.Length + 1) }
}
Add-Type -AssemblyName System.IO.Compression
$archivePath = Join-Path $OutputDirectory "ProxyChoiceApp-$version-source.zip"
$stream = [IO.File]::Open($archivePath, [IO.FileMode]::Create, [IO.FileAccess]::Write)
$archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        $entry = $archive.CreateEntry(("ProxyChoiceApp-$version/" + $file.Replace('\','/')), [IO.Compression.CompressionLevel]::Optimal)
        $input = [IO.File]::OpenRead((Join-Path $workspace $file))
        $output = $entry.Open()
        try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
    }
} finally { $archive.Dispose(); $stream.Dispose() }
$hashes = @('ProxyChoiceApp.exe','ProxyChoiceApp-Setup.exe',"ProxyChoiceApp-$version-source.zip") | ForEach-Object {
    $hash = Get-FileHash -LiteralPath (Join-Path $OutputDirectory $_) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + $_
}
$portableFiles = Get-ChildItem -LiteralPath $OutputDirectory -File | Where-Object { ($_.Name -eq 'ProxyChoiceApp.exe' -or $_.Extension -eq '.txt') -and $_.Name -ne 'SHA256SUMS.txt' }
$portable = Join-Path $OutputDirectory "ProxyChoiceApp-$version-win-x64.zip"
Compress-Archive -LiteralPath @($portableFiles.FullName) -DestinationPath $portable -Force
$portableStream = [IO.File]::Open($portable, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite)
$portableArchive = [IO.Compression.ZipArchive]::new($portableStream, [IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($guide in (Get-ChildItem -LiteralPath (Join-Path $OutputDirectory 'docs') -File)) {
        $entry = $portableArchive.CreateEntry('docs/' + $guide.Name, [IO.Compression.CompressionLevel]::Optimal)
        $input = [IO.File]::OpenRead($guide.FullName); $output = $entry.Open()
        try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
    }
} finally { $portableArchive.Dispose(); $portableStream.Dispose() }
$hashes += (Get-FileHash -LiteralPath $portable -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($portable)
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS.txt'), $hashes)
Get-Item -LiteralPath $archivePath | Select-Object Name, Length
