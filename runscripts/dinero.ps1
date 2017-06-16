$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\Dinero\bin\Debug\DineroAll.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\Dinero\bin\Debug\DineroSDK.dll"
cd "X:\benchmarks\dinero"
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
C:\SEAL\Bin\Checker.exe /in $d1 /in $d0 /outdir "X:\benchmarks\dinero" > X:\benchmarks\release\dinero
