$process = Get-Process | Where-Object { $_.ProcessName -like 'GUI*' } | Select-Object ProcessName, Id, MainWindowTitle
Write-Output "GUI Process Found:"
Write-Output $process

if ($process -ne $null) {
    Write-Output "`nProcess is running. Checking for main window..."
    Start-Sleep -Seconds 2

    # Try to find the window using UI Automation
    Add-Type -AssemblyName UIAutomationClient

    # Get the main window handle
    $hwnd = (Get-Process | Where-Object { $_.ProcessName -like 'GUI*' }).MainWindowHandle

    if ($hwnd -ne 0) {
        Write-Output "Main window handle: $hwnd"
    } else {
        Write-Output "No main window found (window may not be visible yet)"
    }
} else {
    Write-Output "No GUI process found"
}
