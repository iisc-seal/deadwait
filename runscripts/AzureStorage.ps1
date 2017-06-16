$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAzureStorage\bin\Debug\DeadlockAzureStorage.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAzureStorage\bin\Debug\Microsoft.Rest.ClientRuntime.dll"
$d2 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAzureStorage\bin\Debug\Microsoft.Rest.ClientRuntime.Azure.dll"
$d3 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\DeadlockAzureStorage\bin\Debug\Microsoft.Azure.Management.Storage.dll"
cd "X:\benchmarks\AzureStorage"
C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
C:\SEAL\Bin\Checker.exe /config-file "C:\SEAL\Configs\azureStorage.config" /in $d1 /in $d2 /in $d3 /in $d0 /outdir "X:\benchmarks\Storage" > X:\benchmarks\release-nofilter\azure-storage-3
