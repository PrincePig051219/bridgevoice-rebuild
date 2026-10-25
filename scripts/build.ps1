$ErrorActionPreference='Stop'
$env:DOTNET_ROOT='E:\dev-tools\dotnet8'
$env:DOTNET_CLI_HOME='E:\dev-tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:NUGET_PACKAGES='E:\dev-tools\nuget'
$env:NUGET_HTTP_CACHE_PATH='E:\dev-tools\nuget-http'
$env:TEMP='E:\dev-tools\temp'
$env:TMP=$env:TEMP
$projectRoot=Split-Path -Parent $PSScriptRoot
& "$env:DOTNET_ROOT\dotnet.exe" publish "$projectRoot\src\Suikouda\Suikouda.csproj" -c Release -r win-x64 --self-contained true -o "$projectRoot\dist\Suikouda"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
