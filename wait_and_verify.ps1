Write-Output "Waiting for GUI window to appear..."
Start-Sleep -Seconds 3

$guiProcesses = Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' }
Write-Output "`nFound $($guiProcesses.Count) GUI.Application processes"

foreach ($proc in $guiProcesses) {
    Write-Output "`nProcess ID: $($proc.Id)"
    Write-Output "  MainWindowHandle: $($proc.MainWindowHandle)"
    Write-Output "  MainWindowTitle: '$($proc.MainWindowTitle)'"
    Write-Output "  Responding: $($proc.Responding)"
    Write-Output "  HasExited: $($proc.HasExited)"

    if ($proc.MainWindowHandle -ne 0) {
        Write-Output "  ✓ Has main window"

        # Try to get window info
        try {
            Add-Type -AssemblyName UIAutomationClient
            $ae = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
            Write-Output "  AutomationElement: Success"
            Write-Output "    Name: '$($ae.Current.Name)'"
            Write-Output "    ClassName: '$($ae.Current.ClassName)'"
        } catch {
            Write-Output "  AutomationElement: Error - $_"
        }
    } else {
        Write-Output "  ✗ No main window (invisible or not yet created)"
    }
}

# Check if there's a visible window by looking at the main process
Write-Output "`n=== Detailed Process Info ==="
$mainProcess = $guiProcesses | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1

if ($mainProcess) {
    Write-Output "✓ Found visible window process"
} else {
    Write-Output "✗ No visible window found - checking for errors"

    # Check for any .NET runtime errors in event log (recent)
    Write-Output "`nChecking for recent application errors..."
    $errors = Get-WinEvent -FilterHashtable @{LogName='Application'; Level=2; StartTime=(Get-Date).AddMinutes(-5)} -MaxEvents 5 -ErrorAction SilentlyContinue
    if ($errors) {
        foreach ($err in $errors) {
            Write-Output "  [$($err.TimeCreated)] $($err.ProcessName) - $($err.Id): $($err.Message)"
        }
    }
}
