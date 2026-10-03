# Inventario de los add-ins ARBA (fase 0)

Fecha del inventario: 2026-10-03. Repos clonados en modo lectura en `/home/user/andy-rba30/`.
Regla de rama: `main` cuando existe; si no, la rama `claude/*` con el commit más reciente.

| Repo | Rama inventariada (commit) | Rama por defecto en GitHub | Observación |
|---|---|---|---|
| Acero-columnas | `main` (6bed5b0, 2026-09-29) | `main` | 10 commits |
| Acero-vigas | `claude/intelligent-lovelace-nlqbfb` (4d66a34, 2026-10-02) | `claude/affectionate-allen-1kin91` | **No hay `main`.** La rama por defecto es más antigua (2026-09-30) que la elegida |
| Acero-losas | `main` (f4eca62, 2026-10-02) | `claude/brave-allen-3gowqo` | `main` es más nueva que la rama por defecto |
| Acero-Zapatas | `claude/pensive-rubin-nsa1u1` (a9e4039, 2026-10-02) | la misma | **No hay `main`.** Un solo commit |
| Acero-cimientos-corridos | `main` (11052c0, 2026-10-02) | `claude/affectionate-allen-1kin91` | `main` integra las ramas de empalmes, suelos/muros y columnas unidas (ver `PENDIENTE.md`: no probado en Revit) |
| Fosa_transformadores | `claude/ecstatic-ptolemy-cehcro` (a9691a0, 2026-10-03) | la misma | **No hay `main`.** 15 commits |
| Acero-automatico | `main` (5ead63f, 2026-09-29) | `main` | Es el add-in de **muros de contención** (`RetainingWallRebar`), no un "acero automático" genérico |
| Exportacion-metrados-excel | `main` (477acaf, 2026-10-03) | `claude/wizardly-thompson-jw4ffy` | `main` es la más nueva (20 commits) |

## 1. Versiones de Revit y TargetFramework

