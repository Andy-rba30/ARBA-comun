# Prompt: integrar ARBA-comun en Acero-columnas (ColumnRebar)

Estás en el repo **Acero-columnas** (rama `main`, commit 6bed5b0 o posterior). Vas a integrar el código común
**ARBA-comun** (https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`) siguiendo `external/ARBA-comun/INTEGRACION.md`
y `CONTRATO.md`. Trabaja en `claude/integrar-arba-comun`. No modifiques nada dentro de `external/ARBA-comun`.

Tu add-in hoy: ensamblado y namespace `ColumnRebar`, `net10.0-windows`, `Nice3point… 2027.*`; plantilla `COL-{marca}`
con `{estribo}` (número de estribo); **partición escrita con `LookupParameter("Partition")`** (en Revit en español no
se escribe); sin marca propia; botón `ARBA_Acero_Columnas` con el icono genérico `IconAcero` (el mismo del desplegable);
anfitrión pilar estructural. En el contrato: prefijo **`COL`**, origen **`COLUMNAS`**, partición por defecto
`COLUMNAS - COL-C3` (sin código).

## Cambios, archivo por archivo

1. **Submódulo y csproj** (`ColumnRebar.csproj`): submódulo (checkout `v1.0.0`); `<RevitVersion>2027</RevitVersion>`,
   `<EnableWindowsTargeting>true</EnableWindowsTargeting>`, `<Import Project="external/ARBA-comun/Arba.Comun.props" />`;
   `CopyToRevit` solo en Windows.
2. **`RibbonApp.cs`**: borra la clase `ArbaRibbon`; `ArbaRibbon.Ensure(app)` + `ArbaRibbon.AddAcero(app, data)`;
   `using Arba.Comun;`. Dibuja un icono propio `IconColumnas` en `RibbonApp` (sección rectangular con estribo y barras
   de esquina, como hacen Vigas y Zapatas) en lugar de reutilizar `IconAcero`. Nombre interno `ARBA_Acero_Columnas`.
3. **Borrar** `RevitTheme.cs` y `PartitionName.cs`.
4. **`AppConfig.cs` / `config.json`**: `PartitionTemplate` por defecto `"{categoria} - {prefijo}-{marca}"`.
5. **`HostAnalysis.cs`**: `Partition(cfg, setName, stirrup)` → `ArbaPartition.BuildFor(Host, ArbaContract.Columnas,
   cfg.PartitionTemplate, new PartitionName.Source { Mark = Mark, TypeName = TypeName, FamilyName = FamilyName,
   SetName = setName, Code = stirrup })`.
6. **`RebarGenerator.cs`**: `Finish` → `ArbaPartition.Write(r, partition)` + `ArbaOrigin.WriteFor(r, host,
   ArbaContract.Columnas, code)` con `code` = `"longitudinal"` para las barras verticales, `"estribo " + n` para los
   estribos y `"grapa"` para las grapas (lo que hoy distingues por `SetName`/`stirrup`). `MatchName` → `NameMatch.First`
   (o `Unique` con aviso).
7. **`ArmarColumnaCommand.cs`**: tras `tx.Start()`: `ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen,
   ArbaContract.Codigo, ArbaContract.Elemento }, avisos); doc.Regenerate();`. Por anfitrión: `ArbaOrigin.Find(doc,
   ArbaContract.Columnas, host)` → borrar y rearmar / conservar; `ArbaOrigin.Delete` en la `SubTransaction`;
   `ArbaMigration.HasLegacy` → "Migrar sin rearmar".
8. **`RebarOptionsWindow.cs`**: aviso de plantilla; versión del contrato; `PartitionName.Help` común.
9. **`README.md`**. `dotnet build -c Release` sin errores. Commit y push.

## Lista de verificación en Revit

1. Cinta: una pestaña ARBA, desplegable Acero con "Columnas" y su icono propio.
2. Armar una columna marca `C3`: Partición `COLUMNAS - COL-C3`, `ARBA - Origen = COLUMNAS`, `ARBA - Código` =
   longitudinal / estribo N / grapa, `Metrado - Elemento = COLUMNAS`.
3. Revit en español: la partición se escribe (antes no).
4. Rearmar: pregunta borrar/conservar; sin duplicados con "borrar".
5. Modelo con barras `COL-C1`: ofrece migrar; tras migrar `COLUMNAS - COL-C1` con origen.
6. Con el plugin de metrados: "Metrado acero - Columnas" agrupa por `COLUMNAS - COL-…`.

**Detente aquí**: resume los cambios archivo por archivo y espera a que yo pruebe la lista en Revit.
