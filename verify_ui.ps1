Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Write-Output "=== GUI UI Verification ===`n"

# Get the first GUI process with a main window
$guiProcess = Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' -and $_.MainWindowHandle -ne 0 } | Select-Object -First 1

if ($guiProcess -eq $null) {
    Write-Output "ERROR: No GUI.Application window found"
    Write-Output "Found processes without main windows:"
    Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' } | Format-Table Id, ProcessName, MainWindowTitle -AutoSize
    exit 1
}

Write-Output "✓ GUI.Application found (PID: $($guiProcess.Id), Title: '$($guiProcess.MainWindowTitle)')"

# Get the AutomationElement for the main window
$automationElement = [System.Windows.Automation.AutomationElement]::FromHandle($guiProcess.MainWindowHandle)

if ($automationElement -eq $null) {
    Write-Output "ERROR: Could not get automation element for window"
    exit 1
}

Write-Output "`n--- Searching for UI Elements ---`n"

# Find all tab items
$tabCondition = [System.Windows.Automation.Condition]::TrueCondition
$tabItems = $automationElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition) | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.TabItem' }

Write-Output "Tab Items Found: $($tabItems.Count)"
foreach ($tab in $tabItems) {
    Write-Output "  - $($tab.Current.Name)"
}

# Find all buttons
$buttons = $automationElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tabCondition) | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' }

Write-Output "`nButtons Found: $($buttons.Count)"
foreach ($button in $buttons) {
    Write-Output "  - $($button.Current.Name)"
}

# Check for specific elements
Write-Output "`n=== Verification Results ===`n"

$tab3Found = $false
$tab4Found = $false
$loadPdfBtnFound = $false
$loadConfigBtnFound = $false
$saveConfigBtnFound = $false

foreach ($tab in $tabItems) {
    if ($tab.Current.Name -like '*Parameter*Extraction*' -or $tab.Current.Name -eq 'Tab 3') {
        $tab3Found = $true
        Write-Output "✓ Tab 3 'Parameter Extraction' FOUND"
    }
    if ($tab.Current.Name -like '*Simulator*Control*' -or $tab.Current.Name -eq 'Tab 4') {
        $tab4Found = $true
        Write-Output "✓ Tab 4 'Simulator Control' FOUND"
    }
}

foreach ($button in $buttons) {
    if ($button.Current.Name -like '*Load*PDF*' -or $button.Current.Name -like '*Load*Datasheet*') {
        $loadPdfBtnFound = $true
        Write-Output "✓ 'Load PDF Datasheet' button FOUND"
    }
    if ($button.Current.Name -like '*Load*Config*') {
        $loadConfigBtnFound = $true
        Write-Output "✓ 'Load Config' button FOUND"
    }
    if ($button.Current.Name -like '*Save*Config*') {
        $saveConfigBtnFound = $true
        Write-Output "✓ 'Save Config' button FOUND"
    }
}

# Summary
Write-Output "`n=== Summary ==="
$allFound = $tab3Found -and $tab4Found -and $loadPdfBtnFound -and $loadConfigBtnFound -and $saveConfigBtnFound

if ($allFound) {
    Write-Output "✓ ALL REQUIRED ELEMENTS FOUND"
    Write-Output "  - Tab 3: Parameter Extraction"
    Write-Output "  - Tab 4: Simulator Control"
    Write-Output "  - Load PDF Datasheet button"
    Write-Output "  - Load Config button"
    Write-Output "  - Save Config button"
    exit 0
} else {
    Write-Output "✗ SOME ELEMENTS MISSING:"
    if (-not $tab3Found) { Write-Output "  ✗ Tab 3 'Parameter Extraction' NOT FOUND" }
    if (-not $tab4Found) { Write-Output "  ✗ Tab 4 'Simulator Control' NOT FOUND" }
    if (-not $loadPdfBtnFound) { Write-Output "  ✗ 'Load PDF Datasheet' button NOT FOUND" }
    if (-not $loadConfigBtnFound) { Write-Output "  ✗ 'Load Config' button NOT FOUND" }
    if (-not $saveConfigBtnFound) { Write-Output "  ✗ 'Save Config' button NOT FOUND" }
    exit 1
}
