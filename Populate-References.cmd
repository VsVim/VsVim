set SOURCE=%~dp0
set TARGET=%SOURCE%\Populate-References.ps1
powershell -ExecutionPolicy RemoteSigned -File "%TARGET%"
