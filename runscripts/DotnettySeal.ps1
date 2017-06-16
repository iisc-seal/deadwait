$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockDotNetty\bin\Debug\DeadlockDotNetty.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockDotNetty\bin\Debug\DotNetty.Common.dll"
$d2 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockDotNetty\bin\Debug\DotNetty.Codecs.dll"
$d3 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockDotNetty\bin\Debug\DotNetty.Transport.dll"
$d4 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockDotNetty\bin\Debug\DotNetty.Buffers.dll"
$d5 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockDotNetty\bin\Debug\DotNetty.Handlers.dll"
cd "X:\benchmarks\dotnetty"
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
#C:\SEAL\Bin\Checker.exe /config-file "C:\SEAL\Configs\dotnetty.config" /in $d1 /in $d2 /in $d3 /in $d4 /in $d0 /outdir "X:\benchmarks\dotnetty" > X:\benchmarks\results\dotnetty
C:\SEAL\Bin\Checker.exe /in $d1 /in $d3 /in $d4 /in $d5 /in $d0 /outdir "X:\benchmarks\dotnetty" > X:\benchmarks\release\dotnetty
