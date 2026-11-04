param([Parameter(Mandatory=$true)][string]$PackageZip)
$ErrorActionPreference='Stop'
$zip=(Resolve-Path $PackageZip).Path
$output=Split-Path $zip -Parent
$expected=([IO.File]::ReadAllText("$output\SHA256SUMS.txt").Split(' ')[0]).Trim()
if((Get-FileHash $zip).Hash.ToLowerInvariant() -ne $expected){throw 'ZIP checksum mismatch'}
$test=Join-Path $env:TEMP ('vascend-release-test-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$package=Join-Path $test 'extracted'
Expand-Archive $zip $package
foreach($app in @('engine','desktop')){
    $name=if($app -eq 'engine'){'VascendEngine'}else{'BarraVascend'}
    $bin=Join-Path $package "artifacts\$app"
    foreach($file in @("$name.exe",'coreclr.dll','hostfxr.dll','PresentationFramework.dll')){if(!(Test-Path "$bin\$file")){throw "Missing runtime file: $app/$file"}}
    $runtime=Get-Content "$bin\$name.runtimeconfig.json" -Raw | ConvertFrom-Json
    if(!$runtime.runtimeOptions.includedFrameworks){throw 'Not self-contained'}
    if((Get-Item "$bin\$name.exe").VersionInfo.ProductVersion -notlike '1.0.0*'){throw 'Incorrect executable version'}
}
$forbidden=Get-ChildItem $package -Recurse -File | Where-Object {$_.Name -in @('stile.json','extension-state.json','app-preferences.json','.credentials.json') -or $_.Name -like '*.log'}
if($forbidden){throw 'Local runtime data found in package'}
$previousProfile=$env:USERPROFILE
$env:USERPROFILE=Join-Path $test 'profile'
try{
    & "$package\tools\install.ps1" -NoShortcut
    $root=Join-Path $env:USERPROFILE '.config\vascend-engine'
    if(!(Test-Path "$root\bin\VascendEngine.exe")){throw 'Engine not installed'}
    if(!(Test-Path "$root\EXTENSIONS.it.md")){throw 'Translated documentation not installed'}
    if(!(Test-Path "$env:USERPROFILE\.config\barra-vascend\bin\BarraVascend.exe")){throw 'Desktop component not installed'}
    if(!(Test-Path "$env:USERPROFILE\.config\yasb\barra-windows.ps1")){throw 'Taskbar support not installed'}
    $preferences=@{'stile.json'='{"name":"test-style"}';'extension-state.json'='{"vascend-technologies":false}';'app-preferences.json'='{"language":"it"}'}
    foreach($file in $preferences.Keys){[IO.File]::WriteAllText("$root\$file",$preferences[$file])}
    & "$package\tools\install.ps1" -NoShortcut
    foreach($file in $preferences.Keys){if([IO.File]::ReadAllText("$root\$file") -ne $preferences[$file]){throw "Update modified $file"}}
    if(!(Get-ChildItem "$root\backups" -Recurse -Filter VascendEngine.exe)){throw 'Previous binary backup missing'}
    Write-Output 'PASS extracted ZIP, SHA256, bundled runtime, version, clean payload, fresh install, update, saved preferences and binary backups.'
}finally{$env:USERPROFILE=$previousProfile}
