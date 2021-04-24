echo "Downloading Mono"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/2516b6e5-6965-4f5b-af68-d1959a446e7a/443346a56436b5e2682b7c5b5b25e990/monoframework-mdk-6.12.0.125.macos10.xamarin.universal.pkg

sudo installer -pkg monoframework-mdk-6.12.0.125.macos10.xamarin.universal.pkg -target /

echo "Downloading VSMac"
wget --quiet https://download.visualstudio.microsoft.com/download/pr/e5b7cb77-1248-4fb7-a3fe-532ca3335f78/777b586636b0cdba9db15d69bf8d8b1f/visualstudioformac-8.9.7.8.dmg

sudo hdiutil attach visualstudioformac-8.9.7.8.dmg

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

echo "Installing VSMac 8.9"
ditto -rsrc "/Volumes/Visual Studio/" /Applications/

msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore /t:Build

# Generate Vim.Mac.VsVim_2.8.0.0.mpack extension artifact
msbuild Src/VimMac/VimMac.csproj /t:InstallAddin
