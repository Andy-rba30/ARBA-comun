# Guía de integración de ARBA-comun en un add-in

Pasos genéricos. Cada repo tiene además su prompt adaptado en `PROMPTS/`, con los archivos concretos que cambian.

## 0. Antes de empezar

- Lee `CONTRATO.md` (parámetros, partición, cinta) y `README.md` (qué hay en `src/`).
- Trabaja en una rama (`claude/integrar-arba-comun`), compila con `dotnet build` y prueba en Revit antes de fusionar.
- **Nunca** cambies el código de `external/ARBA-comun` desde el add-in: si falta algo, anótalo en un
  `NOTAS-ARBA-COMUN.md` del repo y sigue; el cambio se hace en ARBA-comun con su versión.

## 1. Añadir el submódulo

```powershell
git submodule add https://github.com/Andy-rba30/ARBA-comun external/ARBA-comun
git -C external/ARBA-comun checkout v1.0.0
git add .gitmodules external/ARBA-comun
```

Quien clone el repo después necesita `git clone --recurse-submodules …` o `git submodule update --init`
(ponlo en el README / INSTALADOR del add-in). Para subir de versión: `git -C external/ARBA-comun checkout v1.1.0`
y commit del puntero.

## 2. Importar el `.props`

En el `.csproj`, después del `PropertyGroup` principal:

```xml
<PropertyGroup>
  <RevitVersion>2027</RevitVersion>
  <EnableWindowsTargeting>true</EnableWindowsTargeting>   <!-- si no estaba: permite compilar fuera de Windows -->
</PropertyGroup>
<Import Project="external/ARBA-comun/Arba.Comun.props" />
```

El `.props` compila `external/ARBA-comun/src/**/*.cs` dentro del ensamblado del add-in (clases `internal` en
`namespace Arba.Comun`), define `REVIT2027` (o la versión indicada) y, desde 1.0.3, excluye la carpeta del submódulo
del glob por defecto del SDK (`DefaultItemExcludes`): sin eso `**/*.cs` compilaba `src/` dos veces (CS2002) y
arrastraba `tests/` y `build/` del común. Con versiones anteriores hay que ponerlo en el `.csproj`, **antes** del
`Import`: `<DefaultItemExcludes>$(DefaultItemExcludes);external/**</DefaultItemExcludes>`. No añade paquetes: el add-in sigue trayendo
`Nice3point.Revit.Api.RevitAPI/RevitAPIUI` (o `RevitAPI.dll` por `HintPath`) y `UseWPF`. Un proyecto que referencia
`RevitAPI.dll` por `HintPath` puede añadir, como respaldo para compilar sin Revit (Linux, CI), los paquetes
`Nice3point.Revit.Api.*` con `Condition="!Exists('$(RevitInstallDir)\RevitAPI.dll')"` y `ExcludeAssets="runtime"`.

Si el proyecto de tests de consola del add-in enlaza `..\PartitionName.cs`, cámbialo por los archivos puros del
común: `..\external\ARBA-comun\src\ArbaContract.cs`, `ArbaPartition.cs`, `PartitionName.cs`, `NameMatch.cs`.

## 3. Sustituir el código duplicado

| En el add-in | Qué hacer |
|---|---|
| Clase `ArbaRibbon` dentro de `RibbonApp.cs` | Borrarla (queda la de `Arba.Comun`). Los iconos propios (`IconVigas`, `IconLosas`, `IconZapatas`, `IconBloques`) que estaban dentro de `ArbaRibbon` pasan a la clase `RibbonApp` del add-in. `ArbaRibbon.AddToPulldown(app, PanelAceroName, PanelAceroName, data)` → `ArbaRibbon.AddAcero(app, data)` |
| `RevitTheme.cs` | Borrar el archivo; `RevitTheme.Apply(...)` y los pinceles siguen igual |
| `PartitionName.cs` | Borrar el archivo. `PartitionName.Source` común tiene `Mark, Id, TypeName, FamilyName, SetName, Code, Category, Prefix`: lo que antes era `Layer`, `Face`, `Stirrup`, `Wing` o `Family` (código F1…F8 de Bloques) va en `Code`; `{capa}`, `{cara}`, `{estribo}` y `{ala}` siguen funcionando como alias de `{codigo}` |
| `RebarGenerator.MatchName` / `FindBarType` / `FindHookType` | `MatchName(...)` → `NameMatch.First(...)` (mismo comportamiento) o, mejor, `NameMatch.Unique` + `NameMatch.IsAmbiguous` para avisar como hace Bloques |
| `NameMatch.cs` (solo Bloques) | Borrar el archivo (misma API) |
| `using` | Añadir `using Arba.Comun;` donde se usen estas clases |

