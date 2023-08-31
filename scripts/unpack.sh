unzip bmi.zip -d ~/to_deploy
rm ~/to_deploy/appsettings.json
rm bmi.zip
systemctl stop bmirussian_ru.service
cp -r ~/to_deploy/* /var/bmirussian_ru
systemctl start bmirussian_ru.service
rm -r ~/to_deploy