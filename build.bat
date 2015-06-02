@echo off

set smf=Altiris.ASDK.SMF


if "%1"=="7.6" goto build-7.6
if "%1"=="7.1" goto build-7.1

:default build path
:build-7.5
set build=7.5
set gac=C:\Windows\Assembly\GAC_MSIL
set csc=@c:\Windows\Microsoft.NET\Framework\v2.0.50727\csc.exe
set ver=7.5.3083.0__d516cb311cfb6e4f


goto build

:build-7.1
set build=7.1

set gac=C:\Windows\Assembly\GAC_MSIL
set csc=@c:\Windows\Microsoft.NET\Framework\v2.0.50727\csc.exe

set ver=7.1.8400.0__d516cb311cfb6e4f


goto build


:build-7.6
set build=7.6

set gac=C:\Windows\Microsoft.NET\assembly\GAC_MSIL
set csc=@c:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

set ver=v4.0_7.6.1383.0__d516cb311cfb6e4f



:build
cmd /c %csc% /reference:%gac%\%smf%\%ver%\%smf%.dll /out:SoftwareImporter-%build%.exe *.cs

:end