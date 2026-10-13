param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $workspace 'dist' }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory 'ProxyChoiceApp.exe'))) { throw 'Сначала выполните dotnet publish приложения.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Не найден стандартный компилятор .NET Framework Windows.' }
$staging = Join-Path $workspace 'artifacts\installer-build'
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$icon = Join-Path $workspace 'src\ProxyChoiceApp\Assets\app.ico'
$manifest = Join-Path $workspace 'src\ProxyChoiceApp\app.manifest'
$common = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/codepage:65001',"/win32icon:$icon","/win32manifest:$manifest",'/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll')
$common += '/reference:System.Web.Extensions.dll'
$languages = @()
foreach ($code in @('en','fr','es','zh','de','ru')) {
    $languages += "/resource:$(Join-Path $workspace "src\ProxyChoiceApp\Localization\$code.json"),language.$code.json"
    $languages += "/resource:$(Join-Path $workspace "src\ProxyChoiceApp\Assets\Flags\$code.png"),flag.$code.png"
}
& $compiler @common @languages "/out:$(Join-Path $staging 'Uninstall.exe')" (Join-Path $workspace 'installer\Uninstall.cs') (Join-Path $workspace 'installer\Texts.cs')
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки удаления.' }
$payload = @('ProxyChoiceApp.exe','Uninstall.exe','README.txt','LICENSE.txt','THIRD-PARTY-NOTICES.txt','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','WINDOWS-DESKTOP-LICENSE.txt')
$payload += @('docs/Guide-ru.md','docs/Guide-en.md','docs/Guide-de.md','docs/Guide-es.md')
$hashLines = @()
$resources = @()
foreach ($name in $payload) {
    $file = if ($name -eq 'Uninstall.exe') { Join-Path $staging $name } else { Join-Path $OutputDirectory $name }
    if (-not (Test-Path -LiteralPath $file)) { throw "Отсутствует файл $file" }
    $hashLines += "$((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash) $name"
    $resources += "/resource:$file,payload.$name"
}
$hashFile = Join-Path $staging 'payload.sha256'
$hashLines | Set-Content -LiteralPath $hashFile -Encoding utf8
$resources += "/resource:$hashFile,payload.sha256"
& $compiler @common @languages @resources "/out:$(Join-Path $OutputDirectory 'ProxyChoiceApp-Setup.exe')" (Join-Path $workspace 'installer\Setup.cs') (Join-Path $workspace 'installer\Texts.cs')
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки установщика.' }
Get-Item -LiteralPath (Join-Path $OutputDirectory 'ProxyChoiceApp-Setup.exe') | Select-Object Name,Length
