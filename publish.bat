set "THIS_DIR=%~dp0"
del %THIS_DIR%bmi.zip
cd BMIRussian_ru\
del bin /Q /S
dotnet publish -c Release -r linux-x64 --no-self-contained
cd bin\Release\net7.0\linux-x64\publish\
"C:\Program Files\7-Zip\7z.exe" a -r "%THIS_DIR%bmi.zip" -x!*.pdb 
cd %THIS_DIR%