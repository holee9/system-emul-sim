# Kill all existing GUI.Application processes
Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' } | Stop-Process -Force
Write-Output "Killed existing GUI processes"
Start-Sleep -Seconds 1

# Launch the GUI application and capture output
$exePath = "D:\workspace-github\system-emul-sim\tools\GUI.Application\src\GUI.Application\bin\Debug\net8.0-windows\GUI.Application.exe"

Write-Output "Launching: $exePath"
Start-Process -FilePath $exePath -RedirectStandardOutput "gui_output.txt" -RedirectStandardError "gui_error.txt"
Write-Output "Process started"

# Wait and check
Start-Sleep -Seconds 5

$proc = Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' } | Select-Object -First 1
if ($proc) {
    Write-Output "`nProcess Info:"
    Write-Output "  ID: $($proc.Id)"
    Write-Output "  MainWindowHandle: $($proc.MainWindowHandle)"
    Write-Output "  MainWindowTitle: '$($proc.MainWindowTitle)'"
} else {
    Write-Output "`nNo process found"

    # Check output files
    if (Test-Path "gui_output.txt") {
        Write-Output "`n=== STDOUT ==="
        Get-Content "gui_output.txt"
    }
    if (Test-Path "gui_error.txt") {
        Write-Output "`n=== STDERR ==="
        Get-Content "gui_error.txt"
    }
}