| Repo | Ensamblado / namespace | TargetFramework | Paquete RevitAPI | Compila fuera de Windows | Despliegue |
|---|---|---|---|---|---|
| Acero-columnas | `ColumnRebar` / `ColumnRebar` | `net10.0-windows` (solo 2027) | `Nice3point.Revit.Api.RevitAPI(+UI) 2027.*` | No (`CopyToRevit` sin condición de SO) | `%AppData%\Autodesk\Revit\Addins\2027\ColumnRebar\` en Debug |
| Acero-vigas | `BeamRebar` / `BeamRebar` | `net10.0-windows` | `2027.*` | No | ídem `BeamRebar` |
| Acero-losas | `SlabRebar` / `SlabRebar` | `net10.0-windows` | `2027.2.*` | Sí (`EnableWindowsTargeting`, `CopyToRevit` solo en Windows) | ídem `SlabRebar` |
| Acero-Zapatas | `FootingRebar` / `FootingRebar` | `net10.0-windows` | `2027.2.*` | Sí | ídem `FootingRebar` |
| Acero-cimientos-corridos | `StripFootingRebar` / **`FootingRebar`** | `net10.0-windows` | `2027.*` | No | ídem `StripFootingRebar` |
| Fosa_transformadores | `BlockRebar` / `BlockRebar` (+ `Clipper2` 1.5) | `net10.0-windows` | `2027.2.*` | Sí | ídem `BlockRebar` (+ `Clipper2Lib.dll`) |
| Acero-automatico | `RetainingWallRebar` / `RetainingWallRebar` | `net10.0-windows` | `2027.*` | No | ídem `RetainingWallRebar` |
| Exportacion-metrados-excel | `ExportacionMetrados` / `ExportacionMetrados` (+ `ClosedXML` 0.102.3) | **`-p:RevitVersion=2021..2027`** → 2021-2024 `net48`, 2025-2026 `net8.0-windows`, 2027+ `net10.0-windows`; define `REVIT$(RevitVersion)` | **No usa NuGet**: `HintPath` a `C:\Program Files\Autodesk\Revit <v>\RevitAPI.dll` | Sí para el framework (`EnableWindowsTargeting`), **no** para la API (necesita Revit instalado) | `%AppData%\Autodesk\Revit\Addins\<v>\ExportacionMetrados\` |

Comprobado en esta máquina (Linux, SDK .NET 10.0.112): un archivo con `Autodesk.Revit.DB` + `System.Windows` compila
contra `Nice3point.Revit.Api.RevitAPI` 2021 (net48), 2025 (net8.0-windows) y 2027 (net10.0-windows) con
`EnableWindowsTargeting=true`. Es la base del proyecto de comprobación de ARBA-comun (`build/`).

Versiones disponibles del paquete `Nice3point.Revit.Api.RevitAPI`: 2021.1.100, 2022.1.80, 2023.1.90, 2024.3.60,
2025.4.60, 2026.4.10, 2027.3.0 (una rama estable por año).

## 2. Partición por defecto y marcado de las barras

| Repo | Plantilla por defecto (`config.json` → `partitionTemplate`) | Comodines propios | Cómo escribe la partición | Marca propia para "borrar y rearmar" |
|---|---|---|---|---|
| Acero-Zapatas | `ZAP-{marca}` | `{capa}` = inferior, inferior-sec, superior, superior-sec | `NUMBER_PARTITION_PARAM`, respaldo `LookupParameter("Partition"/"Particion"/"Partición")` | **Ninguna.** Rearmar duplica las barras |
| Acero-cimientos-corridos | `CC-{marca}` | `{cara}` = superior / inferior / estribo | `LookupParameter("Partition")` **solo** | Ninguna |
| Fosa_transformadores | `BLQ-{marca}-{familia}` | `{familia}` = **código F1…F8** (no la familia de Revit), `{familiarevit}`, `{capa}` = u, v, cara… | `NUMBER_PARTITION_PARAM` + respaldo | **Comentarios** = `BlockRebar F#` en cada conjunto; rejillas y ángulos: `BlockRebar GRID host <id> P1 foso 1` / `BlockRebar ANGLE host <id> …`. `FindPluginRebars` recorre `RebarHostData.GetRebarsInHost` y filtra por el prefijo del comentario; pregunta "borrar y rearmar / conservar" |
| Acero-vigas | `VIG-{marca}` | `{cara}` = superior / inferior / estribo | `LookupParameter("Partition")` solo | Ninguna |
| Acero-columnas | `COL-{marca}` | `{estribo}` = número de estribo | `LookupParameter("Partition")` solo | Ninguna |
| Acero-losas | `LOSA-{marca}` | `{capa}` = inferior, baston, temperatura… | `NUMBER_PARTITION_PARAM` + respaldo | Ninguna |
| Acero-automatico | `MC-{marca}` | `{ala}`, `{conjunto}`; si todo queda vacío devuelve `MURO <id>` | `LookupParameter("Partition")` solo | Ninguna |
| Exportacion-metrados-excel | — (escribe `VIGAS`, `COLUMNAS`, `CIMIENTOS`, `LOSAS`, `MUROS` en las particiones **vacías**; "Asignar partición" puede sobrescribir con ese texto o uno libre) | — | `NUMBER_PARTITION_PARAM` | — |

Comodines comunes a los 7 add-ins: `{marca}` (Marca del anfitrión; vacía → Id), `{id}`, `{tipo}`, `{familia}`, `{conjunto}`.
Las 7 copias de `PartitionName.cs` tienen la misma regla de limpieza de separadores huérfanos, salvo Acero-automatico
(regex distinta, conserva un separador entre dos, y respaldo `MURO <id>`).

Todos los generadores escriben la partición en `RebarGenerator.Finish(doc, rebar, partition)` justo después de crear
cada conjunto (`Rebar.CreateFromCurves` + `SetLayoutAsFixedNumber`/`SetLayoutAsSingle`) y antes de
`SetUnobscuredInView`. El flujo de los 7 comandos es idéntico: `AppConfig.Load()` → `HostAnalysis.Analyze` por
anfitrión → ventana `RebarOptionsWindow` (`ShowDialog`) → `Transaction` + una `SubTransaction` por anfitrión →
`RebarGenerator.Build` → `sub.Commit()` / `sub.RollBack()`.

