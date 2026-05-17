@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "BASE=http://127.0.0.1:11434"

rem ---- Make a unique, safe filename like response_20251003_091423_217.json
for /f %%I in ('powershell -NoProfile -Command "[DateTime]::UtcNow.ToString(\"yyyyMMdd_HHmmss_fff\")"') do set "STAMP=%%I"
set "RESP=response_!STAMP!.json"
set "REQ=request_!STAMP!.json"

echo ==== First chat message (no session) ====
> "!REQ!" echo {"messages":[{"role":"user","content":"Hello"}]}
curl -s -X POST %BASE%/v1/responses ^
  -H "Content-Type: application/json" ^
  --data-binary "@!REQ!" > "!RESP!"

set "SESSION="
for /f %%I in ('powershell -NoProfile -Command "(Get-Content -Raw !RESP! | ConvertFrom-Json).session_id"') do set "SESSION=%%I"
echo Using response file: !RESP!
echo Session ID: !SESSION!
type "!RESP!"
echo.

if defined SESSION (
  echo ==== Follow-up message using session ====
  > "!REQ!" echo {"session_id":"!SESSION!","messages":[{"role":"user","content":"How are you?"}]}
  curl -s -X POST %BASE%/v1/responses ^
    -H "Content-Type: application/json" ^
    --data-binary "@!REQ!"
  echo.
) else (
  echo ==== Follow-up skipped: no session_id returned ====
)

echo ==== One-off message (store=false, session removed after reply) ====
> "!REQ!" echo {"messages":[{"role":"user","content":"This should not be stored"}],"store":false}
curl -s -X POST %BASE%/v1/responses ^
  -H "Content-Type: application/json" ^
  --data-binary "@!REQ!"
echo.

if defined SESSION (
  echo ==== Reset session ====
  > "!REQ!" echo {"session_id":"!SESSION!"}
  curl -s -X POST %BASE%/v1/reset ^
    -H "Content-Type: application/json" ^
    --data-binary "@!REQ!"
  echo.
)
echo.

rem ---- Delete the unique response file (comment this out to keep logs)
del "!RESP!"
del "!REQ!"

endlocal
pause
