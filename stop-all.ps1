# YiPix - 停止所有后台服务
Stop-Job * -ErrorAction SilentlyContinue
Remove-Job * -ErrorAction SilentlyContinue
Write-Host "All services stopped." -ForegroundColor Green