## 4. Plantilla de partición por defecto

- `AppConfig.PartitionTemplate` por defecto y `config.json` → `"{categoria} - {prefijo}-{marca}"` (Bloques:
  `"{categoria} - {prefijo}-{marca}-{codigo}"`).
- `HostAnalysis.Partition(cfg, …)` → `ArbaPartition.BuildFor(Host, ArbaContract.<Prefijo>, cfg.PartitionTemplate,
  new PartitionName.Source { Mark = Mark, Id = …, TypeName = TypeName, FamilyName = FamilyName, SetName = setName,
  Code = <capa/cara/estribo/ala/familia> })`. La categoría la deduce del anfitrión; el prefijo lo pone el add-in.
- En la ventana, si `!ArbaPartition.TemplateFollowsContract(cfg.PartitionTemplate)` muestra un aviso (la plantilla
  tiene que empezar por `{categoria} - {prefijo}-`) y enseña `ArbaContract.Version` en el pie o el informe.
- `PartitionName.Help` común describe los comodines nuevos.

## 5. Escribir ARBA - Origen / Código al crear

- Al abrir la transacción del comando, antes de la primera `SubTransaction`:
  `ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen, ArbaContract.Codigo, ArbaContract.Elemento }, avisos);
  doc.Regenerate();` (Bloques además `Anfitrion`, `Partida`, `Material`, `Peso`, `Pernos`). Los avisos van al informe.
- En `RebarGenerator.Finish(doc, rebar, partition)` añade el anfitrión y el código y escribe:
  `ArbaPartition.Write(rebar, partition);` (sustituye `LookupParameter("Partition")`, que no escribe nada en Revit en
  español) y `ArbaOrigin.WriteFor(rebar, host, ArbaContract.<Prefijo>, code);` (origen, código y `Metrado - Elemento`).
- Elementos que no son armaduras (rejillas, ángulos): `ArbaOrigin.WriteFor(fi, host, ArbaContract.Bloques, "REJILLA P1")`
  escribe también `ARBA - Anfitrión`; `ArbaMetrado.WriteMiscelaneo(fi, ArbaContract.PartidaRejillas, kg, pernos)`.

## 6. Borrar y rearmar

Antes de armar cada anfitrión: `List<Element> propias = ArbaOrigin.Find(doc, ArbaContract.<Prefijo>, host);`.
Si hay, pregunta una vez (TaskDialog con dos CommandLink como en Bloques): **Borrar la armadura del add-in y
rearmar** → dentro de la `SubTransaction` del elemento `ArbaOrigin.Delete(doc, prefijo, host, out int barras)` antes
de `Build`; **Conservar** → se arma encima (duplica). Un botón "Borrar armado del add-in" en la ventana es opcional.

Barras anteriores al contrato (partición antigua, sin origen): `ArbaMigration.HasLegacy(doc, host, prefijo)` → ofrece
`ArbaMigration.MigrateHost(doc, host, prefijo)` antes de rearmar; así pasan a reconocerse como propias. Por defecto la
migración asegura los ocho parámetros del contrato; un add-in de armado puede limitarlo a los tres suyos con
`ensure: ArbaMigration.RebarParams`. Un add-in que crea armaduras y otros elementos (Bloques) borra cada grupo por
separado con `ArbaOrigin.Delete(..., kind: ArbaOriginKind.Rebar)` / `ArbaOriginKind.NotRebar`.

## 7. Migración de modelos existentes: botón "Migrar particiones y origen"

Lo aporta **Exportacion-metrados-excel** (botón `ARBA_Metrados_Migrar`, clase pública `MigrarParticionesCommand :
IExternalCommand` con `[Transaction(TransactionMode.Manual)]` que delega en una subclase privada de
`ArbaMigrateCommandBase`, porque el código común es `internal` y una clase pública no puede heredar de él): sin
selección migra todo el modelo; con selección, los anfitriones elegidos. Convierte `ZAP-Z1` → `CIMIENTOS - ZAP-Z1`, `CC-C1` → `MUROS - CCO-C1` (categoría del anfitrión real),
`BLQ-FT-01-F1` → `CIMIENTOS - BLQ-FT-01-F1`, `LOSA-L1` → `LOSAS - LOS-L1`, `MC-M1` → `…- MCO-M1`; rellena
`ARBA - Origen`, `ARBA - Código` (si la partición lo llevaba) y `Metrado - Elemento`; no toca las particiones de solo
categoría ni las desconocidas; no crea ni borra barras; Ctrl+Z lo deshace. Los add-ins de armado no duplican el botón
(usan `MigrateHost` al armar, paso 6). Si un add-in quiere su propio botón: subclase de `ArbaMigrateCommandBase`
con `OnlyPrefix => ArbaContract.<Prefijo>` y `ArbaRibbon.AddAcero`.

