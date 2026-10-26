param([string]$Dotnet, [switch]$SkipTests, [string]$OutputDirectory = 'dist', [string]$DependencyDirectory)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $workspace
$OutputDirectory = [IO.Path]::GetFullPath((Join-Path $workspace $OutputDirectory))
if (-not $DependencyDirectory) { $DependencyDirectory = Join-Path $workspace '.tools' }
$DependencyDirectory = [IO.Path]::GetFullPath($DependencyDirectory)
if (-not $Dotnet) {
    $Dotnet = Join-Path $workspace '.tools\dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $Dotnet)) {
        $command = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($command -and (& $command.Source --list-sdks)) { $Dotnet = $command.Source }
        else { throw 'Нужен .NET 10 SDK. Установите его или передайте путь: .\scripts\Build.ps1 -Dotnet C:\path\dotnet.exe' }
    }
}
$previousAppData = $env:APPDATA
try {
    $env:DOTNET_CLI_HOME = Join-Path $workspace '.tools\dotnet-home'
    $env:APPDATA = Join-Path $workspace '.tools\build-appdata'
    $env:NUGET_PACKAGES = Join-Path $DependencyDirectory 'packages'
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $workspace '.tools\nuget-cache'
    $env:NUGET_SCRATCH = Join-Path $workspace '.tools\nuget-scratch'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    & (Join-Path $PSScriptRoot 'Test-PublicPackage.ps1') -SourceDirectory $workspace -ReportPath (Join-Path $workspace 'artifacts\privacy-source.json')
    if (-not (Test-Path -LiteralPath 'vendor\singbox.zip')) {
        New-Item -ItemType Directory -Path 'vendor' -Force | Out-Null
        Invoke-WebRequest -Uri 'https://github.com/SagerNet/sing-box/releases/download/v1.14.3/sing-box-1.14.3-windows-amd64.zip' -OutFile 'vendor\singbox.zip'
    }
    if ((Get-FileHash -LiteralPath 'vendor\singbox.zip' -Algorithm SHA256).Hash -ne 'd8b86ef58936dba9230ec6a57ffa4243933f3c14ef4809828b6c553440ed1518') { throw 'Неверная контрольная сумма движка.' }
    if (-not (Test-Path -LiteralPath 'vendor\upstream-source\sing-box-v1.14.3-source.zip')) {
        New-Item -ItemType Directory -Path 'vendor\upstream-source' -Force | Out-Null
        Invoke-WebRequest -Uri 'https://github.com/SagerNet/sing-box/archive/refs/tags/v1.14.3.zip' -OutFile 'vendor\upstream-source\sing-box-v1.14.3-source.zip'
    }
    if ((Get-FileHash -LiteralPath 'vendor\upstream-source\sing-box-v1.14.3-source.zip' -Algorithm SHA256).Hash -ne '5bf11eeb2f5b93377166af1ecdcf24dd2c4be3c92bcb43c09d95edd01eb85949') { throw 'Invalid upstream source checksum.' }
    & (Join-Path $PSScriptRoot 'New-Icons.ps1')
    & $Dotnet restore 'src\ProxyChoiceApp\ProxyChoiceApp.csproj' --configfile 'NuGet.Config' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось восстановить зависимости.' }
    if (-not $SkipTests) {
        & $Dotnet restore 'tests\ProxyChoiceApp.Tests\ProxyChoiceApp.Tests.csproj' --configfile 'NuGet.Config' --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось восстановить тесты.' }
        & $Dotnet build 'tests\ProxyChoiceApp.Tests\ProxyChoiceApp.Tests.csproj' -c Release --no-restore --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать тесты.' }
        & '.\tests\ProxyChoiceApp.Tests\bin\Release\net10.0-windows\win-x64\ProxyChoiceApp.Tests.exe' (Join-Path $workspace 'artifacts\tests')
        if ($LASTEXITCODE -ne 0) { throw 'Проверки не прошли.' }
    }
    & $Dotnet publish 'src\ProxyChoiceApp\ProxyChoiceApp.csproj' -c Release --no-restore -o $OutputDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать приложение.' }
    foreach ($name in @('README.txt','LICENSE.txt','THIRD-PARTY-NOTICES.txt')) { Copy-Item -LiteralPath (Join-Path $workspace $name) -Destination (Join-Path $OutputDirectory $name) -Force }
    Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.runtime.win-x64\10.0.12\LICENSE.TXT') -Destination (Join-Path $OutputDirectory 'DOTNET-LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.netcore.app.runtime.win-x64\10.0.12\THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $OutputDirectory 'DOTNET-THIRD-PARTY-NOTICES.txt') -Force
    Copy-Item -LiteralPath (Join-Path $env:NUGET_PACKAGES 'microsoft.windowsdesktop.app.runtime.win-x64\10.0.12\LICENSE') -Destination (Join-Path $OutputDirectory 'WINDOWS-DESKTOP-LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $workspace 'docs') -Destination $OutputDirectory -Recurse -Force
    & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -OutputDirectory $OutputDirectory
    & (Join-Path $PSScriptRoot 'Package-Source.ps1') -OutputDirectory $OutputDirectory
} finally { $env:APPDATA = $previousAppData }
