@echo off
setlocal
if not defined LMSTUD_API_BASE set "LMSTUD_API_BASE=http://127.0.0.1:11434"

powershell -NoProfile -Command ^
 "$ErrorActionPreference = 'Stop'; $uri = $env:LMSTUD_API_BASE.TrimEnd('/') + '/v1/responses';" ^
 "$headers = @{}; if ($env:LMSTUD_API_TOKEN) { $headers.Authorization = 'Bearer ' + $env:LMSTUD_API_TOKEN };" ^
 "function Send-Response($body) { if ($env:LMSTUD_API_MODEL) { $body.model = $env:LMSTUD_API_MODEL }; Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 10) };" ^
 "$parent = Send-Response @{input = 'Hello'; store = $true}; $parent | ConvertTo-Json -Depth 10;" ^
 "Send-Response @{input = 'How are you?'; previous_response_id = $parent.id; store = $false} | ConvertTo-Json -Depth 10"
set "RESULT=%ERRORLEVEL%"
pause
exit /b %RESULT%