Categorías de anfitrión que acepta cada add-in: Zapatas `OST_StructuralFoundation`; Cimientos corridos
`OST_StructuralFoundation`, `OST_StructuralFraming`, `OST_Floors`, `OST_Walls` (sobrecimientos como muro);
Bloques `OST_StructuralFoundation`; Vigas `OST_StructuralFraming`; Columnas `OST_StructuralColumns`; Losas
`OST_Floors`; Muros de contención `OST_Walls` y `OST_StructuralFoundation`.

## 3. Parámetros de proyecto o compartidos que crea o lee cada repo

| Repo | Lee | Escribe |
|---|---|---|
| 7 add-ins de acero | `ALL_MODEL_MARK` del anfitrión; `RebarBarType.BarNominalDiameter`, `StandardBendDiameter`, `StirrupTieBendDiameter`; `RebarHookType` | Partición de cada conjunto (`NUMBER_PARTITION_PARAM` o `LookupParameter("Partition")`) |
| Fosa_transformadores (además) | `W`/`w`/`Peso`/`Weight` del tipo de ángulo (kg/m; lb/ft si es número), `Peso por m2` del tipo de rejilla | Comentarios (`ALL_MODEL_INSTANCE_COMMENTS`) en barras, rejillas y ángulos; parámetros de familia que crea en la familia de rejilla: `Largo`, `Ancho`, `Espesor` (ejemplar), `Peso por m2`, `Peso` (fórmula), `Designacion`, `Pieza` (ejemplar, P1/P2…); `Y_JUSTIFICATION`, `Z_JUSTIFICATION`, `STRUCTURAL_BEND_DIR_ANGLE` en ángulos. **No escribe nada de metrado** (kg, pernos, partida) en el modelo: solo en el informe |
| Exportacion-metrados-excel | `NUMBER_PARTITION_PARAM`, `REBAR_ELEM_QUANTITY_OF_BARS`, `REBAR_ELEM_LENGTH`, `REBAR_ELEM_TOTAL_LENGTH`, `REBAR_BAR_DIAMETER`, `Bar Mass per Unit Length` (u otros nombres), `STRUCTURAL_MATERIAL_PARAM`, `HOST_VOLUME_COMPUTED`, `STRUCTURAL_SECTION_AREA`, `STRUCTURAL_FRAME_CUT_LENGTH`, niveles, `FabricSheet` masas | **Parámetros compartidos de ejemplar, con GUID fijo**, creados desde un archivo de definiciones propio (`%LocalAppData%\ExportacionMetrados\ParametrosMetrado.txt`, grupo `Metrados`) restaurando `SharedParametersFilename` en `finally`: **`Metrado - Material`** (texto; `5B7E3C1A-2D4F-4A6B-9C8D-0E1F2A3B4C5D`; valores `CONCRETO`, `ACERO ESTRUCTURAL`, `MADERA`, `OTRO`), **`Metrado - Peso (kg)`** (número; `7D2A9F4E-6B1C-4C3D-8E5F-1A2B3C4D5E6F`), **`Metrado - Elemento`** (texto; `9A4C7E21-3B5D-4F8A-A6C2-2D3E4F5A6B7C`; grupo de metrado `VIGAS`, `COLUMNAS`, `CIMIENTOS`, `LOSAS`, `MUROS`, `CONEXIONES`, `OTROS`, en el refuerzo el del anfitrión). Partición vacía del refuerzo (nombre del grupo). Grupo de parámetro `PG_DATA` / `GroupTypeId.Data` |

