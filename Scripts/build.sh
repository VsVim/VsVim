echo "Downloading VSMac"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/abec5a47-e411-463e-9668-cf62db9ac526/6d94780d075d9ac6db45f8b9570fb873/visualstudioformac-preview-17.3.0.1038-pre.2.1-x64.dmg

sudo hdiutil attach visualstudioformac-preview-17.3.0.1038-pre.2.1-x64.dmg

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
