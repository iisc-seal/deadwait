$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAuthentication\bin\Debug\DeadlockAuthentication.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAuthentication\bin\Debug\Microsoft.Rest.ClientRuntime.dll"
$d2 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAuthentication\bin\Debug\Microsoft.Rest.ClientRuntime.Azure.Authentication.dll"
$d3 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAuthentication\bin\Debug\Microsoft.Azure.Common.Authentication.dll"

cd "X:\benchmarks\Authentication"
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
C:\SEAL\Bin\Checker.exe /in $d1 /in $d2 /in $d3 /in $d0 /outdir "X:\benchmarks\Authentication" > "X:\benchmarks\release\authentication"
