param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$engine=Join-Path $env:USERPROFILE '.config\vascend-engine'
$desktop=Join-Path $env:USERPROFILE '.config\barra-vascend'
foreach($name in @('VascendEngine','BarraVascend')){
    if(Get-Process $name -ErrorAction SilentlyContinue){throw "Chiudi $name prima di installare."}
}
if(!$SkipBuild){& (Join-Path $PSScriptRoot 'build.ps1')}
foreach($name in @('engine','desktop')){
    if(!(Test-Path (Join-Path $root "artifacts\$name"))){throw 'Build mancante: esegui tools\build.ps1'}
}
$stamp=Get-Date -Format 'yyyyMMdd-HHmmss'
foreach($target in @($engine,$desktop)){
    if(Test-Path "$target\bin"){
        New-Item -ItemType Directory -Path "$target\backups\$stamp" -Force | Out-Null
        Copy-Item "$target\bin" "$target\backups\$stamp\bin" -Recurse
    }
    New-Item -ItemType Directory -Path "$target\bin" -Force | Out-Null
}
Copy-Item (Join-Path $root 'artifacts\engine\*') "$engine\bin" -Recurse -Force
Copy-Item (Join-Path $root 'artifacts\desktop\*') "$desktop\bin" -Recurse -Force
Copy-Item (Join-Path $root 'apps\barra-vascend\assets\*') $desktop -Force
Copy-Item (Join-Path $root 'docs\EXTENSIONS.md') "$engine\EXTENSIONS.md" -Force
New-Item -ItemType Directory -Path "$engine\community-example" -Force | Out-Null
Copy-Item (Join-Path $root 'examples\community-example\*') "$engine\community-example" -Recurse -Force
$taskbarScript=Join-Path $env:USERPROFILE '.config\yasb\barra-windows.ps1'
if(!(Test-Path $taskbarScript)){
    New-Item -ItemType Directory -Path (Split-Path $taskbarScript) -Force | Out-Null
    Copy-Item (Join-Path $root 'assets\scripts\barra-windows.ps1') $taskbarScript
}
Write-Output "Installato: $engine\bin\VascendEngine.exe"
Write-Output 'Vascend Technologies resta disattivata fino alla scelta esplicita dalla home.'
