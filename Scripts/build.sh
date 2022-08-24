echo "Downloading VSMac"
url="https://download.visualstudio.microsoft.com/download/pr/e81e04d3-768a-4310-9c9b-f32e8ba00eaa/889c20580d7989524e9b42726510452e/visualstudioformac-17.3.0.2102-x64.dmg"
wget --quiet $url

hdiutil attach `basename $url`

echo "Installing VSMac 17.3 Preview"
ditto -rsrc "/Volumes/Visual Studio (Preview)/" /Applications/
mv "/Applications/Visual Studio (Preview).app" "/Applications/Visual Studio.app"

echo "installing dotnet 6.0.3xx"
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 6.0.3xx
sudo ~/.dotnet/dotnet workload install macos

echo "Building the extension"
~/.dotnet/dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore
cd Src/VimMac
~/.dotnet/dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Build

echo "Creating and installing Extension"
# Generate mpack extension artifact
~/.dotnet/dotnet msbuild VimMac.csproj /t:InstallAddin
