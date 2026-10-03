# Prompt: integrar ARBA-comun en Exportacion-metrados-excel

Estás en el repo **Exportacion-metrados-excel** (rama `main`, commit 477acaf o posterior). Vas a integrar el
código común **ARBA-comun** (https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`) y a adaptar el plugin al
contrato. Trabaja en la rama `claude/integrar-arba-comun`. No modifiques nada dentro de `external/ARBA-comun`: si el
contrato no cubre algo, escríbelo en `NOTAS-ARBA-COMUN.md` y sigue.

Lee primero `external/ARBA-comun/CONTRATO.md`, `INTEGRACION.md` y `README.md` (tras añadir el submódulo). Resumen del
contrato que te afecta: 8 parámetros compartidos con GUID fijo (`ARBA - Origen`, `ARBA - Código`, `ARBA - Anfitrión`,
`Metrado - Partida`, `Metrado - Material`, `Metrado - Peso (kg)`, `Metrado - Pernos (und)`, `Metrado - Elemento`; los
tres que ya creas conservan tu GUID), partición del acero `CATEGORIA - PREFIJO-marca[-codigo]` (CATEGORIA = tu grupo
VIGAS/COLUMNAS/CIMIENTOS/LOSAS/MUROS deducido del anfitrión; prefijos ZAP, CCO, BLQ, VIG, COL, LOS, MCO y `MAN` para
el acero no creado por ARBA), cinta: pestaña `ARBA`, panel `Metrados` con tus botones, y el grupo `MISCELANEOS` de
`Metrado - Elemento` para los elementos con `Metrado - Partida`.

## 1. Submódulo y proyecto

- `git submodule add https://github.com/Andy-rba30/ARBA-comun external/ARBA-comun && git -C external/ARBA-comun checkout v1.0.0`.
- `src/ExportacionMetrados/ExportacionMetrados.csproj`: tras el `PropertyGroup` que fija `RevitVersion`/`TargetFramework`
  añade `<Import Project="../../external/ARBA-comun/Arba.Comun.props" />`. Tu csproj ya define `REVIT$(RevitVersion)`
  y `EnableWindowsTargeting`; el `.props` los repite sin conflicto. Comprueba `dotnet build -c Release -p:RevitVersion=2027`
  y, si tienes la API, `-p:RevitVersion=2024` (net48) y `2026` (net8).
- Documenta en `README.md` el clonado con `--recurse-submodules`.

## 2. Cinta (`src/ExportacionMetrados/App.cs`)

Hoy: pestaña `Metrados`, panel `Exportar`, botones `ExportarMetradosExcel`, `MetradoAutomatico`, `AsignarParticion`.
Nuevo: `ArbaRibbon.Ensure(application)` y `ArbaRibbon.AddMetrados(application, data)` para cada botón, con los nombres
internos del contrato: `ARBA_Metrados_Exportar` ("Exportar a\nExcel"), `ARBA_Metrados_Automatico` ("Metrado\nautomático"),
`ARBA_Metrados_Particion` ("Asignar\npartición") y el nuevo `ARBA_Metrados_Migrar` ("Migrar\nparticiones y origen",
icono `ArbaRibbon.IconMetrados(32/16)` o un PNG nuevo). Borra `NombrePestana`/`NombrePanel` y la creación manual de la
pestaña. Nueva clase `MigrarParticionesCommand : Arba.Comun.ArbaMigrateCommandBase` con `[Transaction(TransactionMode.Manual)]`
y `[Regeneration(RegenerationOption.Manual)]` (los atributos van en tu clase). El tooltip del botón de migrar explica
lo que hace (ver `INTEGRACION.md` §7).

## 3. Parámetros (`Core/Metrado/ClasificadorElementos.cs`)

- Borra `GuidParametroMaterial/Peso/ElementoRefuerzo`, `AsegurarParametro`, `BuscarDefinicionVinculada` y
  `ObtenerDefinicionCompartida` (y el archivo permanente `%LocalAppData%\ExportacionMetrados\ParametrosMetrado.txt`:
  ya no se usa). `AsegurarParametroMaterial/Elemento/Peso` pasan a llamar
  `ArbaSharedParams.Ensure(doc, ArbaContract.Material / Elemento / Peso, advertencias)`; añade `AsegurarParametrosContrato`
  que asegura también `Partida`, `Pernos`, `Origen`, `Codigo` y `Anfitrion` (`ArbaSharedParams.EnsureAll`). Las
  categorías ya no las decide el plugin: las trae el contrato (incluyen las tuyas).
- Mantén las constantes `NombreParametroMaterial`, `NombreParametroPeso`, `NombreParametroElementoRefuerzo` pero como
  alias de `ArbaContract.Material.Name` etc., y `ValorConcreto`… como alias de `ArbaContract.MaterialConcreto`….
- `IdParametroMaterial/IdParametroElementoRefuerzo` → `ArbaSharedParams.IdOf(doc, ArbaContract.X)`.
- `EscribirPesos`: salta (y cuenta como "respetados") los elementos con `ArbaMetrado.PesoProtegido(e)` (origen ARBA y
  peso > 0 escrito por su add-in: rejillas, ángulos).
- `RellenarMaterial`: no sobrescribas `Metrado - Material` en elementos con `ArbaOrigin.IsArba(e)` (ya lo escribió su
  add-in).
- Migración de parámetros homónimos manuales: la hace `ArbaSharedParams.Ensure` (copia valores, quita el vínculo
  antiguo, avisa); recoge sus avisos en el resumen.

## 4. Grupo MISCELANEOS

- `ModelosMetrado.cs`: nuevo `TipoGrupo.Miscelaneos` y `CategoriaMetrado` "Misceláneos" (`NombreParticion` =
  `ArbaContract.ElementoMiscelaneos`, categorías = las de `ArbaContract.Partida.Categories`, `PesoPorVolumen = true`,
  `PuedeSerMetalica = true`, `TablaMulticategoria = true`, no aloja refuerzo), seleccionado por defecto.
- `ClasificadorElementos.GrupoDe`: si `ArbaMetrado.EsMiscelaneo(e)` (tiene `Metrado - Partida`) → el grupo Misceláneos,
  antes que la regla de conexiones. `RellenarElementoEnElementos` escribe entonces `MISCELANEOS` en `Metrado - Elemento`,
  y las tablas de Vigas / Conexiones / Otros, que filtran por ese parámetro, dejan de contarlos.
- `CalculadorMetrado.MedirPerfilMetalico`: para misceláneos, peso = `Metrado - Peso (kg)` si `PesoProtegido`, si no
  volumen × densidad; guarda además partida (`ArbaSharedParams.GetText(e, ArbaContract.Partida)`), pernos
  (`GetInteger(e, ArbaContract.Pernos)`) y `ARBA - Código` en `ElementoAceroEstructural` (campos nuevos `Partida`,
  `Pernos`, `Codigo`).
- `GeneradorTablasRevit`: nueva tabla **"Metrado acero estructural - Misceláneos"** (multicategoría,
  `ViewSchedule.CreateSchedule(doc, ElementId.InvalidElementId)`), campos: `Metrado - Partida` (agrupación con
  cabecera y pie), Categoría, Elemento (familia y tipo), `ARBA - Código`, Cantidad, `Metrado - Peso (kg)` (totales),
  `Metrado - Pernos (und)` (totales); filtro `Metrado - Elemento = MISCELANEOS`. Añádela a `MotivoTabla…Desactualizada`
  para regenerarla si falta el filtro. Añade también la columna `ARBA - Código` (oculta o visible, tu criterio) a
  "Metrado acero - General".
- `GeneradorFiltrosVista`: filtro "Metrado - Acero estructural - Misceláneos" (`Metrado - Elemento = MISCELANEOS`),
  color propio.
- `ExportadorMetrado`: en la hoja Resumen un bloque "Misceláneos" por partida (kg, pernos, n.º de piezas) y columnas
  Partida/Código/Pernos en la hoja de detalle de acero estructural.

## 5. Partición del acero según el contrato

- `ClasificadorElementos.AsignarParticion` (y el relleno automático de `MetradoAutomaticoCommand` 1b): para cada
  armadura, `var info = ArbaPartition.ParseOf(r)`. Si `info.IsArba` o `ArbaOrigin.IsArba(r)` → **no se toca** (ni con
  "Sobrescribir"); cuenta como "respetadas (ARBA)". Si no: con texto fijo, se escribe tal cual; sin texto fijo, se
  escribe `ArbaPartition.BuildFor(host, ArbaContract.Manual)` (p. ej. `VIGAS - MAN-V1`) y
  `ArbaOrigin.WriteFor(r, host, ArbaContract.Manual, "")`; sin anfitrión, `OTROS - MAN-<id>`. Respeta "Sobrescribir"
  solo para particiones no ARBA. Actualiza el texto del botón/ventana y el README (hoy dice que escribe `VIGAS`).
- `GeneradorTablasRevit.CrearTablaRefuerzo`: el filtro principal sigue siendo `Metrado - Elemento = GRUPO`; el
  respaldo por partición pasa de `Equal GRUPO` a `BeginsWith ArbaPartition.FilterPrefix(GRUPO)` (`"VIGAS - "`), y el
  tercer respaldo (categoría del anfitrión) se mantiene. Lo mismo en `GeneradorFiltrosVista` cuando falte
  `Metrado - Elemento`: regla `ArbaPartition.CategoryRule(GRUPO)`.

## 6. Resumen y documentación

- En el resumen final del metrado añade la versión del contrato (`ArbaContract.Version`) y los contadores nuevos
  (pesos respetados, particiones ARBA respetadas, misceláneos).
- Actualiza `README.md`: cinta ARBA, parámetros del contrato (enlaza `external/ARBA-comun/CONTRATO.md`), tabla de
  misceláneos, partición `MAN`, botón Migrar.
- `dotnet build -c Release -p:RevitVersion=2027` sin errores (y, si puedes, 2024 y 2026). Commit y push de la rama.

## Lista de verificación en Revit (haz todo esto antes de fusionar)

1. Cinta: pestaña **ARBA** → panel **Metrados** con cuatro botones; ya no existe la pestaña "Metrados".
2. **Metrado automático** en un modelo nuevo: en Gestionar > Parámetros de proyecto aparecen los 8 parámetros del
   contrato como compartidos de ejemplar (grupo Datos); Gestionar > Parámetros compartidos sigue con tu archivo (o
   vacío) y no queda `ARBA-comun-*.txt` en `%TEMP%`.
3. En un modelo ya metrado con la versión anterior: los valores de `Metrado - Material` y `Metrado - Peso (kg)` se
   conservan y no aparecen parámetros duplicados.
4. En un modelo donde creaste a mano un parámetro de proyecto "Metrado - Material" (no compartido): tras el metrado
   hay uno solo, compartido, con los valores de antes, y el resumen lo avisa.
5. Armaduras sin partición: quedan `VIGAS - MAN-V1` (o la categoría que toque) con `ARBA - Origen = MANUAL`; las
   que tenían `ZAP-…` o `CIMIENTOS - ZAP-…` no cambian.
6. **Migrar particiones y origen** en un modelo con `ZAP-Z1`, `CC-C1`, `BLQ-FT-01-F1`: particiones nuevas con la
   categoría del anfitrión, `ARBA - Origen`/`Código`/`Metrado - Elemento` rellenos, mismo número de conjuntos; Ctrl+Z
   lo deshace.
7. Las tablas "Metrado acero - Vigas/Columnas/Cimentaciones/Losas" agrupan por partición y muestran `CIMIENTOS - ZAP-Z1`
   etc.; "Metrado acero - General" muestra la columna `ARBA - Código`.
8. Con un modelo del add-in de bloques integrado (prompt 03): tabla "Metrado acero estructural - Misceláneos" con
   las partidas REJILLAS y ÁNGULOS, kg y pernos sumados; los ángulos **no** aparecen en "Metrado acero estructural -
   Vigas" ni las rejillas en "Otros"; el peso de ángulos y rejillas no cambia al repetir el metrado.
9. Excel: bloque Misceláneos en Resumen.

**Detente aquí** (no fusiones): dime qué has cambiado archivo por archivo y espera a que yo pruebe la lista en Revit.
