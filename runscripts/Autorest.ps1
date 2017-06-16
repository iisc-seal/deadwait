$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAutorest\bin\Debug\DeadlockAutorest.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAutorest\bin\Debug\AutoRest.Core.dll"
$d2 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAutorest\bin\Debug\Microsoft.Rest.ClientRuntime.dll"
cd "X:\benchmarks\Autorest"
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
C:\SEAL\Bin\Checker.exe /in $d1 /in $d0 /outdir "X:\benchmarks\Autorest" > X:\benchmarks\release\autorest
