# Prompt: integrar ARBA-comun en Acero-cimientos-corridos (StripFootingRebar)

Estás en el repo **Acero-cimientos-corridos** (rama `main`, commit 11052c0 o posterior; `PENDIENTE.md` dice que `main` no se ha
compilado ni probado: compílala primero y, si falla, arregla la compilación antes de seguir). Vas a integrar el código común **ARBA-comun**
(https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`) siguiendo `external/ARBA-comun/INTEGRACION.md` y
`CONTRATO.md`. Trabaja en `claude/integrar-arba-comun`. No modifiques nada dentro de `external/ARBA-comun`.

Tu add-in hoy: ensamblado `StripFootingRebar` pero **namespace `FootingRebar`** (el mismo que Acero-Zapatas) y
manifiesto `FootingRebar.RibbonApp` / `FootingRebar.ArmarCimientoCommand`; `net10.0-windows`, `Nice3point… 2027.*`;
plantilla `CC-{marca}` con `{cara}` (superior / inferior / estribo); **partición escrita con
`LookupParameter("Partition")`** (en Revit en español no se escribe nada); sin marca propia; botón
`ARBA_Acero_Cimientos` con el icono `IconVigas` dentro de `ArbaRibbon`; anfitriones: cimentaciones, vigas de
cimentación (armazón), suelos y muros. En el contrato: prefijo **`CCO`** (antes `CC`), origen **`CIMIENTOS CORRIDOS`**,
categoría la del anfitrión real: cimentación → `CIMIENTOS - CCO-C1`, muro (sobrecimiento) → `MUROS - CCO-C1`, suelo →
`LOSAS - CCO-…`, viga → `VIGAS - CCO-…` (el prefijo dice quién armó; la categoría, dónde, igual que agrupa el metrado).

## Cambios, archivo por archivo

1. **Namespace**: renombra `namespace FootingRebar` → `namespace StripFootingRebar` en todos los `.cs` y actualiza
   `StripFootingRebar.addin` (`FullClassName` de los dos `AddIn`). Quita así la colisión con Acero-Zapatas.
2. **Submódulo y csproj** (`StripFootingRebar.csproj`): submódulo en `external/ARBA-comun` (checkout `v1.0.0`);
   `<RevitVersion>2027</RevitVersion>`, `<EnableWindowsTargeting>true</EnableWindowsTargeting>` y
   `<Import Project="external/ARBA-comun/Arba.Comun.props" />`; condiciona `CopyToRevit` a `'$(OS)'=='Windows_NT'`.
3. **`RibbonApp.cs`**: borra la clase `ArbaRibbon`; `IconVigas` pasa a `RibbonApp` (y, si quieres, dibuja un icono
   propio de cimiento corrido: sección con dos capas y estribo sobre terreno); `ArbaRibbon.Ensure(app)` +
   `ArbaRibbon.AddAcero(app, data)`; `using Arba.Comun;`. Nombre interno `ARBA_Acero_Cimientos` se conserva.
4. **Borrar** `RevitTheme.cs` y `PartitionName.cs`.
5. **`AppConfig.cs` / `config.json`**: `PartitionTemplate` por defecto `"{categoria} - {prefijo}-{marca}"`.
6. **`HostAnalysis.cs`**: `Partition(cfg, setName, face)` → `ArbaPartition.BuildFor(Host, ArbaContract.CimientosCorridos,
   cfg.PartitionTemplate, new PartitionName.Source { Mark = Mark, TypeName = TypeName, FamilyName = FamilyName,
   SetName = setName, Code = face })`. Un recorrido con varios tramos (`AnalyzeAll`) usa el anfitrión de cada tramo.
7. **`RebarGenerator.cs`**: `Finish` → `ArbaPartition.Write(r, partition)` (sustituye `LookupParameter("Partition")`) +
   `ArbaOrigin.WriteFor(r, host, ArbaContract.CimientosCorridos, face)`; `MatchName` → `NameMatch.First` (o `Unique`
   con aviso de ambigüedad). Borra `MatchName` propio.
8. **`ArmarCimientoCommand.cs`**: tras `tx.Start()`: `ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen,
   ArbaContract.Codigo, ArbaContract.Elemento }, avisos); doc.Regenerate();`. Antes del bucle, por anfitrión:
   `ArbaOrigin.Find(doc, ArbaContract.CimientosCorridos, host)` → pregunta borrar y rearmar / conservar (TaskDialog con
   CommandLink); con "borrar", `ArbaOrigin.Delete(doc, ArbaContract.CimientosCorridos, host, out barras)` dentro de la
   `SubTransaction` antes de `Build`. `ArbaMigration.HasLegacy(doc, host, ArbaContract.CimientosCorridos)` → opción
   "Migrar sin rearmar" → `MigrateHost`.
9. **`RebarOptionsWindow.cs`**: aviso si la plantilla no cumple `ArbaPartition.TemplateFollowsContract`; versión del
   contrato en el pie; `PartitionName.Help` común.
10. **`README.md`** y **`PENDIENTE.md`**: submódulo, namespace nuevo, partición nueva (ejemplos por tipo de anfitrión),
    borrar/rearmar, migración. `dotnet build -c Release` sin errores. Commit y push.

## Lista de verificación en Revit

1. Revit carga el add-in con el namespace nuevo (manifiesto correcto); cinta: una pestaña ARBA, desplegable Acero con
   "Cimientos/Sobrecimientos" junto a "Zapatas".
2. Armar un cimiento corrido (cimentación) marca `C1`: Partición `CIMIENTOS - CCO-C1`, `ARBA - Origen = CIMIENTOS CORRIDOS`,
   `ARBA - Código` = superior / inferior / estribo, `Metrado - Elemento = CIMIENTOS`.
3. Armar un sobrecimiento modelado como muro: `MUROS - CCO-<marca>`, `Metrado - Elemento = MUROS`.
4. Revit en español: la partición se escribe (antes no).
5. Rearmar: pregunta borrar/conservar; sin duplicados con "borrar".
6. Modelo con barras `CC-C1`: ofrece migrar; tras migrar `CIMIENTOS - CCO-C1` (o `MUROS - …`) y origen relleno.
7. Con el plugin de metrados: en "Metrado acero - Cimentaciones" conviven `CIMIENTOS - ZAP-…`, `CIMIENTOS - CCO-…` y
   `CIMIENTOS - BLQ-…`.

**Detente aquí**: resume los cambios archivo por archivo y espera a que yo pruebe la lista en Revit.
