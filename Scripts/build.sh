echo "Downloading VSMac"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/bc5903bb-4aab-497e-a097-1eb1a5e02645/3ff8f8cb722e81e2538bece0b82fa44f/visualstudioformac-preview-17.3.0.2012-pre.4-x64.dmg

sudo hdiutil attach visualstudioformac-preview-17.3.0.2012-pre.4-x64.dmg

rm -rf ~/Library/Preferences/VisualStudio
rm -rf ~/Library/Preferences/Visual\ Studio
rm -rf ~/Library/Logs/VisualStudio
rm -rf ~/Library/VisualStudio
rm -rf ~/Library/Preferences/Xamarin/
rm -rf ~/Library/Developer/Xamarin
rm -rf ~/Library/Application\ Support/VisualStudio

echo "Installing VSMac 17.3 Preview"
ditto -rsrc "/Volumes/Visual Studio (Preview)/" /Applications/

echo "Installing dotnet 6.0.3xx"
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 6.0.3xx
dotnet workload install macos

echo "Building the extension"
dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore
cd Src/VimMac
dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Build

echo "Creating and installing Extension"
# Generate mpack extension artifact
dotnet msbuild VimMac.csproj /t:InstallAddin
