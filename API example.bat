@echo off
setlocal enabledelayedexpansion
set BASE=http://10.0.1.13:11434

echo ==== Get available models ====
curl -s %BASE%/v1/model
echo.

echo ==== First chat message (no session) ====
curl -s -X POST %BASE%/v1/chat/completions ^
  -H "Content-Type: application/json" ^
  -d "{\"messages\":[{\"role\":\"user\",\"content\":\"Hello\"}]}" > response.json

for /f %%I in ('powershell -NoProfile -Command "(Get-Content response.json | ConvertFrom-Json).session_id"') do set SESSION=%%I
echo Session ID: !SESSION!
type response.json
echo.

echo ==== Follow-up message using session ====
curl -s -X POST %BASE%/v1/chat/completions ^
  -H "Content-Type: application/json" ^
  -d "{\"session_id\":\"!SESSION!\",\"messages\":[{\"role\":\"user\",\"content\":\"How are you?\"}]}"
echo.

echo ==== Reset session ====
curl -s -X POST %BASE%/v1/chat/reset ^
  -H "Content-Type: application/json" ^
  -d "{\"session_id\":\"!SESSION!\"}"
echo.

del response.json
endlocal
pause