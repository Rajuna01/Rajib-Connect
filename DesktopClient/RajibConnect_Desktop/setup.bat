@echo off
setlocal
cd /d "%~dp0"
echo ============================================
echo   RAJIB CONNECT - DESKTOP CRM SETUP
echo ============================================
where py >nul 2>nul
if %errorlevel%==0 (
  py -3 -m venv .venv
) else (
  python -m venv .venv
)
if not exist ".venv\Scripts\python.exe" (
  echo Failed to create virtual environment. Check Python installation.
  pause
  exit /b 1
)
.venv\Scripts\python.exe -m pip install --upgrade pip
.venv\Scripts\python.exe -m pip install -r requirements.txt
if errorlevel 1 (
  echo Dependency installation failed.
  pause
  exit /b 1
)
echo.
echo Setup completed. Start the app using run.bat.
pause
