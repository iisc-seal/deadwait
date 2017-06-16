$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\amqp\bin\Debug\amqp.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\amqp\bin\Debug\Amqp.Net.dll"
#Start-Sleep -Seconds 1500
cd "X:\benchmarks\amqp"
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
C:\SEAL\Bin\Checker.exe /config-file "C:\SEAL\Configs\amqp.config" /in $d1 /in $d0 /outdir "X:\benchmarks\amqp" > X:\benchmarks\release\amqp
