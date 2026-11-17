
@echo off
setlocal

cd /d "%~dp0"
title Rajib Connect Desktop CRM

if not exist ".venv\Scripts\python.exe" (
    echo Virtual environment not found.
    echo Run setup.bat first.
    pause
    exit /b 1
)

REM Detect the Python installation directory dynamically
for /f "delims=" %%I in ('.venv\Scripts\python.exe -c "import sys; print(sys.base_prefix)"') do set "PYTHON_BASE=%%I"

REM Detect the Tcl/Tk version dynamically
for /f "delims=" %%I in ('.venv\Scripts\python.exe -c "import tkinter; print(str(tkinter.TclVersion))"') do set "TCL_VERSION=%%I"

REM Configure Tcl/Tk only when the detected directories exist
if exist "%PYTHON_BASE%\tcl\tcl%TCL_VERSION%" (
    set "TCL_LIBRARY=%PYTHON_BASE%\tcl\tcl%TCL_VERSION%"
)

if exist "%PYTHON_BASE%\tcl\tk%TCL_VERSION%" (
    set "TK_LIBRARY=%PYTHON_BASE%\tcl\tk%TCL_VERSION%"
)

.venv\Scripts\python.exe gui_client.py

if errorlevel 1 (
    echo.
    echo Rajib Connect stopped because of an error.
    pause
)

endlocal
