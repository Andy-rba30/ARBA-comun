<#
.SYNOPSIS
  Actualiza el submódulo external/ARBA-comun a una etiqueta en varios repos de add-ins, confirma, sube y compila.

.EXAMPLE
  .\actualizar-arba-comun.ps1 -Version v1.0.2 -Repos "D:\Proyectos C#\METRADOS","D:\Proyectos C#\ACERO ZAPATAS"
  .\actualizar-arba-comun.ps1 -Version v1.0.2 -Repos (Get-Content repos.txt) -Build -RevitVersion 2027

.NOTES
  - Cada repo debe estar limpio (git status) y en la rama que se quiere actualizar; si no, se salta y se avisa.
  - No fusiona ni cambia de rama. Con -NoPush solo confirma en local.
  - Con -Build compila (Release; -p:RevitVersion para proyectos multi-versión). Revit debe estar cerrado
    si la compilación copia a la carpeta de add-ins.
#>
param(
    [Parameter(Mandatory = $true)] [string] $Version,
    [Parameter(Mandatory = $true)] [string[]] $Repos,
    [switch] $Build,
    [string] $RevitVersion = "2027",
    [switch] $NoPush
)

$ErrorActionPreference = 'Stop'
$resultado = @()

foreach ($repo in $Repos) {
    $fila = [ordered]@{ Repo = $repo; Rama = ''; Submodulo = ''; Estado = '' }
    try {
        if (-not (Test-Path (Join-Path $repo 'external\ARBA-comun'))) { $fila.Estado = 'sin submódulo external/ARBA-comun'; $resultado += [pscustomobject]$fila; continue }
        Push-Location $repo
        $fila.Rama = (git rev-parse --abbrev-ref HEAD).Trim()
        $sucio = git status --porcelain --untracked-files=no
        if ($sucio) { $fila.Estado = 'SALTADO: cambios locales sin confirmar'; Pop-Location; $resultado += [pscustomobject]$fila; continue }

        git submodule update --init external/ARBA-comun | Out-Null
        git -C external/ARBA-comun fetch --tags --quiet origin
        git -C external/ARBA-comun checkout --quiet $Version
        $fila.Submodulo = (git -C external/ARBA-comun describe --tags).Trim()

        $cambio = git status --porcelain -- external/ARBA-comun
        if (-not $cambio) {
            $fila.Estado = "ya estaba en $Version"
        }
        else {
            git add external/ARBA-comun
            git commit --quiet -m "Submodulo ARBA-comun $Version"
            if (-not $NoPush) { git push --quiet origin $fila.Rama }
            $fila.Estado = if ($NoPush) { 'confirmado (sin push)' } else { 'confirmado y subido' }
        }

        if ($Build) {
            $csproj = Get-ChildItem -Recurse -Filter *.csproj | Where-Object { $_.FullName -notmatch '\\(external|Tests|build)\\' } | Select-Object -First 1
            if ($csproj) {
                $args = @('build', $csproj.FullName, '-c', 'Release', '-nologo', '-v', 'q')
                if ((Get-Content $csproj.FullName -Raw) -match 'RevitVersion') { $args += "-p:RevitVersion=$RevitVersion" }
                $salida = & dotnet @args 2>&1
                $fila.Estado += if ($LASTEXITCODE -eq 0) { '; compila' } else { '; ERROR al compilar: ' + (($salida | Select-String 'error' | Select-Object -First 3) -join ' | ') }
            }
        }
        Pop-Location
    }
    catch {
        $fila.Estado = 'ERROR: ' + $_.Exception.Message
        if ((Get-Location).Path -eq $repo) { Pop-Location }
    }
    $resultado += [pscustomobject]$fila
}

$resultado | Format-Table -AutoSize
if ($resultado | Where-Object { $_.Estado -match 'ERROR|SALTADO' }) { exit 1 }