Nota sobre la premisa de la tarea: en `main` (y en las tres ramas `claude/*`) los parámetros de metrado **ya son
compartidos con GUID fijo** (desde el commit b8a5766, 2026-10-02); en ningún commit se crearon como parámetros de
proyecto no compartidos. Sí hay dos riesgos reales que la migración del contrato debe cubrir: (a) modelos donde el
usuario creó a mano un parámetro de proyecto `Metrado - Material` / `Metrado - Peso (kg)` (no compartido o con otro
GUID) antes de usar el plugin; (b) el plugin reutiliza **cualquier** definición vinculada con ese nombre
(`BuscarDefinicionVinculada` compara solo el nombre), así que un parámetro manual con el mismo nombre se rellena
pero nunca se convierte al GUID fijo y las tablas/filtros (que buscan por GUID) no lo encuentran.

## 4. Código duplicado entre repos

| Archivo | Repos | Estado |
|---|---|---|
| `RibbonApp.cs` → clase `ArbaRibbon` (pestaña ARBA, paneles IA/Acero/Encofrado ocultos, `AddToPulldown`, `IconAcero`, `IconEncofrado`) | 7 add-ins de acero | **Idéntica** salvo namespace y comentarios. Vigas y Cimientos añaden `IconVigas` dentro de `ArbaRibbon`; Losas `IconLosas`; Zapatas `IconZapatas`; Bloques `IconBloques`; Muros un `Icon` privado en `RibbonApp` |
| `RevitTheme.cs` (tema oscuro WPF, 550 líneas, XAML embebido, barra de título oscura por `DwmSetWindowAttribute`) | Columnas, Vigas, Losas, Zapatas, Cimientos, Bloques | **Idéntica** salvo namespace. Acero-automatico y el plugin de metrados no lo tienen (el de metrados usa XAML compilado y ventanas claras) |
| `PartitionName.cs` | 7 add-ins | Misma regla en 6; variantes de comodines (`{capa}`/`{cara}`/`{estribo}`/`{ala}`; `{familia}` significa cosas distintas en Bloques); Acero-automatico distinta |
| `RebarGenerator.MatchName` / `FindBarType` / `FindHookType` (exacto, si no **el primer** fragmento que coincide) | Zapatas, Cimientos, Columnas, Losas, Vigas, Muros | Idéntico en los 6. Bloques usa `NameMatch` (`Candidates`/`Unique`/`IsAmbiguous`): un fragmento ambiguo **no se elige en silencio** |
| `Log.cs` (`%Temp%\BlockRebar.log`, 2 MB, `Write`/`Block`/`Error`) | solo Bloques | No duplicado |
| `Geometry2D.cs` (`Pt`, `Span`, `Outline2D`, cortes de rectas contra contornos con huecos) | Losas, Zapatas, Bloques | Zapatas ⊂ Bloques (Bloques añade `CutExact`, `RawLineCut`, rectas generales y una línea cambiada en `Cut`); Losas = Zapatas + `Support` y `JoistAxes` (viguetas). En evolución, no estable |
| `Rectilinear.cs` (sección rectilínea por rectángulos máximos) | Vigas, Columnas, Cimientos | Idéntico en los 3 (261 líneas), sin cambios desde su primer commit |
| `AppConfig.cs` (`ConfigPath` junto a la DLL, `Load`, `Save`, `Clone`, `Normalize`, `PartitionTemplate`) | 7 add-ins | Mismo patrón; contenido distinto por add-in. Bloques usa `[JsonPropertyName]` y `Load(path)` |
| `HostAnalysis.cs` (`Mark`, `TypeName`, `FamilyName`, `Tag`, `Partition(cfg, …)`, `RebarHostData.IsValidHost`) | 7 add-ins | Mismo esqueleto, cuerpo distinto |
| `BeamProfile/BeamPlan/BeamSection/SpliceLayout/StirrupLayout/BastonPreview/ElevationPreview/SectionPreview` | Vigas ↔ Cimientos (fork) | Divergentes (BeamSection 197 líneas de diferencia, SpliceLayout 112) |
| `Tests/Program.cs` (consola `Check(...)`, código de salida 1 si falla) | Zapatas (66), Losas (98), Bloques (203 comprobaciones) | Mismo estilo; los otros 4 add-ins y el plugin de metrados no tienen tests |

