$process = Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' } | Select-Object -First 1
if ($process) {
    Write-Output "GUI Process Found:"
    Write-Output "  ID: $($process.Id)"
    Write-Output "  MainWindowHandle: $($process.MainWindowHandle)"
    Write-Output "  MainWindowTitle: '$($process.MainWindowTitle)'"
} else {
    Write-Output "No GUI process found"
}
