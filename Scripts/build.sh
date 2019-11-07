wget https://download.visualstudio.microsoft.com/download/pr/5b7dcb51-3035-46f7-a8cb-efe3a1da351c/dcba976cd3257636b6b2828575d29d3c/monoframework-mdk-6.4.0.208.macos10.xamarin.universal.pkg
sudo installer -pkg monoframework-mdk-6.4.0.208.macos10.xamarin.universal.pkg -target /

msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU" /t:Restore
msbuild /p:Configuration=ReleaseMac /p:Platform="Any CPU"
