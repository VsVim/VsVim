param ([switch]$fast = $false)
[string]$script:scriptPath = split-path -parent $MyInvocation.MyCommand.Definition 
pushd $scriptPath

function generateSharedService() {
    param ([string]$version, [string]$sourceFile)

    $destFile = split-path -leaf $sourceFile
    $destPath = join-path (join-path ".." $version) $destFile

    $lines = new-object System.Collections.ArrayList
    $lines.Add("// !!! Generated file. Do not edit directly !!!") | out-null

    foreach ($line in gc $sourceFile) {
        $line = $line.Replace("`$version`$", $version)
        $lines.Add($line) | out-null
    }

    sc $destPath $lines
}

generateSharedService "Vs2012" "SharedService.cs"
generateSharedService "Vs2012" "SharedService.NoLazy.cs"
generateSharedService "Vs2013" "SharedService.cs"
generateSharedService "Vs2013" "SharedService.Lazy.cs"
generateSharedService "Vs2015" "SharedService.cs"
generateSharedService "Vs2015" "SharedService.Lazy.cs"
generateSharedService "Vs2017" "SharedService.cs"
generateSharedService "Vs2017" "SharedService.Lazy.cs"
popd
