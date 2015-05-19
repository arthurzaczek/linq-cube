"Creating LinqCube.nuspec from template with a version from GitVersion" | out-host

rm *.nupkg

msbuild ..\LinqCube.sln /p:Configuration=Release

$str = (..\packages\GitVersion.CommandLine.3.0.0-beta0002\Tools\GitVersion.exe) | out-string
$json = ConvertFrom-Json $str

$version = $json.NuGetVersionV2
"  Version = $version" | out-host

Get-Content LinqCube.nuspec.template | Foreach-object { 
	$_ -replace '##VERSION##', $version 
} | Set-Content -encoding "UTF8" LinqCube.nuspec

..\.nuget\nuget.exe pack .\LinqCube.nuspec