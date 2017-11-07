@echo off
set BINARIESDIR=%~dp0\..\Binaries
set USERFILEPATH=%BINARIESDIR%\User.props

if NOT exist %BINARIESDIR% mkdir %BINARIESDIR%

echo ^<?xml version="1.0" encoding="utf-8"?^> > %USERFILEPATH%
echo ^<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003"^> >> %USERFILEPATH%
echo    ^<PropertyGroup^> >> %USERFILEPATH%
echo      ^<VsVimTargetVersion^>%1^</VsVimTargetVersion^> >> %USERFILEPATH%
echo    ^</PropertyGroup^> >> %USERFILEPATH%
echo ^</Project^> >> %USERFILEPATH%
