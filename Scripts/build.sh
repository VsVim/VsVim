wget https://download.visualstudio.microsoft.com/download/pr/5b7dcb51-3035-46f7-a8cb-efe3a1da351c/dcba976cd3257636b6b2828575d29d3c/monoframework-mdk-6.4.0.208.macos10.xamarin.universal.pkg
sudo installer -pkg monoframework-mdk-6.4.0.208.macos10.xamarin.universal.pkg -target /

wget https://download.visualstudio.microsoft.com/download/pr/82f53c42-6dc7-481b-82e1-c899bb15a753/df08f05921d42cc6b3b02e9cb082841f/visualstudioformac-8.4.0.2350.dmg

sudo hdiutil attach visualstudioformac-8.4.0.2350.dmg

ditto -rsrc "/Volumes/Visual Studio/" /Applications/

msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore /t:Build
