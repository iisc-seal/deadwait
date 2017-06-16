$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\Sharefile\bin\Debug\Sharefile.dll"
$d2 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\Sharefile\bin\Debug\ShareFile.Api.Client.Core.dll"
#$d3 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\bin\Debug\ShareFile.Api.Client.Net45.dll"
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
cd "X:\benchmarks\sharefile"
C:\SEAL\Bin\Checker.exe /config-file "C:\SEAL\Configs\sharefile.config" /in $d2 /in $d1 /outdir "X:\benchmarks\sharefile" > X:\benchmarks\release\sharefile
