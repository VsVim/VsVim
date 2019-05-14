param (
    [string]$vsVersion = "2019",
    [string]$vsDir = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview")

Set-StrictMode -version 2.0
$ErrorActionPreference="Stop"

[string]$rootDir = Split-Path -parent $MyInvocation.MyCommand.Definition 
[string]$rootDir = Resolve-Path (Join-Path $rootDir "..")
[string]$refDir = Join-Path $rootDir "References\Vs$vsVersion"
$locations = @(
    "Common7\IDE\PublicAssemblies\Microsoft.VisualStudio.Threading.16.0",
    "VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0",
    "Common7\IDE",
    "Common7\IDE\CommonExtensions\Microsoft\Editor",
    "Common7\IDE\PublicAssemblies",
    "Common7\IDE\PrivateAssemblies"
)

Push-Location $refDir 
try {
    foreach ($filePath in Get-ChildItem "*.dll") {
        $fileName = Split-Path -Leaf $filePath
        Write-Host "Copying $fileName"
        $copied = $false
        foreach ($location in $locations) {
            $sourceDir = Join-Path $vsDir $location
            $sourceFilePath = Join-Path $sourceDir $fileName
            if (Test-Path $sourceFilePath) {
                Copy-Item $sourceFilePath $refDir
                $copied = $true
                break
            }
        }

        if (-not $copied) {
            Write-Host "Could not find $filePath"
        }
    }

}
catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}


