$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\Orws365Client\bin\Debug\Microsoft.Office365.ReportingWebServiceClient.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\Orws365Client\bin\Debug\Orws365Client.dll"
cd "X:\benchmarks\o365rwsclient"
C:\SEAL\Bin\Checker.exe /in $d0 /in $d1 /outdir "X:\benchmarks\o365rwsclient" > X:\benchmarks\release-nofilter\o365rwsclient
