#$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\tweetinvi.dll"
#$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\TweetinviTemp.Core.dll"
#$d2 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\TweetinviTemp.Streams.dll"
#$d3 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\TweetinviTemp.WebLogic.dll" 
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
#Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
#cd "X:\benchmarks\tweetinvi"
#C:\SEAL\Bin\Checker.exe /in $d1 /in $d2 /in $d3 /in $d0 /outdir "X:\benchmarks\tweetinvi" > X:\benchmarks\results\tweetinvi
#Start-Sleep -Seconds 16000
$d0 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\tweetinvi.dll"
$d1 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.Core.dll"
$d2 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.Logic.dll"
$d3 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.Security.dll"
$d4 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.Factories.dll"
$d5 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.Controllers.dll"
$d6 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.WebLogic.dll" 
$d7 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.Streams.dll"
$d8 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\\TweetinviTemp.Credentials.dll" 
$d9 = "C:\Users\anirudh\Source\Repos\testdriverfordeadlock2\tweetinvi\bin\Debug\TweetinviTemp.dll" 
#C:\Users\anirudh\Downloads\4gb_patch\4gb_patch.exe "C:\SEAL\Bin\Checker.exe"
Copy-Item "C:\SEAL\DBs\old\*" C:\SEAL\DBs
cd "X:\benchmarks\tweetinvi"
C:\SEAL\Bin\Checker.exe /in $d1 /in $d2 /in $d3 /in $d4 /in $d5 /in $d6 /in $d7 /in $d8 /in $d9 /in $d0 /outdir "X:\benchmarks\tweetinvi" > X:\benchmarks\release\tweetinvi
