# Prompt: integrar ARBA-comun en Fosa_transformadores (BlockRebar)

Estás en el repo **Fosa_transformadores** (rama `claude/ecstatic-ptolemy-cehcro`, commit a9691a0 o posterior; no hay
`main`). Vas a integrar el código común **ARBA-comun** (https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.0`)
siguiendo `external/ARBA-comun/INTEGRACION.md` y `CONTRATO.md`. Trabaja en la rama `claude/integrar-arba-comun`. No
modifiques nada dentro de `external/ARBA-comun`; lo que falte va a `NOTAS-ARBA-COMUN.md`.

Tu add-in hoy: ensamblado `BlockRebar`, `net10.0-windows`, `Nice3point.Revit.Api.* 2027.2.*` + `Clipper2`, plantilla
`BLQ-{marca}-{familia}` donde **`{familia}` es el código F1…F8** y `{familiarevit}` la familia de Revit; marca propia
en **Comentarios** (`BlockRebar F#` en barras; `BlockRebar GRID host <id> P1 foso 1` / `BlockRebar ANGLE host <id>
longCore foso 1` en rejillas y ángulos), `FindPluginRebars`/`DeletePluginRebars` (RebarGenerator) y
`FindPluginItems`/`DeletePluginItems` (GridGenerator) por ese comentario; `NameMatch.cs` propio (misma API que el
común); `Log.cs` propio (se queda); kg de ángulos (parámetro `W`, `boltsPerAngle` = 5) y de rejillas (`Peso por m2`)
solo en el informe. En el contrato: prefijo **`BLQ`**, origen **`BLOQUES`**, categoría del anfitrión (bloques =
`CIMIENTOS`), partición por defecto **con código**: `CIMIENTOS - BLQ-FT-01-F4`; rejillas y ángulos llevan
`ARBA - Origen = BLOQUES`, `ARBA - Código` (`REJILLA P1` / `ANGULO longCore`), `ARBA - Anfitrión` (Id del bloque),
`Metrado - Partida` (`ESTRUCTURAS METÁLICAS - REJILLAS` / `… - ÁNGULOS`), `Metrado - Material = ACERO ESTRUCTURAL`,
`Metrado - Peso (kg)`, `Metrado - Pernos (und)` (solo ángulos) y `Metrado - Elemento = MISCELANEOS`.

## Cambios, archivo por archivo

1. **Submódulo y csproj** (`BlockRebar.csproj`): submódulo en `external/ARBA-comun` (checkout `v1.0.0`);
   `<RevitVersion>2027</RevitVersion>` + `<Import Project="external/ARBA-comun/Arba.Comun.props" />`.
2. **`RibbonApp.cs`**: borra la clase `ArbaRibbon`; `IconBloques` pasa a `RibbonApp`; `ArbaRibbon.Ensure(app)` +
   `ArbaRibbon.AddAcero(app, data)`; `using Arba.Comun;`. Nombre interno `ARBA_Acero_Bloques` se conserva.
3. **Borrar** `RevitTheme.cs`, `PartitionName.cs` y `NameMatch.cs` (los comunes tienen la misma API; `BarTypes.cs`
   sigue llamando `NameMatch.Candidates/Unique/IsAmbiguous`).
4. **`AppConfig.cs` / `config.json`**: `partitionTemplate` por defecto `"{categoria} - {prefijo}-{marca}-{codigo}"`.
   **Ojo al cambio de significado**: `{familia}` pasa a ser la familia de Revit (como en los otros seis add-ins) y el
   código F1…F8 es `{codigo}`. En `Normalize`, si la plantilla contiene `{familia}` y no `{codigo}` y empieza por
   `BLQ-` (plantilla antigua), conviértela a la nueva y avisa en el informe.
5. **`HostAnalysis.cs`**: `Partition(cfg, familyName, familyCode, layer)` →
   `ArbaPartition.BuildFor(Host, ArbaContract.Bloques, cfg.PartitionTemplate, new PartitionName.Source { Mark = Mark,
   TypeName = TypeName, FamilyName = FamilyName, SetName = familyName, Code = familyCode })` (la capa u/v queda en
   `ARBA - Código` si la quieres: `Code = familyCode + (layer != "" ? "-" + layer : "")`; en la partición por defecto
   solo va F#). `PluginRebars` → `ArbaOrigin.Find(doc, ArbaContract.Bloques, host)` filtrado a armaduras
   (`ArbaPartition.IsRebar`) **más**, por compatibilidad con modelos no migrados, los que tengan el comentario antiguo
   (deja `RebarGenerator.FindPluginRebars` como respaldo durante esta versión). `PluginGridItems` igual con
   `ArbaOrigin.Find(...)` filtrado a no armaduras + `GridGenerator.FindPluginItems` antiguo.
6. **`RebarGenerator.cs`**: `Finish(doc, r, partition, family)` → `ArbaPartition.Write(r, partition);
   ArbaOrigin.WriteFor(r, host, ArbaContract.Bloques, Families.Code(family));` y **deja de escribir Comentarios**
   (`Marker` se mantiene solo para `FindPluginRebars` de respaldo). `DeletePluginRebars(doc, host, out bars)` →
   `ArbaOrigin.Delete(doc, ArbaContract.Bloques, host, out bars)` + el borrado por comentario antiguo.
7. **`GridGenerator.cs`**: en cada ángulo creado, tras `SetInt(Y/Z_JUSTIFICATION)`:
   `ArbaOrigin.WriteFor(fi, host, ArbaContract.Bloques, "ANGULO " + AngleCategories.Key(a.Category));
   ArbaMetrado.WriteMiscelaneo(fi, ArbaContract.PartidaAngulos, a.Length(m) * angle.KgPerM, g.Angles.BoltsPerAngle);`
   (longitud en metros: `a.Length * 0.3048`). En cada rejilla: `ArbaOrigin.WriteFor(fi, host, ArbaContract.Bloques,
   "REJILLA " + p.Group); ArbaMetrado.WriteMiscelaneo(fi, ArbaContract.PartidaRejillas, areaM2 * plan.Type.KgPerM2, null);`.
   Quita las dos líneas `SetString(fi, ALL_MODEL_INSTANCE_COMMENTS, Marker…)`; el `Pieza` (P1, P2) se mantiene.
   `FindPluginItems`/`DeletePluginItems` → `ArbaOrigin.Find/Delete` (no armaduras) + respaldo por comentario.
8. **`ArmarBloqueCommand.cs`**: tras cada `tx.Start()` de "Armar bloques con foso" y "Colocar rejillas y angulos":
   `ArbaSharedParams.EnsureAll(doc, avisos); doc.Regenerate();` (los ocho parámetros: rejillas y ángulos los necesitan
   todos) y los avisos al informe. Barras antiguas: `ArbaMigration.HasLegacy(doc, host, ArbaContract.Bloques)` →
   tercer `CommandLink` "Migrar la armadura antigua al contrato (sin rearmar)" → `MigrateHost(doc, host, ArbaContract.Bloques)`.
   En el detalle del informe (§3b, línea que imprime "Particion: … | comentario: …") sustituye el comentario por
   `ARBA - Origen/Código`. Botones "Borrar armado del plugin" / "Borrar rejillas y ángulos del plugin" siguen igual
   por dentro llamando a las funciones nuevas.
9. **`SectionViews.cs`** (línea ~263 lee Comentarios para etiquetar): usa `ArbaOrigin.CodeOf(rb)` (F#) con el
   comentario como respaldo.
10. **`RebarOptionsWindow.cs`**: aviso si `!ArbaPartition.TemplateFollowsContract(cfg.PartitionTemplate)`; pie con
    "Contrato ARBA " + `ArbaContract.Version`; `PartitionName.Help` común.
11. **`Tests/BlockRebar.Tests.csproj`**: `..\PartitionName.cs` y `..\NameMatch.cs` → los de
    `..\external\ARBA-comun\src\` (+ `ArbaContract.cs`, `ArbaPartition.cs`). **`Tests/Program.cs`**: las pruebas
    `PartitionName.Expand("BLQ-{marca}-{familia}", … Family = "F4")` pasan a `"{categoria} - {prefijo}-{marca}-{codigo}"`
    con `Category = "CIMIENTOS", Prefix = "BLQ", Code = "F4"` → `"CIMIENTOS - BLQ-FT-01-F4"`; añade
    `ArbaPartition.Parse("CIMIENTOS - BLQ-FT-01-F4").Code == "F4"`. Las de `NameMatch` siguen igual. `dotnet run` en verde
    (hoy 203 comprobaciones).
12. **`README.md` / `INSTALADOR.md` / `PLAN.md`**: submódulo, partición nueva, origen en vez de Comentarios, metrado
    de misceláneos. `dotnet build -c Release` sin errores. Commit y push.

## Lista de verificación en Revit

1. Cinta: una sola pestaña ARBA, desplegable Acero con "Bloques con foso".
2. Armar un bloque marca `FT-01`: conjuntos con Partición `CIMIENTOS - BLQ-FT-01-F1` … `-F8`, `ARBA - Origen = BLOQUES`,
   `ARBA - Código = F#`, `Metrado - Elemento = CIMIENTOS`; Comentarios vacío en las barras nuevas.
3. Colocar rejillas y ángulos: cada rejilla con `ARBA - Origen = BLOQUES`, `ARBA - Código = REJILLA P1`,
   `ARBA - Anfitrión = <id del bloque>`, `Metrado - Partida = ESTRUCTURAS METÁLICAS - REJILLAS`, `Metrado - Material =
   ACERO ESTRUCTURAL`, `Metrado - Peso (kg)` = m² × kg/m² (coincide con el informe), `Metrado - Elemento = MISCELANEOS`;
   cada ángulo igual con partida ÁNGULOS, peso = m × kg/m y `Metrado - Pernos (und) = 5`.
4. Armar / colocar otra vez: pregunta borrar/conservar y con "borrar" no duplica (barras, rejillas ni ángulos).
   "Borrar armado del plugin" y "Borrar rejillas y ángulos del plugin" borran solo lo del add-in.
5. Modelo armado con la versión anterior (`BLQ-FT-01-F1` + Comentarios): el add-in lo reconoce (respaldo por
   comentario), ofrece migrar; tras migrar, partición `CIMIENTOS - BLQ-FT-01-F1` y origen relleno.
6. Gestionar > Parámetros compartidos sigue con tu archivo; nada en `%TEMP%\ARBA-comun-*.txt`.
7. Con el plugin de metrados integrado: "Metrado acero estructural - Misceláneos" muestra REJILLAS y ÁNGULOS con los
   kg y pernos del informe del add-in; los ángulos no salen en "… - Vigas" y el peso no cambia tras un metrado.

**Detente aquí**: resume los cambios archivo por archivo y espera a que yo pruebe la lista en Revit.
