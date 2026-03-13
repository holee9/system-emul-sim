# Kill existing
Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Set E2E mode to avoid Dispatcher saturation
$env:XRAY_E2E_MODE = "true"

# Launch from the correct directory
$exePath = "D:\workspace-github\system-emul-sim\tools\GUI.Application\src\GUI.Application\bin\Debug\net8.0-windows\GUI.Application.exe"
$workingDir = "D:\workspace-github\system-emul-sim\tools\GUI.Application\src\GUI.Application\bin\Debug\net8.0-windows"

Write-Output "Launching from: $workingDir"
$processInfo = New-Object System.Diagnostics.ProcessStartInfo
$processInfo.FileName = $exePath
$processInfo.WorkingDirectory = $workingDir
$processInfo.UseShellExecute = $false
$processInfo.RedirectStandardOutput = $true
$processInfo.RedirectStandardError = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $processInfo
$process.Start() | Out-Null

$stdout = $process.StandardOutput.ReadToEnd()
$stderr = $process.StandardError.ReadToEnd()

Write-Output "Process ID: $($process.Id)"
Write-Output "MainWindowHandle before wait: $($process.MainWindowHandle)"

# Wait for window to appear
Write-Output "Waiting for window..."
Start-Sleep -Seconds 3

$process.Refresh()
Write-Output "MainWindowHandle after wait: $($process.MainWindowHandle)"
Write-Output "MainWindowTitle: '$($process.MainWindowTitle)'"

if ($stdout) { Write-Output "`n=== STDOUT ===`n$stdout" }
if ($stderr) { Write-Output "`n=== STDERR ===`n$stderr" }

# Now check if we can find the window elements
if ($process.MainWindowHandle -ne 0) {
    Write-Output "`n=== Window Found! Verifying UI Elements ===`n"

    Add-Type -AssemblyName UIAutomationClient
    $ae = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)

    $tabCondition = [System.Windows.Automation.Condition]::TrueCondition
    $allElements = $ae.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition)

    Write-Output "Total UI Elements: $($allElements.Count)"

    $tabs = $allElements | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.TabItem' }
    Write-Output "`nTab Items ($($tabs.Count)):"
    foreach ($tab in $tabs) { Write-Output "  - '$($tab.Current.Name)'" }

    $buttons = $allElements | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' }
    Write-Output "`nButtons ($($buttons.Count)):"
    foreach ($btn in $buttons) { Write-Output "  - '$($btn.Current.Name)'" }

    Write-Output "`n=== Verification ==="
    $tab3 = $tabs | Where-Object { $_.Current.Name -match 'Parameter.*Extraction|Tab 3' }
    $tab4 = $tabs | Where-Object { $_.Current.Name -match 'Simulator.*Control|Tab 4' }
    $loadPdf = $buttons | Where-Object { $_.Current.Name -match 'Load.*PDF|Load.*Datasheet' }
    $loadCfg = $buttons | Where-Object { $_.Current.Name -match 'Load.*Config' }
    $saveCfg = $buttons | Where-Object { $_.Current.Name -match 'Save.*Config' }

    Write-Output "Tab 3 (Parameter Extraction): $(if ($tab3) { '✓ FOUND' } else { '✗ NOT FOUND' })"
    Write-Output "Tab 4 (Simulator Control): $(if ($tab4) { '✓ FOUND' } else { '✗ NOT FOUND' })"
    Write-Output "Load PDF Datasheet button: $(if ($loadPdf) { '✓ FOUND' } else { '✗ NOT FOUND' })"
    Write-Output "Load Config button: $(if ($loadCfg) { '✓ FOUND' } else { '✗ NOT FOUND' })"
    Write-Output "Save Config button: $(if ($saveCfg) { '✓ FOUND' } else { '✗ NOT FOUND' })"

    if ($tab3 -and $tab4 -and $loadPdf -and $loadCfg -and $saveCfg) {
        Write-Output "`n✓ ALL REQUIRED ELEMENTS FOUND"
        exit 0
    } else {
        Write-Output "`n✗ SOME ELEMENTS MISSING"
        exit 1
    }
} else {
    Write-Output "`n✗ No main window found"
    exit 1
}
