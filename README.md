# ARBA-comun

Código fuente común y **contrato** compartido por todos los add-ins de Revit ARBA (armado de zapatas, cimientos
corridos, bloques con foso, vigas, columnas, losas y muros de contención, y el plugin de metrados). Versión del
contrato: **1.0.0** (etiqueta `v1.0.0`).

| Documento | Qué contiene |
|---|---|
| [`INVENTARIO.md`](INVENTARIO.md) | Fase 0: lo que hay hoy en cada repo (ramas, TargetFramework, particiones, marcado, parámetros, código duplicado, cinta, clasificación del metrado) y 17 incoherencias |
| [`CONTRATO.md`](CONTRATO.md) / [`contrato.json`](contrato.json) | Fase 1: parámetros compartidos con GUID fijo, regla de partición y prefijos, cinta, versionado y compatibilidad. El JSON lo validan los tests contra el código |
| [`INTEGRACION.md`](INTEGRACION.md) | Fase 3: pasos genéricos para integrar el código común en un add-in y migrar modelos existentes; orden recomendado |
| [`PROMPTS/`](PROMPTS) | Un prompt por repo, listo para pegar en una sesión de Claude Code abierta en ese repo |

## Cómo se consume: como código fuente, nunca como DLL

Revit carga todos los add-ins en el mismo proceso: dos add-ins con dos versiones de una misma `Arba.Comun.dll`
chocarían. Por eso cada add-in **compila el código común dentro de su propio ensamblado** (clases `internal`,
namespace `Arba.Comun`), tomándolo de un submódulo git:

```powershell
git submodule add https://github.com/Andy-rba30/ARBA-comun external/ARBA-comun
git -C external/ARBA-comun checkout v1.0.0
```

y en el `.csproj`, después de las propiedades del proyecto:

```xml
<PropertyGroup>
  <RevitVersion>2027</RevitVersion>   <!-- opcional: define REVIT2027; se infiere del TargetFramework si falta -->
</PropertyGroup>
<Import Project="external/ARBA-comun/Arba.Comun.props" />
```

`Arba.Comun.props` añade `<Compile Include="…/src/**/*.cs" Link="Arba.Comun/…" />`, enlaza `contrato.json` y
define `REVIT$(RevitVersion)`, `REVIT_2022_OR_OLDER` y `REVIT_2024_OR_NEWER` para la compilación condicional.

Compila en `net48` (Revit 2021-2024), `net8.0-windows` (2025-2026) y `net10.0-windows` (2027), con `UseWPF`.

## Qué hay en `src/`

| Clase | Para qué |
|---|---|
| `ArbaContract` | El contrato en constantes: versión, parámetros (`ArbaParam`: nombre, GUID, tipo, categorías), categorías y prefijos de partición (`ArbaPrefix`), nombres de la cinta, valores de texto |
| `ArbaSharedParams` | Asegura las definiciones compartidas y sus vínculos (archivo temporal, `SharedParametersFilename` restaurado en `finally`), migra parámetros homónimos antiguos conservando valores, y lee/escribe por GUID |
| `ArbaPartition` (+ `.Revit`) | Construye (`BuildFor(host, prefijo, código)`) y lee (`Parse`) particiones del contrato con tolerancia a las antiguas; categoría del anfitrión; escritura correcta del parámetro Partición (predefinido, con respaldo por nombre en español e inglés); reglas de filtro "empieza por" |
| `PartitionName` | Expansión de la plantilla de partición, unificando las siete copias (`{categoria}`, `{prefijo}`, `{marca}`, `{id}`, `{codigo}` y alias `{capa}`/`{cara}`/`{estribo}`/`{ala}`, `{tipo}`, `{familia}`, `{conjunto}`) |
| `ArbaOrigin` | Escribe y lee `ARBA - Origen` / `Código` / `Anfitrión`; `Find` y `Delete` de lo que creó un add-in en un anfitrión ("borrar y rearmar" sin Comentarios) |
| `ArbaMetrado` | Partida, material, peso, pernos y grupo MISCELANEOS en los elementos con peso propio; `PesoProtegido` para el plugin de metrados |
| `ArbaMigration` / `ArbaMigrateCommandBase` | Migración de modelos existentes sin rearmar: particiones antiguas → nuevas, origen, código y `Metrado - Elemento`; comando base para el botón "Migrar particiones y origen" |
| `ArbaRibbon` | Pestaña `ARBA`, paneles `IA` / `Acero` / `Metrados` / `Encofrado` en orden, desplegable `Acero`, botones sueltos del panel `Metrados`, iconos vectoriales comunes |
| `RevitTheme` | Tema oscuro WPF estilo Revit 2027 (idéntico en seis add-ins) |
| `NameMatch` | Regla de nombres de tipo de barra/gancho: `Unique`/`IsAmbiguous` (la de Bloques) y `First` (la antigua de los otros seis) |
| `ArbaRevit` | Diferencias de la API 2021-2027 en un sitio (`ElementId` int/long, reglas de filtro, categoría predefinida, lectura/escritura de parámetros) |

## Qué se deja fuera a propósito (y por qué)

Solo entra lo que está duplicado en dos o más repos **y** es estable:

| Candidato | Repos | Por qué queda fuera |
|---|---|---|
| `Log.cs` | solo Fosa_transformadores | No está duplicado. Cuando un segundo add-in lo necesite, entra tal cual (`%Temp%\<AddIn>.log`) |
| `Geometry2D.cs` | Losas, Zapatas, Bloques | Tres versiones divergentes y en evolución (Bloques añadió `CutExact` y rectas generales esta semana; Losas tiene `Support`/`JoistAxes`). Unificarla ahora obligaría a elegir una y arrastrar cambios de comportamiento a los otros dos |
| `Rectilinear.cs` | Vigas, Columnas, Cimientos | Idéntico en los tres y estable, pero es lógica de dominio (sección por rectángulos máximos) que no forma parte del contrato; candidato claro para 1.1 si los tres siguen iguales tras la integración |
| `AppConfig.cs`, `HostAnalysis.cs`, `RebarOptionsWindow.cs`, `RebarGenerator.cs` | 7 add-ins | Mismo patrón, contenido distinto por add-in |
| `BeamProfile`/`BeamSection`/`SpliceLayout`… | Vigas ↔ Cimientos | Fork divergente (hasta 197 líneas de diferencia) |
| Iconos de cada botón (`IconVigas`, `IconLosas`…) | uno por add-in | No duplicados; se quedan en el `RibbonApp` de cada repo |

## Comprobar que compila y pasa los tests

```bash
# tests de consola (sin Revit): ArbaPartition, PartitionName, NameMatch, GUID únicos, contrato.json ↔ código
cd tests && dotnet run -c Release

# compilación del código común contra Revit 2021..2027 (descarga los paquetes Nice3point.Revit.Api.*)
build/check-all.sh          # Linux/macOS
build/check-all.ps1         # Windows
dotnet build build/Arba.Comun.Check.csproj -c Release -p:RevitVersion=2025   # una sola versión
```

## Cambiar el contrato

1. Edita `contrato.json` **y** `src/ArbaContract.cs` **y** `CONTRATO.md`.
2. Sube `version` según las reglas de compatibilidad de `CONTRATO.md` §4 (MAJOR rompe, MINOR añade, PATCH documenta).
3. `cd tests && dotnet run` tiene que quedar en 0 fallos; `build/check-all.sh` en verde.
4. Etiqueta `vX.Y.Z` y actualiza el submódulo en cada add-in (`git -C external/ARBA-comun checkout vX.Y.Z`).
