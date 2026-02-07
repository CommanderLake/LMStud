@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "BASE=http://127.0.0.1:11434"

rem ---- Make a unique, safe filename like response_20251003_091423_217.json
for /f %%I in ('powershell -NoProfile -Command "[DateTime]::UtcNow.ToString(\"yyyyMMdd_HHmmss_fff\")"') do set "STAMP=%%I"
set "RESP=response_!STAMP!.json"

echo ==== Get available models ====
curl -s %BASE%/v1/model
echo.

echo ==== First chat message (no session) ====
curl -s -X POST %BASE%/v1/responses ^
  -H "Content-Type: application/json" ^
  -d "{\"messages\":[{\"role\":\"user\",\"content\":\"Hello\"}]}" > "!RESP!"

for /f %%I in ('powershell -NoProfile -Command "(Get-Content !RESP! | ConvertFrom-Json).session_id"') do set "SESSION=%%I"
echo Using response file: !RESP!
echo Session ID: !SESSION!
type "!RESP!"
echo.

echo ==== Follow-up message using session ====
curl -s -X POST %BASE%/v1/responses ^
  -H "Content-Type: application/json" ^
  -d "{\"session_id\":\"!SESSION!\",\"messages\":[{\"role\":\"user\",\"content\":\"How are you?\"}]}"
echo.

echo ==== Reset session ====
curl -s -X POST %BASE%/v1/reset ^
  -H "Content-Type: application/json" ^
  -d "{\"session_id\":\"!SESSION!\"}"
echo.

rem ---- Delete the unique response file (comment this out to keep logs)
del "!RESP!"

endlocal
pause