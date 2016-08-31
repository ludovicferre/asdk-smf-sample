@echo off

set smf=Altiris.ASDK.SMF
set ans=Altiris.NS
set adb=Altiris.Database
set acm=Altiris.Common


if "%1"=="7.5" goto build-7.5
if "%1"=="7.1" goto build-7.1
if "%1"=="8.0" goto build-8.00



:default build path

:build-7.6
set build=7.6

set gac=C:\Windows\Microsoft.NET\assembly\GAC_MSIL

set csc=@c:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

set ver1=v4.0_7.6.1383.0__d516cb311cfb6e4f
set ver2=v4.0_7.6.1395.0__d516cb311cfb6e4f
set ver3=v4.0_7.6.1383.0__99b1e4cc0d03f223
set ver3=v4.0_7.6.1085.0__d516cb311cfb6e4f

goto build

:build-7.5
set build=7.5
set gac=C:\Windows\Assembly\GAC_MSIL
set csc=@c:\Windows\Microsoft.NET\Framework\v2.0.50727\csc.exe

set ver1=7.5.3153.0__d516cb311cfb6e4f
set ver2=7.5.3219.0__d516cb311cfb6e4f
set ver3=7.5.3083.0__d516cb311cfb6e4f
set ver4=7.5.3153.0__99b1e4cc0d03f223

goto build

:build-7.1
set build=7.1

set gac=C:\Windows\Assembly\GAC_MSIL
set csc=@c:\Windows\Microsoft.NET\Framework\v2.0.50727\csc.exe

set ver1=7.1.8400.0__d516cb311cfb6e4f
set ver2=7.1.7858.0__d516cb311cfb6e4f
set ver3=7.1.8400.0__99b1e4cc0d03f223
set ver4=7.1.8400.0__d516cb311cfb6e4f

:build
cmd /c %csc% /reference:%gac%\%smf%\%ver3%\%smf%.dll /reference:%gac%\%ans%\%ver1%\%ans%.dll /reference:%gac%\%acm%\%ver1%\%acm%.dll /reference:%gac%\%adb%\%ver1%\%adb%.dll /out:UnknownSoftwareHandler-%build%.exe unknownsoftwarehandler.cs strings.cs
: cmd /c %csc% /reference:%gac%\%smf%\%ver3%\%smf%.dll /reference:%gac%\%ans%\%ver1%\%ans%.dll /reference:%gac%\%acm%\%ver1%\%acm%.dll /reference:%gac%\%adb%\%ver1%\%adb%.dll /out:SoftwareProductCLI-%build%.exe softwareimporter.cs strings.cs
: cmd /c %csc% /out:Curator.exe curator.cs
:end