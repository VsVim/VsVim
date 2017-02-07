
$outDir = join-path $PSScriptRoot "..\Binaries\Tools"
$outPath = join-path $outDir "NuGet.exe"
if (-not (test-path $outPath)) { 
    mkdir $outDir -ErrorAction SilentlyContinue | out-null
    invoke-webrequest -uri https://dist.nuget.org/win-x86-commandline/v3.5.0/NuGet.exe -outfile $outPath
}