## 5. Cinta

| Repo | Pestaña | Panel | Desplegable | Botón: nombre interno → texto | Icono |
|---|---|---|---|---|---|
| Acero-Zapatas | ARBA | Acero | Acero | `ARBA_Acero_Zapatas` → "Zapatas" | `IconZapatas` |
| Acero-cimientos-corridos | ARBA | Acero | Acero | `ARBA_Acero_Cimientos` → "Cimientos/\nSobrecimientos" | `IconVigas` (el de vigas) |
| Fosa_transformadores | ARBA | Acero | Acero | `ARBA_Acero_Bloques` → "Bloques con foso" | `IconBloques` |
| Acero-vigas | ARBA | Acero | Acero | `ARBA_Acero_Vigas` → "Vigas" | `IconVigas` |
| Acero-columnas | ARBA | Acero | Acero | `ARBA_Acero_Columnas` → "Columnas" | `IconAcero` (el mismo del desplegable) |
| Acero-losas | ARBA | Acero | Acero | `ARBA_Acero_Losas` → "Losas" | `IconLosas` |
| Acero-automatico | ARBA | Acero | Acero | `ARBA_Acero_Muro` → "Muro de contencion" | `Icon` propio |
| Exportacion-metrados-excel | **Metrados** | **Exportar** | — (botones sueltos) | `ExportarMetradosExcel` → "Exportar a\nExcel"; `MetradoAutomatico` → "Metrado\nautomático"; `AsignarParticion` → "Asignar\npartición" | PNG en `Resources/` |

`ArbaRibbon.Ensure` crea siempre los paneles `IA`, `Acero` y `Encofrado` (ocultos hasta que alguien añade un botón);
ningún add-in usa `IA` ni `Encofrado`. Manifiestos: los 7 add-ins usan `ClientId` + `VendorId=LOCAL`; el de metrados
`AddInId` (forma antigua) + `VendorId=DBRS`. Nombres de aplicación: "ARBA", "ARBA Columnas", "ARBA Vigas", "ARBA Losas",
"ARBA Zapatas", "ARBA Cimientos/Sobrecimientos", "ARBA Bloques", "Exportación de Metrados a Excel".

## 6. Cómo clasifica el plugin de metrados

- **Grupos** (`CategoriaMetrado.Predeterminadas`): Vigas `OST_StructuralFraming` → `VIGAS`; Columnas
  `OST_StructuralColumns` → `COLUMNAS`; Cimentaciones `OST_StructuralFoundation` → `CIMIENTOS` (zapatas, cimientos
  corridos, bloques con foso, muros de contención modelados como cimentación); Losas `OST_Floors` → `LOSAS`; Muros
  `OST_Walls` → `MUROS` (desmarcado por defecto); "Conexiones y anclajes" (`OST_StructuralStiffener` + por nombre en
  cualquier categoría metálica) → `CONEXIONES`; "Otros" (`OST_StructConnections`, `OST_GenericModel`, `OST_Roofs`) →
  `OTROS`. Los nombres de grupo son exactamente los valores de `Metrado - Elemento`.
- **`Metrado - Material`**: `ClasificadorElementos.Clasificar` por este orden: `StructuralMaterialType` de la
  familia; materiales asignados (concreto si clase/nombre contiene "concret", "hormig" o `f'c` como palabra, o activo
  estructural `Concrete`; metálico si "metal"/"acero"/"steel"/"alumin"); nombre de familia/tipo (pistas `acero`,
  `steel`, `hss`, `ipe`, `w `, `l `, `angle`, `ángulo`…; en categorías metálicas también pistas de conexión
  `perno`, `anclaje`, `plancha`, `stud`…); material por defecto de la categoría; sin datos: losas/muros/cimentaciones
  → `CONCRETO`, conexiones/rigidizadores → `ACERO ESTRUCTURAL`, resto → `OTRO`. Se escribe en cada ejecución salvo
  "Conservar la clasificación ya escrita".
