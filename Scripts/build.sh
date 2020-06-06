echo "Downloading Mono"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/15b48a0a-f363-47d8-9687-b7b59dbafd10/7fb3f0ff97647cb6c24bbdc36f2aad9c/monoframework-mdk-6.10.0.104.macos10.xamarin.universal.pkg

sudo installer -pkg monoframework-mdk-6.10.0.104.macos10.xamarin.universal.pkg -target /

echo "Downloading VSMac"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/68ffa29a-6a5b-41f7-af7b-506ddcf4bbfc/6f601f3b5ff4d19ecd2c258517acc562/visualstudioformac-8.6.2.6.dmg

sudo hdiutil attach visualstudioformac-8.6.2.6.dmg

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

echo "Installing VSMac 8.6"
ditto -rsrc "/Volumes/Visual Studio/" /Applications/

msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore /t:Build

# Generate Vim.Mac.VsVim_2.8.0.0.mpack extension artifact
msbuild Src/VimMac/VimMac.csproj /t:InstallAddin
