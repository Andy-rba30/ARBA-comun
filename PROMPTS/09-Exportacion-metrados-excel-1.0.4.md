# Prompt: contrato 1.0.4 en Exportacion-metrados-excel (ExportacionMetrados)

Estás en el repo **Exportacion-metrados-excel** (rama `main`, commit 56d80d0 o posterior, con el submódulo
`external/ARBA-comun` ya integrado). Vas a adoptar la versión **1.0.4** del contrato ARBA-comun
(https://github.com/Andy-rba30/ARBA-comun, etiqueta `v1.0.4`). Trabaja en la rama `claude/arba-comun-1.0.4`.
No modifiques nada dentro de `external/ARBA-comun`.

## Qué cambia en 1.0.4 y por qué te afecta

Las cimentaciones se suelen modelar como suelos. Hasta 1.0.3 la categoría de la partición y `Metrado - Elemento`
salían de la categoría de Revit del anfitrión, así que el acero de un cimiento corrido o de una zapata armados en un
suelo quedaba `LOSAS - CCO-…` y la tabla "Metrado acero - Cimentaciones" vacía. Desde 1.0.4:

- Cada prefijo fija su categoría (`ArbaPrefix.Category`: ZAP, CCO y BLQ → CIMIENTOS; VIG, COL, LOS, MUR → la suya;
  MCO y MAN → la del anfitrión). `ArbaPartition.BuildFor` y `ArbaOrigin.WriteFor` ya la aplican: los add-ins de
  armado no cambian código.
- `ArbaMetrado.ElementoFor(rebar, host, hostGroup)` devuelve el grupo de metrado que le toca a una armadura: la
  categoría que **declara su partición** (la fija de su prefijo, o la del texto: `CIMIENTOS - ZAP-Z1`,
  `LOSAS - CCO-12` → `CIMIENTOS`, `MUROS - MCO-M1` → `MUROS`, `CIMIENTOS` a secas → `CIMIENTOS`) y, solo si la
  partición no declara ninguna (texto libre, antigua `MC-M1`, vacía), `hostGroup` (tu grupo por categoría del
  anfitrión) o la categoría del anfitrión; `OTROS` sin anfitrión.
- La migración (`ArbaMigration`, tu botón "Migrar particiones y origen") corrige `LOSAS - CCO-12` →
  `CIMIENTOS - CCO-12` y deja `Metrado - Elemento` igual a la categoría de la partición; el resumen añade
  "Metrado - Elemento corregidos". No tienes que tocar `MigrarParticionesCommand`.

Tu `ClasificadorElementos.RellenarElementoRefuerzo` sobrescribe **siempre** `Metrado - Elemento` con el grupo de la
categoría del anfitrión. Sin cambiarlo, "Metrado automático" volvería a poner `LOSAS` en el acero de los cimientos
modelados como suelo y desharía lo que escriben los add-ins y la migración.

## Cambios

1. **Submódulo**: `git -C external/ARBA-comun fetch --tags origin && git -C external/ARBA-comun checkout v1.0.4`;
   `git add external/ARBA-comun`.
2. **`src/ExportacionMetrados/Core/Metrado/ClasificadorElementos.cs`**, método `RellenarElementoRefuerzo`: sustituye
   ```csharp
   else valor = CategoriaMetrado.DeCategoria(categorias, host.Category.Id)?.NombreParticion ?? ArbaPartition.CategoryOf(host);
   ```
   por
   ```csharp
   else valor = ArbaMetrado.ElementoFor(r, host, CategoriaMetrado.DeCategoria(categorias, host.Category.Id)?.NombreParticion);
   ```
   y actualiza el comentario del método: "con la categoría que declara su partición (contrato 1.0.4) y, si no declara
   ninguna, el grupo de su anfitrión". El caso sin anfitrión (`"(SIN ANFITRIÓN)"`) se queda como está.
3. **`AsignarParticion`** no cambia: `ArbaPartition.BuildFor(host, ArbaContract.Manual)` y `ArbaOrigin.WriteFor` ya
   siguen la regla (MAN usa la categoría del anfitrión).
4. Si `EsParticionProtegida` o los filtros de vista (`GeneradorFiltrosVista`) deducen en algún sitio la categoría de
   una armadura ARBA con `ArbaPartition.CategoryOf(host)`, cámbialo por `ArbaPartition.DeclaredCategory(ArbaPartition.ParseOf(r))`
   con respaldo en la del anfitrión (`ArbaMetrado.ElementoFor` ya lo hace). Si no hay ningún sitio así, no inventes
   cambios.
5. **Tests**: el plugin no tiene proyecto de tests propio; las comprobaciones
   `ArbaPartition.DeclaredCategory(ArbaPartition.Parse("LOSAS - CCO-12")) == "CIMIENTOS"` y
   `ArbaPartition.CategoryFor(ArbaContract.CimientosCorridos, "LOSAS") == "CIMIENTOS"` están en
   `external/ARBA-comun/tests` (`cd external/ARBA-comun/tests && dotnet run`); ejecútalas.
6. **`NOTAS-ARBA-COMUN.md`**: añade una entrada "1.0.4" con lo que cambiaste y cualquier incidencia del común.
7. Compila (`dotnet build -c Release`, con `EnableWindowsTargeting` si no estás en Windows) y pasa los tests.
   Confirma con un mensaje claro y detente: el usuario prueba en Revit antes de fusionar.

## Qué comprobar en Revit (lo hará el usuario)

1. Modelo GARITA: **Migrar particiones y origen** → las barras `LOSAS - CCO-476232` pasan a `CIMIENTOS - CCO-476232`
   y su `Metrado - Elemento` a `CIMIENTOS`; el resumen cuenta "Metrado - Elemento corregidos".
2. **Metrado automático** → "Metrado acero - Cimentaciones" ya no está vacía: contiene `CIMIENTOS - CCO-476232` y el
   grupo `CIMIENTOS` escrito a mano (298 barras Ø5/8"), que desaparecen de "Metrado acero - Losas";
   `LOSAS - LOS-475499` sigue en Losas; `COLUMNAS - COL-475833` en Columnas.
3. Rearmar un cimiento modelado como suelo con el add-in de cimientos (ya con el submódulo en v1.0.4): la partición
   nace `CIMIENTOS - CCO-…` y "Metrado automático" no la cambia.