- **`Metrado - Peso (kg)`**: armaduras = `REBAR_ELEM_TOTAL_LENGTH` × kg/m (parámetro del tipo o π·d²/4·7850);
  perfiles (vigas/columnas `ACERO ESTRUCTURAL`) = `Cut Length` × área de sección × 7850; piezas sin longitud
  (conexiones, modelos genéricos, cubiertas) = volumen × 7850. **Se sobrescribe en cada ejecución** (no respeta un
  valor escrito por otro add-in).
- **Tablas** (`GeneradorTablasRevit`): `Metrado concreto - <grupo>` (filtro `Metrado - Material = CONCRETO` y
  `Metrado - Elemento = <GRUPO>`), `Metrado acero estructural - <grupo>` (`≠ CONCRETO`; las de Conexiones y Otros son
  multicategoría), `Metrado acero - <grupo>` (categoría `OST_Rebar`; **filtro `Metrado - Elemento = <GRUPO>`**,
  respaldo partición `= <GRUPO>` y luego categoría del anfitrión; agrupada por Partición → tipo de barra), `Metrado
  acero - General` (Elemento → Partición → tipo). Las tablas antiguas (agrupadas por nivel, sin filtro por material o
  filtradas por partición) se regeneran con aviso.
- **Filtros de vista** (`GeneradorFiltrosVista`): `Metrado - Concreto - <grupo>`, `Metrado - Acero estructural -
  <grupo>`, `Metrado - Refuerzo - <GRUPO>` (regla `Metrado - Elemento =`; por partición solo si el parámetro no existe).
- **Partición del acero**: antes de las tablas rellena la partición **vacía** con el nombre del grupo; "Asignar
  partición" escribe el grupo o un texto libre a la selección, a elementos elegidos o a todo el modelo, con opción de
  sobrescribir. Hoy no existe ninguna noción de prefijo por add-in ni de categoría dentro de la partición.
- **Excel**: hojas `Resumen` (m³, kg, kg/m³ por grupo, acero por diámetro), una hoja por tabla de Revit y hojas de
  detalle opcionales. No existe "Metrado - Partida" ni ninguna tabla de misceláneos agrupada por partida; las rejillas
  (modelo genérico) caen en "Otros" por volumen × densidad y los ángulos (vigas `L-Angle`) en "Vigas" por longitud ×
  área × densidad, ignorando los kg/m y los pernos que calcula el add-in de bloques.

## 7. Incoherencias entre repos

1. **Prefijos de partición sin regla**: `ZAP`, `CC`, `BLQ`, `VIG`, `COL`, `LOSA`, `MC` (2, 3 y 4 letras; `LOSA`
   y `MC` no siguen el patrón de tres letras). Ninguna partición dice la categoría del anfitrión, que es por lo que
   agrupa el plugin de metrados.
2. **El plugin de metrados y los add-ins se pisan la partición**: el plugin rellena `VIGAS`/`CIMIENTOS`… solo si está
   vacía, pero "Asignar partición" con "Sobrescribir" borra las particiones `ZAP-…`/`VIG-…` de los add-ins, y las
   tablas por elemento ya no dependen de la partición sino de `Metrado - Elemento`.
3. **Escritura de la partición en Revit en español**: Cimientos, Vigas, Columnas y Muros usan
   `LookupParameter("Partition")` (nombre en inglés); en una interfaz en español el parámetro se llama "Partición" y
   la partición **no se escribe**. Zapatas, Losas y Bloques usan `NUMBER_PARTITION_PARAM` (correcto).
4. **Solo Bloques puede "borrar y rearmar"** (marca en Comentarios). Los otros seis no marcan sus barras: volver a
   armar un elemento duplica la armadura y no hay forma de encontrar las barras de un add-in salvo adivinar el
   prefijo de la partición (que el usuario puede cambiar en `config.json`).
