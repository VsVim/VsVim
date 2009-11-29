
$root = (split-path -parent $MyInvocation.MyCommand.Definition) 
pushd $root
mstest /testsettings:Local.testsettings /testcontainer:FsVimTest\Bin\Debug\FsVimTest.dll
popd
