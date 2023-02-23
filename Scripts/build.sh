echo "Downloading VSMac"
url="https://download.visualstudio.microsoft.com/download/pr/93f43532-c75a-43bf-a335-9c62d3ad7c56/a0c1df1aa17141472362c80cef1a2581/visualstudioformac-preview-17.6.0.402-pre.1-x64.dmg"
wget --quiet $url

hdiutil attach `basename $url`

echo "Removing existing VSMac installation"
sudo rm -rf "/Applications/Visual Studio.app"

echo "Installing VSMac 17.6"
ditto -rsrc "/Volumes/Visual Studio (Preview)/" /Applications/
mv "/Applications/Visual Studio (Preview).app" "/Applications/Visual Studio.app"
ls -la /Applications/

echo "installing dotnet 7.0.2xx"
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 7.0.2xx
~/.dotnet/dotnet workload install macos

echo "Building the extension"
~/.dotnet/dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore
cd Src/VimMac
~/.dotnet/dotnet msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Build

echo "Creating and installing Extension"
# Generate mpack extension artifact
~/.dotnet/dotnet msbuild VimMac.csproj /t:InstallAddin
