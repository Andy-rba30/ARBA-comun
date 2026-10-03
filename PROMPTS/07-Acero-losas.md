# Prompt: integrar ARBA-comun en Acero-losas (SlabRebar)

Estás en el repo **Acero-losas** (rama `main`, commit f4eca62 o posterior; la rama por defecto de GitHub,
`claude/brave-allen-3gowqo`, es más antigua). Vas a integrar el código común **ARBA-comun**
(https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`) siguiendo `external/ARBA-comun/INTEGRACION.md` y
`CONTRATO.md`. Trabaja en `claude/integrar-arba-comun`. No modifiques nada dentro de `external/ARBA-comun`.

Tu add-in hoy: ensamblado y namespace `SlabRebar`, `net10.0-windows`, `Nice3point… 2027.2.*`, `EnableWindowsTargeting`;
plantilla `LOSA-{marca}` con `{capa}` (inferior, baston, temperatura, inferior-sec, superior, superior-sec); partición
con `NUMBER_PARTITION_PARAM` (correcto); sin marca propia; botón `ARBA_Acero_Losas` con `IconLosas` dentro de
`ArbaRibbon`; tests de consola en `Tests/`. En el contrato: prefijo **`LOS`** (antes `LOSA`), origen **`LOSAS`**,
partición por defecto `LOSAS - LOS-L2` (sin código; la capa va en `ARBA - Código`).

## Cambios, archivo por archivo

1. **Submódulo y csproj** (`SlabRebar.csproj`): submódulo (checkout `v1.0.0`); `<RevitVersion>2027</RevitVersion>` +
   `<Import Project="external/ARBA-comun/Arba.Comun.props" />`. Mantén `<Compile Remove="Tests\**" />`.
2. **`RibbonApp.cs`**: borra la clase `ArbaRibbon`; `IconLosas` pasa a `RibbonApp`; `ArbaRibbon.Ensure(app)` +
   `ArbaRibbon.AddAcero(app, data)`; `using Arba.Comun;`. Nombre interno `ARBA_Acero_Losas` se conserva.
3. **Borrar** `RevitTheme.cs` y `PartitionName.cs`.
4. **`AppConfig.cs` / `config.json`**: `PartitionTemplate` por defecto `"{categoria} - {prefijo}-{marca}"`.
5. **`HostAnalysis.cs`**: `Partition(cfg, setName, layer)` → `ArbaPartition.BuildFor(Host, ArbaContract.Losas,
   cfg.PartitionTemplate, new PartitionName.Source { Mark = Mark, TypeName = TypeName, FamilyName = FamilyName,
   SetName = setName, Code = layer })`.
6. **`RebarGenerator.cs`**: `Finish` → `ArbaPartition.Write(r, partition)` + `ArbaOrigin.WriteFor(r, host,
   ArbaContract.Losas, Layers.Short(b.Layer))`; `MatchName` → `NameMatch.First` (o `Unique` con aviso; conserva el
   aviso especial de ganchos de estilo estribo en `FindHookType`).
7. **`ArmarLosaCommand.cs`**: tras `tx.Start()`: `ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen,
   ArbaContract.Codigo, ArbaContract.Elemento }, avisos); doc.Regenerate();`. Por anfitrión: `ArbaOrigin.Find(doc,
   ArbaContract.Losas, host)` → borrar y rearmar / conservar; `ArbaOrigin.Delete` en la `SubTransaction`;
   `ArbaMigration.HasLegacy` → "Migrar sin rearmar".
8. **`RebarOptionsWindow.cs`**: aviso de plantilla; versión del contrato; `PartitionName.Help` común.
9. **`Tests/SlabRebar.Tests.csproj`**: `..\PartitionName.cs` → `..\external\ARBA-comun\src\ArbaContract.cs`,
   `ArbaPartition.cs`, `PartitionName.cs`, `NameMatch.cs`. **`Tests/Program.cs`** (`Config y particion`):
   `Source { Layer = … }` → `Code = …`; si alguna prueba comprueba el valor por defecto `LOSA-{marca}`, cámbiala a la
   plantilla del contrato y añade `ArbaPartition.Build("LOSAS", "LOS", "L-2", "1") == "LOSAS - LOS-L-2"`. `cd Tests &&
   dotnet run` en verde (hoy 98 comprobaciones).
10. **`README.md` / `INSTALADOR.md` / `PLAN.md`**. `dotnet build -c Release` sin errores. Commit y push.

## Lista de verificación en Revit

1. Cinta: una pestaña ARBA, desplegable Acero con "Losas".
2. Armar una losa aligerada marca `L2`: Partición `LOSAS - LOS-L2` en todos los conjuntos, `ARBA - Origen = LOSAS`,
   `ARBA - Código` = inferior / baston / temperatura, `Metrado - Elemento = LOSAS`. Maciza: inferior, inferior-sec,
   superior, superior-sec.
3. Rearmar: pregunta borrar/conservar; sin duplicados con "borrar".
4. Modelo con barras `LOSA-L1`: ofrece migrar; tras migrar `LOSAS - LOS-L1` con origen.
5. Con el plugin de metrados: "Metrado acero - Losas" agrupa por `LOSAS - LOS-…`.

**Detente aquí**: resume los cambios archivo por archivo y espera a que yo pruebe la lista en Revit.
