echo "Downloading VSMac"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/a643750b-8690-4e7b-a088-9dfc3b2865ba/f315ac486e00a8c3954df7127c0bf526/visualstudioformac-preview-17.0.0.8001-pre.7-x64.dmg

sudo hdiutil attach visualstudioformac-preview-17.0.0.8001-pre.7-x64.dmg

rm -rf ~/Library/Preferences/VisualStudio
rm -rf ~/Library/Preferences/Visual\ Studio
rm -rf ~/Library/Logs/VisualStudio
rm -rf ~/Library/VisualStudio
rm -rf ~/Library/Preferences/Xamarin/
rm -rf ~/Library/Developer/Xamarin
rm -rf ~/Library/Application\ Support/VisualStudio

echo "Installing VSMac 17.0 Preview"
ditto -rsrc "/Volumes/Visual Studio (Preview)/" /Applications/

echo "Installing dotnet 6.0.1xx"
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 6.0.1xx

echo "Building the extension"
dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore /t:Build

echo "Creating and installing Extension"
# Generate mpack extension artifact
dotnet msbuild Src/VimMac/VimMac.csproj /t:InstallAddin
