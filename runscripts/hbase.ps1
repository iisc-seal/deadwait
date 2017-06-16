$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\HBase\bin\Debug\HBase.dll"
$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\HBase\bin\Debug\Microsoft.HBase.Client.dll"
cd "X:\benchmarks\hbase"
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
C:\SEAL\Bin\Checker.exe /in $d0 /in $d1 /outdir "X:\benchmarks\hbase" > X:\benchmarks\release\hbase
