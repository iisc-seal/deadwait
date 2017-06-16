$sealhome = "C:\SEAL"
## set dll paths
#$mscorlib = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\mscorlib.dll"
#$system = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\system.dll"
#$syscore = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\system.core.dll"

$mscorlib = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll"
$system = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll"
$syscore = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"

#& Remove-Item C:\SEAL\DBs\puritydb-full-context-.net4.6

#$mscorlib = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1\mscorlib.dll"
#$system = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1\System.dll"
#$syscore = "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5.1\System.Core.dll"

##populate the database
& Write-Host "Processing stubs"
& "$sealhome\Bin\Checker.exe" "/in" "$sealhome\Stubs\Bin\Stubs.dll" "/config-file" "$sealhome\Configs\stubs-.NET4.config" /framework "true" /outdir "$sealhome\exp" #> stubOp.txt

& Write-Host "Processing mscorlib"
& "$sealhome\Bin\Checker.exe" "/in" $mscorlib "/config-file" "$sealhome\Configs\mscorlib-.NET4.config" /outdir "$sealhome\exp" /framework "true" #> mscorlibOp.txt

& Write-Host "Processing system"
& "$sealhome\Bin\Checker.exe" "/in" $system "/config-file" "$sealhome\Configs\system-.NET4.config" /outdir "$sealhome\exp" /framework "true" #> systemOp.txt

& Write-Host "Processing syscore"
& "$sealhome\Bin\Checker.exe" "/in" $syscore "/config-file" "$sealhome\Configs\system.core-.NET4.config" /outdir "$sealhome\exp" /framework "true" #> systemCoreOp.txt

& Write-Host "Processing orws365client"
#& C:\SEAL\Bin\Checker.exe  "/config-file" "$sealhome\Configs\libconfig.config" /in "C:\Users\Anirudh Laptop\Documents\code\Benchmarks\New\o365rwsclient\bin\Debug\Microsoft.Office365.ReportingWebServiceClient.dll" /framework "true"

& Write-Host "Processing numerousapp"
#& C:\SEAL\Bin\Checker.exe "/config-file" "$sealhome\Configs\libconfig.config"  /in "C:\Users\Anirudh Laptop\Documents\code\potential\TaskExtensions\numerousapp-net\Numerous.Api\bin\Debug\Numerous.Api.dll" /framework "true"

& Write-Host "Processing hbase"
#& C:\SEAL\Bin\Checker.exe "/config-file" "$sealhome\Configs\libconfig.config"  /in "C:\Users\Anirudh Laptop\Documents\code\Benchmarks\New\hbase-sdk-for-net\bin\debug\Microsoft.HBase.Client\Microsoft.HBase.Client.dll" /framework "true"

& Write-Host "Processing dinero"
#& C:\SEAL\Bin\Checker.exe "/config-file" "$sealhome\Configs\libconfig.config"  /in "C:\Users\Anirudh Laptop\Documents\code\potential\TaskExtensions\dinero-csharp-sdk\DineroClientSDK\DineroSDK\bin\Debug\DineroSDK.dll" /framework "true"

#$d1 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.Core.dll"
#$d2 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.Controllers.dll"
#$d3 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.Credentials.dll" 
#$d4 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.WebLogic.dll" 
#$d5 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.Streams.dll"
#$d6 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.Security.dll"
#$d7 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.Logic.dll"
#$d8 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.Factories.dll"
#$d9 = "C:\Users\Anirudh Laptop\Documents\code\potential\tweetinvi\TweetinviTemp\bin\Debug\TweetinviTemp.dll" 

#& Write-Host "Processing tweetinvi"
#& C:\SEAL\Bin\Checker.exe "/config-file" "$sealhome\Configs\libconfig.config"  /in $d1 /in $d2 /in $d3 /in $d4 /in $d5 /in $d6 /in $d7 /in $d8 /in $d9 /framework "true"

#& Write-Host "Processing sharefile"
#& C:\SEAL\Bin\Checker.exe "/config-file" "$sealhome\Configs\libconfig.config"  /in "C:\Users\Anirudh Laptop\Documents\code\potential\TaskExtensions\ShareFile-NET\Net45\bin\Debug\ShareFile.Api.Client.Core.dll" /in  "C:\Users\Anirudh Laptop\Documents\code\potential\TaskExtensions\ShareFile-NET\Net45\bin\Debug\ShareFile.Api.Client.Net45.dll" /framework "true"


#& Write-Host "Processing amqpnetlite"
#& C:\SEAL\Bin\Checker.exe "/config-file" "$sealhome\Configs\libconfig.config" /in "C:\Users\Anirudh Laptop\Documents\code\potential\amqpnetlite\bin\Debug\Amqp.Net\Amqp.Net.dll" /outdir "H:\benchmarks\amqp" /framework "true"
#& New-Item -ItemType Directory -Force -Path C:\SEAL\DBs\old
& Copy-Item "C:\SEAL\DBs\puritydb-full-context-.net4.6" C:\SEAL\DBs\old\