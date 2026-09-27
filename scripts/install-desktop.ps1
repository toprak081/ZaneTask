# Builds ZaneTask and installs it as a desktop app for the current user:
#   program files -> %LOCALAPPDATA%\Programs\ZaneTask
#   your data     -> %LOCALAPPDATA%\ZaneTask   (never touched by this script, so reinstalling keeps it)
# Creates "ZaneTask" shortcuts on the Desktop and in the Start menu.
# Run it again at any time to update the app to the latest code.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$target = Join-Path $env:LOCALAPPDATA 'Programs\ZaneTask'
$exe = Join-Path $target 'ZaneTask.exe'

function Step($text) { Write-Host "`n> $text" -ForegroundColor Cyan }

if (Get-Process -Name 'ZaneTask', 'ZaneTask.Api' -ErrorAction SilentlyContinue) {
    Write-Host "ZaneTask is running. Close its window and run this again." -ForegroundColor Yellow
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "The .NET SDK was not found. Install .NET 10 from https://dotnet.microsoft.com/download" -ForegroundColor Red
    exit 1
}

$database = Join-Path $env:LOCALAPPDATA 'ZaneTask\zanetask.db'
if (Test-Path $database) {
    Step "Backing up your data"
    $backup = Join-Path (Split-Path $database) ("zanetask.backup-{0:yyyyMMdd-HHmmss}.db" -f (Get-Date))
    Copy-Item $database $backup
    Write-Host "  $backup"
    # Keep the 5 most recent backups.
    Get-ChildItem (Split-Path $database) -Filter 'zanetask.backup-*.db' |
        Sort-Object Name -Descending | Select-Object -Skip 5 | Remove-Item
}

Step "Removing the previous version (your data is kept)"
if (Test-Path $target) { Remove-Item -Recurse -Force $target }

Step "Building the server (this takes a minute)"
dotnet publish (Join-Path $repo 'src\ZaneTask.Api') -c Release -o (Join-Path $target 'server') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Building the server failed." }

Step "Building the app window"
dotnet publish (Join-Path $repo 'src\ZaneTask.Desktop') -c Release -o $target --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Building the app failed." }

Step "Creating shortcuts"
$shell = New-Object -ComObject WScript.Shell
$places = @(
    [Environment]::GetFolderPath('Desktop'),
    (Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs')
)
foreach ($folder in $places) {
    $shortcut = $shell.CreateShortcut((Join-Path $folder 'ZaneTask.lnk'))
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $target
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Description = 'ZaneTask - projects and tasks'
    $shortcut.Save()
}

Write-Host "`nZaneTask is installed. Double-click 'ZaneTask' on your Desktop to start it." -ForegroundColor Green
