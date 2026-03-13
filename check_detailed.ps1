# Kill existing
Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Set environment for detailed logging
$env:XRAY_E2E_MODE = "true"
$env:SERILOG_LOG_LEVEL = "Verbose"

Write-Output "=== Launching GUI.Application ===`n"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "D:\workspace-github\system-emul-sim\tools\GUI.Application\src\GUI.Application\bin\Debug\net8.0-windows\GUI.Application.exe"
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.WorkingDirectory = "D:\workspace-github\system-emul-sim\tools\GUI.Application\src\GUI.Application\bin\Debug\net8.0-windows"

$p = New-Object System.Diagnostics.Process
$p.StartInfo = $psi
$p.Start() | Out-Null

Write-Output "Process started: PID $($p.Id)"
Write-Output "Waiting for window initialization..."

# Wait and check multiple times
for ($i = 1; $i -le 10; $i++) {
    Start-Sleep -Seconds 1
    $p.Refresh()

    $handle = $p.MainWindowHandle
    $title = $p.MainWindowTitle
    $hasExited = $p.HasExited

    Write-Output "[$i] Handle: $handle | Title: '$title' | Exited: $hasExited"

    if ($handle -ne 0) {
        Write-Output "`n✓ Window found! Handle: $handle, Title: '$title'"
        break
    }

    if ($hasExited) {
        Write-Output "`n✗ Process exited unexpectedly"
        $stdout = $p.StandardOutput.ReadToEnd()
        $stderr = $p.StandardError.ReadToEnd()
        if ($stdout) { Write-Output "`nSTDOUT:`n$stdout" }
        if ($stderr) { Write-Output "`nSTDERR:`n$stderr" }
        Write-Output "`nExit Code: $($p.ExitCode)"
        break
    }
}

# Final status check
if (-not $p.HasExited -and $p.MainWindowHandle -eq 0) {
    Write-Output "`n✗ Process still running but no window after 10 seconds"

    # Try to get thread info
    try {
        $threads = Get-Process -Id $p.Id | Select-Object -ExpandProperty Threads
        Write-Output "`nThread count: $($threads.Count)"
        Write-Output "Threads state:"
        $threads | Group-Object ThreadState | ForEach-Object {
            Write-Output "  $($_.Name): $($_.Count)"
        }
    } catch {
        Write-Output "Could not get thread info: $_"
    }
}

# Check for recent error events in Application log
Write-Output "`n=== Checking Application Event Log for recent errors ==="
try {
    $events = Get-WinEvent -FilterHashtable @{LogName='Application'; Level=2; StartTime=(Get-Date).AddMinutes(-2)} -MaxEvents 10 -ErrorAction SilentlyContinue
    if ($events) {
        foreach ($e in $events) {
            if ($e.ProcessName -like '*GUI*' -or $e.Message -like '*GUI*Application*') {
                Write-Output "`n[$($e.TimeCreated)] $($e.ProcessName) - Event ID: $($e.Id)"
                Write-Output "  $($e.Message.Substring(0, [Math]::Min(200, $e.Message.Length)))..."
            }
        }
    } else {
        Write-Output "No recent error events found"
    }
} catch {
    Write-Output "Could not read event log: $_"
}
