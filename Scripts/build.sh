echo "Downloading VSMac"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/40d0790a-f7b7-44d3-a6e5-9bc5677fb1d7/28697450215d02cbec72e943d988f51f/visualstudioformacpreviewinstaller-17.0.0.149.dmg

sudo hdiutil attach visualstudioformacpreviewinstaller-17.0.0.149.dmg

echo "Removing pre-installed VSMac"
sudo rm -rf "/Applications/Visual Studio.app"
rm -rf ~/Library/Caches/VisualStudio
rm -rf ~/Library/Preferences/VisualStudio
rm -rf ~/Library/Preferences/Visual\ Studio
rm -rf ~/Library/Logs/VisualStudio
rm -rf ~/Library/VisualStudio
rm -rf ~/Library/Preferences/Xamarin/
rm -rf ~/Library/Developer/Xamarin
rm -rf ~/Library/Application\ Support/VisualStudio

echo "Installing VSMac 17.0 Preview"
ditto -rsrc "/Volumes/Visual Studio for Mac Preview Installer/" /Applications/

dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore /t:Build

# Generate mpack extension artifact
dotnet msbuild Src/VimMac/VimMac.csproj /t:InstallAddin
