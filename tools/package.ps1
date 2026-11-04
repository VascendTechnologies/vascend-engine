param(
    [ValidatePattern('^\d+\.\d+\.\d+([-.][A-Za-z0-9.-]+)?$')][string]$Version='1.0.0',
    [string]$Revision=''
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root 'artifacts\releases'
$name="VascendEngine-$Version-win-x64"
$package=Join-Path $output $name
if(Test-Path $package){throw "Package directory already exists: $package"}
New-Item $package -ItemType Directory -Force | Out-Null
foreach($entry in @(@('vascend-engine','VascendEngine','engine'),@('barra-vascend','BarraVascend','desktop'))){
    & dotnet publish (Join-Path $root "apps\$($entry[0])\src\$($entry[1]).csproj") -c Release -r win-x64 --self-contained true -p:Version=$Version -p:SourceRevisionId=$Revision -o (Join-Path $package "artifacts\$($entry[2])") --nologo
    if($LASTEXITCODE -ne 0){throw "Publish failed: $($entry[1])"}
}
foreach($directory in @('tools','docs','examples\community-example','apps\barra-vascend\assets','assets\scripts')){
    New-Item (Join-Path $package $directory) -ItemType Directory -Force | Out-Null
}
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') (Join-Path $package 'tools')
Copy-Item (Join-Path $root 'docs\EXTENSIONS*.md') (Join-Path $package 'docs')
Copy-Item (Join-Path $root 'examples\community-example\*') (Join-Path $package 'examples\community-example') -Recurse
Copy-Item (Join-Path $root 'apps\barra-vascend\assets\*') (Join-Path $package 'apps\barra-vascend\assets')
Copy-Item (Join-Path $root 'assets\scripts\barra-windows.ps1') (Join-Path $package 'assets\scripts')
Copy-Item (Join-Path $PSScriptRoot 'Install.cmd') $package
Copy-Item (Join-Path $root 'docs\INSTALL.txt') $package
@{version=$Version;revision=$Revision;platform='win-x64';selfContained=$true} | ConvertTo-Json | Set-Content (Join-Path $package 'release.json') -Encoding UTF8
$zip=Join-Path $output "$name.zip"
Compress-Archive -Path "$package\*" -DestinationPath $zip -CompressionLevel Optimal
$hash=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'),"$hash  $name.zip`n")
Write-Output "Release package: $zip"
