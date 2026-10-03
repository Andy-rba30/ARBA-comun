# Prompt: integrar ARBA-comun en Acero-Zapatas

Estás en el repo **Acero-Zapatas** (rama `main`, commit a9e4039 o posterior).
Vas a integrar el código común **ARBA-comun** (https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`) siguiendo
`external/ARBA-comun/INTEGRACION.md` y `CONTRATO.md` (léelos tras añadir el submódulo). Trabaja en la rama
`claude/integrar-arba-comun`. No modifiques nada dentro de `external/ARBA-comun`; lo que falte va a `NOTAS-ARBA-COMUN.md`.

Tu add-in hoy: ensamblado `FootingRebar`, `net10.0-windows`, paquetes `Nice3point.Revit.Api.* 2027.2.*`, plantilla
`ZAP-{marca}` con comodín `{capa}` (inferior, inferior-sec, superior, superior-sec), partición escrita con
`NUMBER_PARTITION_PARAM` en `RebarGenerator.Finish`, sin marca propia en las barras (rearmar duplica), botón
`ARBA_Acero_Zapatas` con `IconZapatas` dentro de `ArbaRibbon`. En el contrato: prefijo **`ZAP`**, origen **`ZAPATAS`**,
categoría del anfitrión (zapatas = `CIMIENTOS`), partición por defecto `CIMIENTOS - ZAP-Z1` (sin código: el detalle de
capa va en `ARBA - Código`).

## Cambios, archivo por archivo

1. **Submódulo y csproj** (`FootingRebar.csproj`): `git submodule add … external/ARBA-comun`, checkout `v1.0.0`;
   `<RevitVersion>2027</RevitVersion>` y `<Import Project="external/ARBA-comun/Arba.Comun.props" />` tras el
   `PropertyGroup` (ya tienes `EnableWindowsTargeting`). Mantén `<Compile Remove="Tests\**" />`.
2. **`RibbonApp.cs`**: borra la clase `ArbaRibbon` entera (constantes, `Ensure`, `GetPanel`, `AddToPulldown`,
   `IconAcero`, `IconEncofrado`); mueve `IconZapatas` a la clase `RibbonApp` (privado). En `OnStartup`:
   `ArbaRibbon.Ensure(app)` y `ArbaRibbon.AddAcero(app, data)`; `using Arba.Comun;`. Nombre interno `ARBA_Acero_Zapatas`
   y texto "Zapatas" no cambian. Añade al `LongDescription` "Contrato ARBA " + `ArbaContract.Version`.
3. **Borrar** `RevitTheme.cs` y `PartitionName.cs` (quedan los comunes; `RevitTheme.Apply`, `RevitTheme.Error`… no cambian).
4. **`AppConfig.cs`** y **`config.json`**: `PartitionTemplate` por defecto `"{categoria} - {prefijo}-{marca}"`; el
   comentario de ayuda usa `PartitionName.Help` común. En `Normalize`, si la plantilla está vacía, pon la del contrato.
5. **`HostAnalysis.cs`**: `Partition(cfg, setName, layer)` →
   `ArbaPartition.BuildFor(Host, ArbaContract.Zapatas, cfg.PartitionTemplate, new PartitionName.Source { Mark = Mark,
   TypeName = TypeName, FamilyName = FamilyName, SetName = setName, Code = layer })` (la categoría y el Id los pone
   el común; `Layer` pasa a `Code`).
6. **`RebarGenerator.cs`**: `Finish(doc, rb, partition)` recibe además el anfitrión y la capa:
   `ArbaPartition.Write(rb, partition); ArbaOrigin.WriteFor(rb, host, ArbaContract.Zapatas, Layers.Short(layer));`.
   `MatchName` → `NameMatch.First` (mismo comportamiento) y, si quieres mejorar, `NameMatch.Unique`/`IsAmbiguous`
   marcando en amarillo el desplegable ambiguo como hace Bloques. Borra `MatchName` propio.
7. **`ArmarZapataCommand.cs`**: tras `tx.Start()`: `var avisos = new List<string>();
   ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen, ArbaContract.Codigo, ArbaContract.Elemento }, avisos);
   doc.Regenerate();` (los avisos al informe). Antes del bucle: para cada `item.Host`,
   `ArbaOrigin.Find(doc, ArbaContract.Zapatas, item.Host)`; si alguno tiene, un `TaskDialog` con dos `CommandLink`
   ("Borrar la armadura del add-in y rearmar" / "Conservar y armar encima") y un botón Cancelar, como en
   `Fosa_transformadores/ArmarBloqueCommand.cs` §3c; con "borrar", dentro de la `SubTransaction` del elemento y antes
   de `RebarGenerator.Build`: `ArbaOrigin.Delete(doc, ArbaContract.Zapatas, item.Host, out int barras)` (al informe).
   Barras antiguas (`ArbaMigration.HasLegacy(doc, item.Host, ArbaContract.Zapatas)`): tercer `CommandLink`
   "Migrar la armadura antigua al contrato (sin rearmar)" → `ArbaMigration.MigrateHost(doc, item.Host, ArbaContract.Zapatas)`
   y su `Resumen()` al informe.
8. **`RebarOptionsWindow.cs`**: en el grupo de recubrimientos/partición, si `!ArbaPartition.TemplateFollowsContract(cfg.PartitionTemplate)`
   muestra en `RevitTheme.Error` "La plantilla no empieza por {categoria} - {prefijo}-: incumple el contrato ARBA";
   en el pie, "Contrato ARBA " + `ArbaContract.Version`.
9. **`Tests/FootingRebar.Tests.csproj`**: sustituye `..\PartitionName.cs` por
   `..\external\ARBA-comun\src\ArbaContract.cs`, `ArbaPartition.cs`, `PartitionName.cs`, `NameMatch.cs`.
   **`Tests/Program.cs`** (`ConfigAndPartition`): `PartitionName.Source { Layer = … }` → `Code = …`; añade comprobaciones
   `AppConfig` por defecto cumple `ArbaPartition.TemplateFollowsContract`, y
   `ArbaPartition.Build("CIMIENTOS", "ZAP", "Z-01", "1") == "CIMIENTOS - ZAP-Z-01"`. `cd Tests && dotnet run` en verde.
10. **`README.md` / `INSTALADOR.md`**: clonado con `--recurse-submodules`, partición nueva, borrar y rearmar, migración.
11. `dotnet build -c Release` sin errores. Commit y push.

## Lista de verificación en Revit

1. Cinta: una sola pestaña ARBA, desplegable Acero con "Zapatas" (y los demás add-ins instalados).
2. Armar una zapata con marca `Z1`: 2 a 4 conjuntos con Partición `CIMIENTOS - ZAP-Z1`, `ARBA - Origen = ZAPATAS`,
   `ARBA - Código` = inferior / inferior-sec / superior / superior-sec, `Metrado - Elemento = CIMIENTOS`; en
   Gestionar > Parámetros de proyecto están `ARBA - Origen`, `ARBA - Código` y `Metrado - Elemento` (compartidos, ejemplar).
3. Armar la misma zapata otra vez: pregunta borrar/conservar; con "borrar" el número de conjuntos no se duplica.
4. Zapata sin marca: `CIMIENTOS - ZAP-<id>`.
5. Modelo con barras `ZAP-Z1` de la versión anterior: ofrece migrar; tras migrar, partición nueva y origen relleno
   sin barras nuevas; un segundo "armar" ya las reconoce como propias.
6. Revit en español: la partición se escribe.
7. Gestionar > Parámetros compartidos sigue apuntando a tu archivo; nada en `%TEMP%\ARBA-comun-*.txt`.
8. Con el plugin de metrados integrado: "Metrado acero - Cimentaciones" agrupa por `CIMIENTOS - ZAP-Z1`.

**Detente aquí**: resume los cambios archivo por archivo y espera a que yo pruebe la lista en Revit.
