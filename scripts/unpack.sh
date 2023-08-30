unzip bmi.zip -d ~/to_deploy
rm ~/to_deploy/appsettings.json
rm bmi.zip
cp -r ~/to_deploy/* /var/bmirussian_ru
rm -r ~/to_deploy