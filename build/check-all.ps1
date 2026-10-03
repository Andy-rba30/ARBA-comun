# Compila el código común contra Revit 2021..2027 (net48 / net8.0-windows / net10.0-windows).
$ErrorActionPreference = 'Continue'
Set-Location $PSScriptRoot
$fail = $false
foreach ($v in 2021..2027) {
  Write-Host "=== Revit $v" -NoNewline
  dotnet build Arba.Comun.Check.csproj -c Release -p:RevitVersion=$v -v q -nologo | Out-String | Set-Variable out
  if ($LASTEXITCODE -eq 0) { Write-Host ": OK" } else { Write-Host ": FALLO"; Write-Host $out; $fail = $true }
}
if ($fail) { exit 1 }
