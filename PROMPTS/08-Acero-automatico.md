# Prompt: integrar ARBA-comun en Acero-automatico (RetainingWallRebar, muros de contención)

Estás en el repo **Acero-automatico** (rama `main`, commit 84611a7 o posterior: ya incluye el tema oscuro fusionado desde `claude/sweet-hypatia-t3n7nf`). Es el add-in de **muros de
contención** (`RetainingWallRebar`). Vas a integrar el código común **ARBA-comun**
(https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`) siguiendo `external/ARBA-comun/INTEGRACION.md` y
`CONTRATO.md`. Trabaja en `claude/integrar-arba-comun`. No modifiques nada dentro de `external/ARBA-comun`.

Tu add-in hoy: ensamblado y namespace `RetainingWallRebar`, `net10.0-windows`, `Nice3point… 2027.*`; plantilla
`MC-{marca}` con `{ala}` y `{conjunto}`, y un `PartitionName` propio (regla de limpieza distinta y respaldo `MURO <id>`);
**partición escrita con `LookupParameter("Partition")`** (en Revit en español no se escribe); sin marca propia;
botón `ARBA_Acero_Muro` con icono propio en `RibbonApp`; `RevitTheme.cs` propio (idéntico al común) aplicado en `RebarOptionsWindow`; anfitriones: muros
(`OST_Walls`) y cimentaciones (`OST_StructuralFoundation`). En el contrato: prefijo **`MCO`** (antes `MC`), origen
**`MUROS DE CONTENCION`**, categoría la del anfitrión: muro → `MUROS - MCO-M1`, cimentación → `CIMIENTOS - MCO-M1`
(el prefijo `MUR` queda reservado para un futuro add-in de placas).

## Cambios, archivo por archivo

1. **Submódulo y csproj** (`RetainingWallRebar.csproj`): submódulo (checkout `v1.0.0`); `<RevitVersion>2027</RevitVersion>`,
   `<EnableWindowsTargeting>true</EnableWindowsTargeting>`, `<Import Project="external/ARBA-comun/Arba.Comun.props" />`;
   `CopyToRevit` solo en Windows.
2. **`RibbonApp.cs`**: borra la clase `ArbaRibbon` (tu `Icon` privado se queda); `ArbaRibbon.Ensure(app)` +
   `ArbaRibbon.AddAcero(app, data)`; `using Arba.Comun;`. Nombre interno `ARBA_Acero_Muro` se conserva.
3. **Borrar** `PartitionName.cs` y `RevitTheme.cs` (quedan los comunes; `RevitTheme.Apply`, `Ok`, `Error`, `Muted` y `Selection` siguen igual). El común no tiene el respaldo `MURO <id>`: si lo quieres, en `HostAnalysis.Partition`
   devuelve `"MURO " + Host.Id` cuando el resultado quede vacío (con la plantilla del contrato nunca queda vacío,
   porque `{marca}` cae al Id).
4. **`AppConfig.cs` / `config.json`**: `PartitionTemplate` por defecto `"{categoria} - {prefijo}-{marca}"` (hoy
   `MC-{marca}`); los comodines `{ala}` y `{conjunto}` siguen funcionando (`{ala}` es alias de `{codigo}`).
5. **`HostAnalysis.cs`**: `Partition(cfg, wing, setName)` → `ArbaPartition.BuildFor(Host, ArbaContract.MurosContencion,
   cfg.PartitionTemplate, new PartitionName.Source { Mark = Mark, TypeName = TypeName, FamilyName = FamilyName,
   SetName = setName, Code = wing })`. En los esquineros en L cada ala es un anfitrión distinto: usa el de cada barra.
6. **`RebarGenerator.cs`**: `Finish` → `ArbaPartition.Write(r, partition)` + `ArbaOrigin.WriteFor(r, host,
   ArbaContract.MurosContencion, code)` con `code` = nombre del conjunto (`s.Label` / `SetName`: vertical trasdós,
   horizontal, parrilla…) y el ala si la hay. `MatchName` → `NameMatch.First` (o `Unique` con aviso).
7. **`ArmarMuroCommand.cs`**: tras `tx.Start()`: `ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen,
   ArbaContract.Codigo, ArbaContract.Elemento }, avisos); doc.Regenerate();`. Por anfitrión: `ArbaOrigin.Find(doc,
   ArbaContract.MurosContencion, host)` → borrar y rearmar / conservar; `ArbaOrigin.Delete` en la `SubTransaction`
   antes de `Build`/`BuildCorner`; `ArbaMigration.HasLegacy` → "Migrar sin rearmar".
8. **`RebarOptionsWindow.cs`**: el tema oscuro ya está aplicado; solo añade el aviso si la plantilla no cumple el
   contrato, la versión del contrato en el pie y `PartitionName.Help` común.
9. **`README.md`** (sección `partitionTemplate`, línea ~508): plantilla nueva, borrar/rearmar, migración.
   `dotnet build -c Release` sin errores. Commit y push.

## Lista de verificación en Revit

1. Cinta: una pestaña ARBA, desplegable Acero con "Muro de contencion".
2. Armar un muro de contención (categoría muro) marca `M1`: Partición `MUROS - MCO-M1`, `ARBA - Origen = MUROS DE
   CONTENCION`, `ARBA - Código` = conjunto/ala, `Metrado - Elemento = MUROS`. Uno modelado como cimentación:
   `CIMIENTOS - MCO-M1` y `Metrado - Elemento = CIMIENTOS`.
3. Esquinero en L: las barras de cada ala llevan la partición de su propio anfitrión.
4. Revit en español: la partición se escribe (antes no).
5. Rearmar: pregunta borrar/conservar; sin duplicados con "borrar".
6. Modelo con barras `MC-M1`: ofrece migrar; tras migrar `MUROS - MCO-M1` (o `CIMIENTOS - …`) con origen.
7. Con el plugin de metrados: aparecen en "Metrado acero - Muros" (marcar el grupo Muros en la ventana) o en
   "… - Cimentaciones" según el anfitrión.

**Detente aquí**: resume los cambios archivo por archivo y espera a que yo pruebe la lista en Revit.
