# Universal Video Downloader - Modern Installer Script
# Installs UVD to Local AppData, creates shortcuts, and adds uninstall entry.

$AppName = "Universal Video Downloader"
$InstallDir = Join-Path $env:LocalAppData "Programs\UniversalVideoDownloader"
$ExeName = "UniversalVideoDownloader.exe"
$SourceExe = Join-Path $PSScriptRoot "bin\Release\net10.0-windows\win-x64\publish\$ExeName"

# Fallback path checks
if (-not (Test-Path $SourceExe)) {
    $SourceExe = Join-Path $PSScriptRoot "publish\$ExeName"
}
if (-not (Test-Path $SourceExe)) {
    $SourceExe = Join-Path $PSScriptRoot $ExeName
}

if (-not (Test-Path $SourceExe)) {
    Write-Error "Could not find $ExeName to install!"
    [System.Windows.MessageBox]::Show("Installation failed: Could not locate $ExeName. Please publish the project first.", "Installation Error", 0, 16)
    Exit
}

# Create installation directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Copy executable
Copy-Item $SourceExe (Join-Path $InstallDir $ExeName) -Force

# Create Desktop Shortcut
$WshShell = New-Object -ComObject WScript.Shell
$DesktopPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
$Shortcut = $WshShell.CreateShortcut((Join-Path $DesktopPath "$AppName.lnk"))
$Shortcut.TargetPath = Join-Path $InstallDir $ExeName
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Save()

# Create Start Menu Shortcut
$StartMenuPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::StartMenu)
$ProgramsPath = Join-Path $StartMenuPath "Programs"
$Shortcut = $WshShell.CreateShortcut((Join-Path $ProgramsPath "$AppName.lnk"))
$Shortcut.TargetPath = Join-Path $InstallDir $ExeName
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Save()

# Register in Windows Add/Remove Programs (Registry for Current User)
$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UniversalVideoDownloader"
if (-not (Test-Path $RegPath)) {
    New-Item -Path $RegPath -Force | Out-Null
}

Set-ItemProperty -Path $RegPath -Name "DisplayName" -Value $AppName
Set-ItemProperty -Path $RegPath -Name "DisplayIcon" -Value (Join-Path $InstallDir $ExeName)
Set-ItemProperty -Path $RegPath -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $RegPath -Name "Publisher" -Value "KAKA91"
Set-ItemProperty -Path $RegPath -Name "InstallLocation" -Value $InstallDir

# Create Uninstall script
$UninstallScript = Join-Path $InstallDir "Uninstall.ps1"
$UninstallContent = @"
`$RegPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UniversalVideoDownloader"
`$InstallDir = "`$env:LocalAppData\Programs\UniversalVideoDownloader"
`$DesktopPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::Desktop)
`$StartMenuPath = [System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::StartMenu)

# Remove registry keys
if (Test-Path `$RegPath) { Remove-Item `$RegPath -Recurse -Force }

# Remove shortcuts
Remove-Item (Join-Path `$DesktopPath "$AppName.lnk") -ErrorAction Ignore
Remove-Item (Join-Path `$StartMenuPath "Programs\$AppName.lnk") -ErrorAction Ignore

# Delete files
Remove-Item `$InstallDir -Recurse -Force -ErrorAction Ignore
[System.Windows.MessageBox]::Show("$AppName has been successfully uninstalled.", "Uninstall Complete", 0, 64)
"@
Set-Content -Path $UninstallScript -Value $UninstallContent

# Set Registry UninstallString
$UninstallCmd = "powershell.exe -ExecutionPolicy Bypass -File `"$UninstallScript`""
Set-ItemProperty -Path $RegPath -Name "UninstallString" -Value $UninstallCmd

[System.Windows.MessageBox]::Show("Universal Video Downloader has been successfully installed!`nShortcuts created on Desktop and Start Menu.", "Installation Complete", 0, 64)
