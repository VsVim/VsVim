@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0Scripts\Build.ps1""" -build %*"
