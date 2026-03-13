# Kill existing
Get-Process | Where-Object { $_.ProcessName -eq 'GUI.Application' } | Stop-Process -Force
Start-Sleep -Seconds 1

# Set environment variables to help debugging
$env:DOTNET_ENVIRONMENT = "Development"

# Run directly
& "D:\workspace-github\system-emul-sim\tools\GUI.Application\src\GUI.Application\bin\Debug\net8.0-windows\GUI.Application.exe"

Write-Output "Application exited with code: $LASTEXITCODE"
