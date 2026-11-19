[CmdletBinding()]
param(
    [switch]$NoAutoStart
)

$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$publishDir = Join-Path $projectDir 'publish'
$project = Join-Path $projectDir 'GammaToggle.csproj'

dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать Gamma Toggle.' }

$exe = Join-Path $publishDir 'GammaToggle.exe'
if (-not $NoAutoStart) {
    $startup = [Environment]::GetFolderPath('Startup')
    $shortcutPath = Join-Path $startup 'Gamma Toggle.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $publishDir
    $shortcut.Description = 'Циклическое переключение гаммы по Ctrl + Num0'
    $shortcut.Save()
    Write-Host "Автозапуск добавлен: $shortcutPath"
}

Start-Process -FilePath $exe
Write-Host 'Gamma Toggle запущен. Нажмите Ctrl + Num0.'
