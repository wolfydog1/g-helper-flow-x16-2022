@echo off
title Push G-Helper Flow X16 Edition to GitHub
echo ========================================================
echo Pushing repository to https://github.com/wolfydog1/g-helper-flow-x16-2022.git
echo ========================================================
cd /d "C:\Users\Kevin\Downloads\g-helper-main\g-helper-main"
git push -u origin main
echo.
if %ERRORLEVEL% equ 0 (
    echo ========================================================
    echo SUCCESS! Your code and README have been pushed to GitHub:
    echo https://github.com/wolfydog1/g-helper-flow-x16-2022
    echo ========================================================
) else (
    echo ========================================================
    echo Push encountered an error or needs authentication.
    echo ========================================================
)
pause