## 8. Lista de verificación en Revit (para todos)

1. La cinta muestra una sola pestaña **ARBA** con el panel **Acero** y un solo desplegable **Acero** con los botones
   de los add-ins instalados (y el panel **Metrados** si está el plugin); sin pestaña "Metrados" aparte.
2. Al armar un elemento nuevo: en cada conjunto creado, Partición = `CATEGORIA - PREFIJO-marca[-codigo]` (p. ej.
   `CIMIENTOS - ZAP-Z1`), `ARBA - Origen` = el del add-in, `ARBA - Código` = capa/familia, `Metrado - Elemento` =
   categoría. Comprobar en Propiedades del conjunto y en **Gestionar > Parámetros de proyecto** (los del contrato
   aparecen como compartidos, de ejemplar, grupo Datos).
3. Armar el mismo elemento otra vez: pregunta "borrar y rearmar / conservar"; con "borrar" no quedan conjuntos
   duplicados.
4. Un modelo con barras antiguas (`ZAP-…`): el add-in ofrece migrar; tras migrar, la partición es la nueva y
   `ARBA - Origen` está relleno, sin barras nuevas.
5. `Application.SharedParametersFilename` del usuario (Gestionar > Parámetros compartidos) sigue apuntando a su
   archivo (o vacío) después de usar el add-in; no queda ningún `ARBA-comun-*.txt` en `%TEMP%`.
6. Revit en español: la partición se escribe (antes fallaba en cuatro add-ins).
7. `dotnet build` sin errores; tests de consola del add-in (si los tiene) en verde.

## 9. Orden recomendado de integración

1. **Exportacion-metrados-excel** — es quien crea los parámetros de metrado, trae el botón de migración y la tabla
   de misceláneos; con él integrado, cualquier add-in que escriba el contrato se ve en las tablas de inmediato.
2. **Acero-Zapatas** — el add-in más pequeño, con tests de consola: valida partición, origen, borrar/rearmar y
   migración con el mínimo riesgo.
3. **Fosa_transformadores** — el único con "borrar y rearmar" propio y el que escribe partida, peso y pernos: cierra
   el circuito completo con el plugin de metrados (tabla de misceláneos).
4. **Acero-cimientos-corridos** — comparte categoría con Zapatas y Bloques (CIMIENTOS): comprueba que los tres prefijos
   conviven en la misma tabla; aprovecha para corregir el namespace `FootingRebar` duplicado.
5. **Acero-vigas**, 6. **Acero-columnas**, 7. **Acero-losas** — mecánicos, mismo patrón.
8. **Acero-automatico** (muros de contención) — el más distinto (`PartitionName` propia, categoría muro o cimentación).

Entre cada paso: compilar, probar en Revit con la lista del apartado 8 y fusionar antes de pasar al siguiente, para
no tener dos add-ins a medias con la cinta compartida.

## 10. Mantenimiento: cambiar ARBA-comun sin reescribir los add-ins

El común entra como código fuente por submódulo, así que un cambio en ARBA-comun llega a un add-in con dos
comandos y una recompilación, sin tocar su código:

```powershell
git -C external/ARBA-comun fetch --tags origin
git -C external/ARBA-comun checkout v1.0.2
git add external/ARBA-comun && git commit -m "Submodulo ARBA-comun v1.0.2" && git push
```

Solo un cambio **MAJOR** del contrato (`CONTRATO.md` §4) obliga a editar código en los add-ins; MINOR y PATCH no.

Para hacerlo en los ocho a la vez: `tools/actualizar-arba-comun.ps1 -Version v1.0.2 -Repos <carpetas> [-Build]`
(salta los repos con cambios sin confirmar y resume el resultado en una tabla). Para no tener que acordarse,
`tools/dependabot-submodulo.yml` copiado como `.github/dependabot.yml` en cada add-in hace que GitHub abra un pull
request en ese repo cada vez que avance `main` de ARBA-comun.

Cuando los ocho estén integrados y probados, la opción más cómoda a largo plazo es un **monorepo** (un repo `ARBA`
con una carpeta por add-in, el común como carpeta normal y una sola solución): un commit cambia todo, una compilación
prueba todo y una sesión de Claude Code ve todo. Migrar es mecánico (`git subtree add` por repo conserva el
historial), pero conviene no hacerlo a mitad de la integración.
