param([string]$Screenshots='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$arguments=@()
if($Screenshots){$arguments=@('--screenshots',$Screenshots)}
& dotnet run --project (Join-Path $root 'tests\VascendEngine.SmokeTests') -c Release -- @arguments
if($LASTEXITCODE -ne 0){throw 'Smoke tests failed'}
