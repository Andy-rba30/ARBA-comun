# Prompt: integrar ARBA-comun en Acero-vigas (BeamRebar)

Estás en el repo **Acero-vigas** (rama `claude/intelligent-lovelace-nlqbfb`, commit 4d66a34 o posterior; no hay `main`
y la rama por defecto de GitHub, `claude/affectionate-allen-1kin91`, es más antigua). Vas a integrar el código común
**ARBA-comun** (https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`) siguiendo `external/ARBA-comun/INTEGRACION.md`
y `CONTRATO.md`. Trabaja en `claude/integrar-arba-comun`. No modifiques nada dentro de `external/ARBA-comun`.

Tu add-in hoy: ensamblado y namespace `BeamRebar`, `net10.0-windows`, `Nice3point… 2027.*`; plantilla `VIG-{marca}`
con `{cara}` (superior / inferior / estribo); **partición escrita con `LookupParameter("Partition")`** (en Revit en
español no se escribe); sin marca propia; botón `ARBA_Acero_Vigas` con `IconVigas` dentro de `ArbaRibbon`; anfitrión
armazón estructural. En el contrato: prefijo **`VIG`**, origen **`VIGAS`**, partición por defecto `VIGAS - VIG-V-101`
(sin código; la cara va en `ARBA - Código`).

## Cambios, archivo por archivo

1. **Submódulo y csproj** (`BeamRebar.csproj`): submódulo en `external/ARBA-comun` (checkout `v1.0.0`);
   `<RevitVersion>2027</RevitVersion>`, `<EnableWindowsTargeting>true</EnableWindowsTargeting>`,
   `<Import Project="external/ARBA-comun/Arba.Comun.props" />`; `CopyToRevit` solo si `'$(OS)'=='Windows_NT'`.
2. **`RibbonApp.cs`**: borra la clase `ArbaRibbon`; `IconVigas` pasa a `RibbonApp`; `ArbaRibbon.Ensure(app)` +
   `ArbaRibbon.AddAcero(app, data)`; `using Arba.Comun;`. Nombre interno `ARBA_Acero_Vigas` se conserva.
3. **Borrar** `RevitTheme.cs` y `PartitionName.cs`.
4. **`AppConfig.cs` / `config.json`**: `PartitionTemplate` por defecto `"{categoria} - {prefijo}-{marca}"`.
5. **`HostAnalysis.cs`**: `Partition(cfg, setName, face)` → `ArbaPartition.BuildFor(Host, ArbaContract.Vigas,
   cfg.PartitionTemplate, new PartitionName.Source { Mark = Mark, TypeName = TypeName, FamilyName = FamilyName,
   SetName = setName, Code = face })`.
6. **`RebarGenerator.cs`**: `Finish` → `ArbaPartition.Write(r, partition)` + `ArbaOrigin.WriteFor(r, host,
   ArbaContract.Vigas, face)`; `MatchName` → `NameMatch.First` (o `Unique` con aviso). Los bastones y empalmes pasan
   por el mismo `Finish`: comprueba que todos los conjuntos (corridas, bastones, laterales, estribos, trozos
   empalmados) reciben origen.
7. **`ArmarVigaCommand.cs`**: tras `tx.Start()`: `ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen,
   ArbaContract.Codigo, ArbaContract.Elemento }, avisos); doc.Regenerate();`. Por anfitrión: `ArbaOrigin.Find(doc,
   ArbaContract.Vigas, host)` → borrar y rearmar / conservar; `ArbaOrigin.Delete(...)` dentro de la `SubTransaction`;
   `ArbaMigration.HasLegacy` → "Migrar sin rearmar" (`MigrateHost`).
8. **`RebarOptionsWindow.cs`**: aviso si la plantilla no cumple el contrato; versión del contrato en el pie;
   `PartitionName.Help` común.
9. **`README.md`**: submódulo, partición nueva, borrar/rearmar, migración. `dotnet build -c Release` sin errores.
   Commit y push.

## Lista de verificación en Revit

1. Cinta: una pestaña ARBA, desplegable Acero con "Vigas".
2. Armar una viga marca `V-101`: todos los conjuntos con Partición `VIGAS - VIG-V-101`, `ARBA - Origen = VIGAS`,
   `ARBA - Código` = superior / inferior / estribo, `Metrado - Elemento = VIGAS`.
3. Revit en español: la partición se escribe (antes no).
4. Rearmar: pregunta borrar/conservar; sin duplicados con "borrar".
5. Modelo con barras `VIG-V1`: ofrece migrar; tras migrar `VIGAS - VIG-V1` con origen, sin barras nuevas.
6. Con el plugin de metrados: "Metrado acero - Vigas" agrupa por `VIGAS - VIG-…`.

**Detente aquí**: resume los cambios archivo por archivo y espera a que yo pruebe la lista en Revit.
