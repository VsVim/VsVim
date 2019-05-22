@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0Scripts\build.ps1""" -test %*"
