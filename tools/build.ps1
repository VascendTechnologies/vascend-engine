param([ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'artifacts'
& dotnet publish (Join-Path $root 'apps\vascend-engine\src\VascendEngine.csproj') -c $Configuration -o (Join-Path $output 'engine') --nologo
if($LASTEXITCODE -ne 0){throw 'Pubblicazione Engine fallita'}
& dotnet publish (Join-Path $root 'apps\barra-vascend\src\BarraVascend.csproj') -c $Configuration -o (Join-Path $output 'desktop') --nologo
if($LASTEXITCODE -ne 0){throw 'Pubblicazione desktop fallita'}
Write-Output "Build pronta in $output"
