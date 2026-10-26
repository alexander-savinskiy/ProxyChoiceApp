param([string]$SourceDirectory, [string]$ArchivePath, [string]$ReportPath)
$ErrorActionPreference = 'Stop'
if (-not $SourceDirectory) { $SourceDirectory = Split-Path -Parent $PSScriptRoot }
$SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory)
$issues = [Collections.Generic.List[string]]::new()
$files = @()
foreach ($directory in @('src','tests','installer','scripts','docs','.github')) {
    $files += Get-ChildItem -LiteralPath (Join-Path $SourceDirectory $directory) -Recurse -File |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
}
foreach ($name in @('README.md','README.txt','CHANGELOG.md','global.json','.gitignore')) { $files += Get-Item -LiteralPath (Join-Path $SourceDirectory $name) }
function Test-AllowedAddress([string]$value) {
    $address = $null
    if (-not [Net.IPAddress]::TryParse($value, [ref]$address)) { return $true }
    $bytes = $address.GetAddressBytes()
    if ($value -in @('0.0.0.0','1.1.1.1','172.30.254.1','172.30.254.2','172.31.255.2')) { return $true }
    return $bytes[0] -eq 127 -or
        ($bytes[0] -eq 192 -and $bytes[1] -eq 0 -and $bytes[2] -eq 2) -or
        ($bytes[0] -eq 198 -and $bytes[1] -eq 51 -and $bytes[2] -eq 100) -or
        ($bytes[0] -eq 203 -and $bytes[1] -eq 0 -and $bytes[2] -eq 113) -or
        ($bytes[0] -eq 198 -and $bytes[1] -eq 18 -and $bytes[2] -eq 0 -and $bytes[3] -eq 0)
}
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($SourceDirectory, $file.FullName).Replace('\','/')
    if ($file.Name -match '(?i)\.(dat|log|pem|key|pfx|p12|rdp)$' -or
        ($file.Extension -eq '.conf' -and $relative -ne 'docs/work-client.example.conf')) { $issues.Add("Private file type: $relative") }
    if ($file.Extension -notin @('.cs','.csproj','.xaml','.manifest','.json','.md','.txt','.ps1','.yml','.yaml','.conf')) { continue }
    $text = [IO.File]::ReadAllText($file.FullName)
    $networkText = if ($file.Extension -eq '.manifest') { [regex]::Replace($text, '\bversion="[\d.]+"', 'version="assembly-version"') } else { $text }
    foreach ($match in [regex]::Matches($networkText, '(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])')) {
        if (-not (Test-AllowedAddress $match.Value)) { $issues.Add("Non-documentation network address: $relative"); break }
    }
    if ($text -match '(?im)\b(?:PrivateKey|PreSharedKey|private_key|pre_shared_key)\b\s*(?:=|:)\s*["'']?[A-Za-z0-9+/]{43}=') { $issues.Add("Literal private key: $relative") }
}
$template = [IO.File]::ReadAllText((Join-Path $SourceDirectory 'docs/work-client.example.conf'))
if ($template -match '\b(?:\d{1,3}\.){3}\d{1,3}\b|[A-Za-z0-9+/]{43}=') { $issues.Add('The import template contains a concrete address or key.') }
foreach ($field in @('PrivateKey','Address','PublicKey','AllowedIPs','Endpoint')) {
    if ($template -notmatch ('(?m)^' + $field + '\s*=\s*REPLACE_WITH_')) { $issues.Add("Missing template placeholder: $field") }
}
if ($ArchivePath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName -match '(?i)(^|/)(profiles?\.dat|core\.json|.*\.log|.*\.rdp|\.env.*)$|(^|/)(\.git|\.tools|artifacts|bin|obj|preview-data)/') { $issues.Add("Private archive entry: $($entry.FullName)") }
            if ($entry.FullName.EndsWith('.conf', [StringComparison]::OrdinalIgnoreCase) -and -not $entry.FullName.EndsWith('/docs/work-client.example.conf', [StringComparison]::OrdinalIgnoreCase)) { $issues.Add('Unexpected VPN profile in archive.') }
        }
    } finally { $archive.Dispose() }
}
$report = [ordered]@{status=if($issues.Count -eq 0){'passed'}else{'failed'};checkedFiles=$files.Count;importTemplate='placeholders only';issues=@($issues)}
$json = $report | ConvertTo-Json -Depth 4
if ($ReportPath) { $parent = Split-Path -Parent ([IO.Path]::GetFullPath($ReportPath)); [IO.Directory]::CreateDirectory($parent) | Out-Null; [IO.File]::WriteAllText([IO.Path]::GetFullPath($ReportPath), $json) }
if ($issues.Count -ne 0) { throw $json }
$json
