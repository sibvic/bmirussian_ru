set "THIS_DIR=%~dp0"
del %THIS_DIR%bmi.zip
cd BMIRussian_ru\
del bin /Q /S
dotnet publish -c Release -r linux-x64 --no-self-contained
cd bin\Release\net8.0\linux-x64\publish\
"C:\Program Files\7-Zip\7z.exe" a -r "%THIS_DIR%bmi.zip" -x!*.pdb 
cd %THIS_DIR%
scp -i C:\Users\admin\.ssh\bmirussian_ru bmi.zip sibvic@158.160.116.99:~/bmi.zip
del bmi.zip
scp -i C:\Users\admin\.ssh\bmirussian_ru scripts/*.sh sibvic@158.160.116.99:~