5. **La marca de Bloques vive en Comentarios** (`BlockRebar F1`, `BlockRebar GRID host 123 …`), un parámetro de
   usuario que cualquiera puede editar y que el plugin de metrados no lee.
6. **`{familia}` significa cosas distintas**: familia de Revit en 6 add-ins, código F1…F8 en Bloques (que usa
   `{familiarevit}` para la familia de Revit).
7. **Regla de nombres de tipo de barra distinta**: 6 add-ins eligen en silencio el primer fragmento que coincide
   (`"5/8"` → `5/8"` o `Ø 5/8"`, según el orden alfabético); Bloques marca la ambigüedad y no arma.
8. **Namespace repetido**: Acero-cimientos-corridos usa `namespace FootingRebar` (el de Acero-Zapatas) con ensamblado
   `StripFootingRebar`; los dos manifiestos declaran `FootingRebar.RibbonApp` y `FootingRebar.RebarGenerator`. Revit
   los distingue por ensamblado, pero cualquier fusión de código o de tests choca.
9. **Cinta partida**: 7 add-ins en `ARBA > Acero > Acero`; el plugin de metrados en su propia pestaña `Metrados`
   con panel `Exportar` y nombres internos sin prefijo (`ExportarMetradosExcel`, `MetradoAutomatico`,
   `AsignarParticion`). `ArbaRibbon` crea además los paneles vacíos `IA` y `Encofrado`.
10. **Iconos**: Cimientos reutiliza el icono de Vigas; Columnas usa el icono genérico del desplegable.
11. **Versiones de Revit**: los 7 add-ins solo compilan para 2027 (`net10.0-windows`, paquetes `2027.*` o
    `2027.2.*`, carpeta de despliegue `2027` fija); el plugin de metrados compila de 2021 a 2027 pero contra
    `RevitAPI.dll` instalado (no se puede compilar en CI ni en Linux). Cuatro add-ins no tienen
    `EnableWindowsTargeting` ni la condición `'$(OS)'=='Windows_NT'` en `CopyToRevit`.
12. **Parámetros de metrado**: ya son compartidos con GUID fijo, pero (a) el archivo de definiciones es permanente en
    `%LocalAppData%` en vez de temporal, (b) la búsqueda de una definición existente es por nombre y acepta cualquier
    parámetro homónimo (manual, de proyecto, de otro GUID) sin migrarlo, (c) `Metrado - Peso (kg)` se sobrescribe
    siempre, (d) el parámetro `Metrado - Elemento` existe en el código pero no aparecía en la lista de parámetros del
    contrato pedido, (e) el GUID se escribe en mayúsculas en un sitio y la API lo lee como `Guid`; no hay problema,
    pero no hay un único origen de verdad.
13. **Los misceláneos de Bloques no llegan al metrado**: rejillas (m², kg por `Peso por m2`) y ángulos (kg/m del
    parámetro `W` + pernos por ángulo) solo se informan en la ventana; el plugin de metrados los pesa por volumen o
    por área de sección y los clasifica en "Otros"/"Vigas", sin partida.
14. **Ramas**: tres repos tienen `main` más nueva que la rama por defecto de GitHub y tres no tienen `main`; el repo
    "Acero-automatico" es en realidad el add-in de muros de contención.
15. **`PartitionName` de Acero-automatico** devuelve `MURO <id>` cuando la plantilla queda vacía y limpia separadores
    con otra regla; los otros seis devuelven `""`.
16. **Tests**: solo Zapatas, Losas y Bloques tienen tests de consola; Cimientos tiene un `PENDIENTE.md` con cambios de
    `main` nunca compilados.
17. **Acero-cimientos-corridos** acepta muros, suelos y vigas como anfitrión: con la regla "categoría = la del
    anfitrión" un sobrecimiento modelado como muro irá a `MUROS` (como ya hace hoy el plugin de metrados), no a
    `CIMIENTOS`. El prefijo `CCO` conserva la trazabilidad del add-in.
