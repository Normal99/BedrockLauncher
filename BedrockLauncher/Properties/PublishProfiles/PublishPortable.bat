@echo off
rem Builds a self-contained ("portable") copy of BedrockLauncher that runs
rem without installing the .NET Desktop Runtime (no admin rights needed).
rem Output: BedrockLauncher\bin\Portable\win-x64\
pushd "%~dp0..\.."
dotnet publish BedrockLauncher.csproj ^
--configuration Release ^
--runtime win-x64 ^
--self-contained true ^
--force ^
--output bin\Portable\win-x64\
set PUBLISH_EXIT=%ERRORLEVEL%
popd
exit /b %PUBLISH_EXIT%
