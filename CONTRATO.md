# Contrato ARBA-comun — versión 1.0.0

Este documento describe lo que todos los add-ins ARBA comparten. La fuente de verdad legible por máquina es
`contrato.json`; el código la refleja en `src/ArbaContract.cs` y los tests de `tests/` comprueban que los tres
coinciden. Cualquier cambio se hace en los tres sitios y pasa por el versionado del apartado 4.

## 1. Parámetros compartidos (GUID fijos, nunca cambian)

Todos son **parámetros compartidos de ejemplar**, vinculados por `ArbaSharedParams.Ensure` al grupo *Datos*
(`PG_DATA` / `GroupTypeId.Data`) desde un archivo de definiciones **temporal** (grupo `ARBA`), restaurando siempre
`Application.SharedParametersFilename` del usuario. Se identifican por GUID (`Element.get_Parameter(Guid)`), nunca
solo por nombre.

| Parámetro | GUID | Tipo | Categorías vinculadas | Quién lo escribe | Quién lo lee |
|---|---|---|---|---|---|
| **ARBA - Origen** | `778D89FB-06FB-4455-B0CB-9F3CE40655F3` | texto | Armaduras, mallas, armazón estructural, pilares, modelos genéricos, conexiones estructurales, rigidizadores, cubiertas | Cada add-in al crear (`ZAPATAS`, `CIMIENTOS CORRIDOS`, `BLOQUES`, `VIGAS`, `COLUMNAS`, `LOSAS`, `MUROS DE CONTENCION`, `MUROS`); el plugin de metrados escribe `MANUAL` en "Asignar partición"; la migración | Cada add-in para encontrar, borrar y rearmar lo suyo (`ArbaOrigin.Find`); el plugin de metrados para no pisar peso ni partición |
| **ARBA - Código** | `AADB7B24-22B1-4D5B-8F24-E2E4468C16B3` | texto | Las mismas | Cada add-in al crear: `F1`…`F8`, `inferior`, `superior-sec`, `estribo`, `baston`, `REJILLA P1`, `ANGULO longCore`… | Los add-ins (borrar una familia, informes); el plugin de metrados como columna informativa |
| **ARBA - Anfitrión** | `F5CE04ED-FD80-4C5E-88EF-1EA46162E8C9` | texto | Armazón, pilares, modelos genéricos, conexiones, rigidizadores, cubiertas (lo que un add-in crea y **no** es armadura) | Fosa_transformadores en rejillas y ángulos (Id del bloque como texto) | `ArbaOrigin.Find`/`Delete` para "borrar y rearmar" sin Comentarios. *Añadido respecto a la lista inicial:* las armaduras conocen su anfitrión por la API, los `FamilyInstance` no |
| **Metrado - Partida** | `379229AB-6C40-4CFC-81B7-10DF6468B842` | texto | Armazón, pilares, modelos genéricos, conexiones, rigidizadores, cubiertas | Fosa_transformadores (`ESTRUCTURAS METÁLICAS - REJILLAS`, `ESTRUCTURAS METÁLICAS - ÁNGULOS`); el usuario a mano | Plugin de metrados: tabla multicategoría "Metrado acero estructural - Misceláneos" agrupada por partida |
| **Metrado - Material** | `5B7E3C1A-2D4F-4A6B-9C8D-0E1F2A3B4C5D` | texto | Armazón, pilares, cimentaciones, suelos, muros, rigidizadores, conexiones, modelos genéricos, cubiertas | Plugin de metrados (`CONCRETO`, `ACERO ESTRUCTURAL`, `MADERA`, `OTRO`); Fosa_transformadores escribe `ACERO ESTRUCTURAL` en rejillas y ángulos | Plugin de metrados (filtros de tablas y de vista, resumen) |
| **Metrado - Peso (kg)** | `7D2A9F4E-6B1C-4C3D-8E5F-1A2B3C4D5E6F` | número | Armaduras, mallas, armazón, pilares, rigidizadores, conexiones, modelos genéricos, cubiertas | Plugin de metrados (armaduras, perfiles, piezas); Fosa_transformadores en rejillas (m² × kg/m²) y ángulos (m × kg/m). **El plugin no sobrescribe un valor > 0 si `ARBA - Origen` no está vacío** | Plugin de metrados (columnas y totales), Excel |
| **Metrado - Pernos (und)** | `E63C6327-2A69-469F-A237-947AF6BF6AB7` | entero | Armazón, pilares, modelos genéricos, conexiones, rigidizadores | Fosa_transformadores en cada ángulo (`boltsPerAngle`) | Plugin de metrados (suma por partida en la tabla de misceláneos y en el Excel) |
| **Metrado - Elemento** | `9A4C7E21-3B5D-4F8A-A6C2-2D3E4F5A6B7C` | texto | Armaduras, mallas y todas las categorías de anfitrión | Plugin de metrados siempre (`VIGAS`, `COLUMNAS`, `CIMIENTOS`, `LOSAS`, `MUROS`, `CONEXIONES`, `OTROS`; `MISCELANEOS` si el elemento tiene `Metrado - Partida`); cada add-in al crear, con la CATEGORIA de la partición (prerrelleno); Fosa_transformadores escribe `MISCELANEOS` en rejillas y ángulos | Plugin de metrados: es el filtro real de "Metrado acero - <elemento>" y de los filtros de vista |

Decisiones:

- `Metrado - Material`, `Metrado - Peso (kg)` y `Metrado - Elemento` **conservan los GUID que ya usa
  Exportacion-metrados-excel en `main`**: los modelos ya metrados no necesitan migración. El inventario mostró que en
  `main` ya son compartidos; la migración del apartado 4 cubre los modelos con parámetros homónimos manuales o de
  otro GUID.
- `Metrado - Elemento` entra en el contrato aunque no estaba en la lista inicial porque es el filtro real de las tablas
  de acero del plugin y sus valores son exactamente la CATEGORIA de la partición.
- `ARBA - Anfitrión` se añade para que rejillas y ángulos dejen de depender de Comentarios.
- El valor `MISCELANEOS` de `Metrado - Elemento` saca a los elementos con `Metrado - Partida` de las tablas de
  Vigas / Conexiones / Otros (que filtran por ese parámetro) y los lleva a la tabla "Metrado acero estructural -
  Misceláneos" agrupada por partida, sin contar dos veces el peso.
- Valores de texto siempre en **mayúsculas sin acentos** salvo `Metrado - Partida` (libre, se recomienda mayúsculas).

## 2. Partición del acero

```
<CATEGORIA> - <PREFIJO>-{marca}-{código}
```

- **CATEGORIA** se deduce de la categoría del **anfitrión**, nunca del add-in:
  `OST_StructuralFoundation` → `CIMIENTOS`, `OST_StructuralFraming` → `VIGAS`, `OST_StructuralColumns` (y
  `OST_Columns`) → `COLUMNAS`, `OST_Floors` → `LOSAS`, `OST_Walls` → `MUROS`; cualquier otra → `OTROS`.
  Coincide con los grupos del plugin de metrados y con `Metrado - Elemento`. Consecuencia asumida: un sobrecimiento
  armado por Acero-cimientos-corridos pero modelado como muro queda en `MUROS - CCO-…` (es como ya lo agrupa el
  metrado); el prefijo conserva quién lo armó.
- **Separador de categoría**: ` - ` (espacio, guion, espacio). **Separador de campos**: `-`.
- **{marca}**: parámetro Marca del anfitrión; si está vacía, su Id.
- **{código}** es **opcional**: cada add-in decide qué unidad de agrupación le interesa en las tablas. Por defecto
  solo Bloques lo incluye (`F1`…`F8`, como hoy); Zapatas, Cimientos, Vigas, Columnas, Losas y Muros lo dejan vacío
  (una partición por elemento, como hoy) y el detalle queda en `ARBA - Código`. Un comodín vacío desaparece con su
  separador: `CIMIENTOS - ZAP-Z1`.
- Plantilla por defecto en `config.json`: `{categoria} - {prefijo}-{marca}-{codigo}`. El usuario puede cambiarla,
  pero una plantilla que no empieza por `{categoria} - {prefijo}-` incumple el contrato y la ventana lo avisa
  (`ArbaPartition.TemplateFollowsContract`).

Prefijos (tres letras, uno por add-in; el prefijo identifica **quién armó**, la categoría dice **dónde**):

| Prefijo | ARBA - Origen | Add-in | Antes | Motivo del ajuste |
|---|---|---|---|---|
| `ZAP` | `ZAPATAS` | Acero-Zapatas | `ZAP` | — |
| `CCO` | `CIMIENTOS CORRIDOS` | Acero-cimientos-corridos | `CC` | tres letras uniformes |
| `BLQ` | `BLOQUES` | Fosa_transformadores | `BLQ` | — |
| `VIG` | `VIGAS` | Acero-vigas | `VIG` | — |
| `COL` | `COLUMNAS` | Acero-columnas | `COL` | — |
| `LOS` | `LOSAS` | Acero-losas | `LOSA` | tres letras uniformes |
| `MCO` | `MUROS DE CONTENCION` | Acero-automatico (muros de contención) | `MC` | tres letras; es un add-in distinto de los muros estructurales |
| `MUR` | `MUROS` | reservado para un futuro add-in de placas | — | propuesto en el encargo; no lo usa ningún repo hoy |
| `MAN` | `MANUAL` | Exportacion-metrados-excel ("Asignar partición" en armaduras sin origen ARBA) | — | permite que el acero manual siga la misma gramática (`VIGAS - MAN-V1`) |

Ejemplos: `CIMIENTOS - ZAP-Z1`, `CIMIENTOS - BLQ-FT-01-F4`, `VIGAS - VIG-V-101`, `COLUMNAS - COL-C3`,
`LOSAS - LOS-L2`, `MUROS - MCO-MC1`, `CIMIENTOS - CCO-1234` (sin marca, con Id), `VIGAS - MAN-V7`.

Lectura tolerante (`ArbaPartition.Parse`): reconoce la forma del contrato, las particiones **antiguas** sin
categoría (`ZAP-Z1`, `CC-C1`, `BLQ-FT-01-F1`, `VIG-V1`, `COL-C1`, `LOSA-L1`, `MC-M1`), las de **solo categoría**
que escribe el plugin de metrados (`VIGAS`) y devuelve `Unknown` para cualquier otra. Las marcas pueden contener
guiones: el código solo se separa cuando coincide con uno de los códigos conocidos del prefijo.

## 3. Cinta

- Pestaña **`ARBA`**. Paneles, siempre en este orden y ocultos hasta que alguien añade un botón: `IA` (reservado),
  **`Acero`**, **`Metrados`**, `Encofrado` (reservado). `IA` y `Encofrado` se mantienen porque todos los add-ins
  actuales ya los crean; quitarlos rompería la mezcla de versiones durante la integración.
- Panel `Acero`: un único desplegable con nombre interno y texto **`Acero`** (se conserva el nombre actual para que
  los add-ins ya instalados y los migrados compartan el mismo desplegable). Cada add-in de armado añade su botón
  con `ArbaRibbon.AddToPulldown`.
- Panel `Metrados`: botones sueltos de Exportacion-metrados-excel con `ArbaRibbon.AddButton`.
- Nombres internos únicos (los de los add-ins de armado no cambian):

| Nombre interno | Texto | Panel / desplegable | Repo |
|---|---|---|---|
| `ARBA_Acero_Zapatas` | Zapatas | Acero / Acero | Acero-Zapatas |
| `ARBA_Acero_Cimientos` | Cimientos/ Sobrecimientos | Acero / Acero | Acero-cimientos-corridos |
| `ARBA_Acero_Bloques` | Bloques con foso | Acero / Acero | Fosa_transformadores |
| `ARBA_Acero_Vigas` | Vigas | Acero / Acero | Acero-vigas |
| `ARBA_Acero_Columnas` | Columnas | Acero / Acero | Acero-columnas |
| `ARBA_Acero_Losas` | Losas | Acero / Acero | Acero-losas |
| `ARBA_Acero_Muro` | Muro de contencion | Acero / Acero | Acero-automatico |
| `ARBA_Metrados_Exportar` | Exportar a Excel | Metrados | Exportacion-metrados-excel |
| `ARBA_Metrados_Automatico` | Metrado automático | Metrados | Exportacion-metrados-excel |
| `ARBA_Metrados_Particion` | Asignar partición | Metrados | Exportacion-metrados-excel |
| `ARBA_Metrados_Migrar` | Migrar particiones y origen | Metrados | Exportacion-metrados-excel |

El botón **Migrar particiones y origen** vive en el plugin de metrados (primero en integrarse) y migra de una vez
todos los prefijos antiguos con `ArbaMigration.MigrateAll`; los add-ins de armado no duplican el botón, pero al
armar un anfitrión con barras antiguas suyas pueden migrarlas con `ArbaMigration.MigrateHost`.

## 4. Versionado y compatibilidad

- `contrato.json` → `version` y `ArbaContract.Version` siguen **semver**. Cada add-in muestra en su informe la
  versión del contrato con la que compiló.
- **MAJOR (rompe)**: cambiar GUID, nombre, tipo o carácter de un parámetro; quitar un parámetro, un prefijo, una
  categoría o un valor de origen; cambiar la gramática de la partición o el significado de un comodín; renombrar la
  pestaña, un panel, el desplegable o un nombre interno de botón.
- **MINOR (no rompe)**: añadir parámetros (GUID nuevo), prefijos, valores de origen, categorías de vinculación,
  botones, paneles, comodines o alias.
- **PATCH**: descripciones, iconos, textos de botón, documentación.
- **Parámetros antiguos**: `ArbaSharedParams.Ensure` busca un parámetro vinculado con el mismo nombre; si su GUID es
  el del contrato solo completa las categorías; si es un parámetro de proyecto no compartido o con otro GUID, crea
  el del contrato, copia los valores de todos los ejemplares, quita el vínculo antiguo y lo avisa. Nunca borra
  valores.
- **Particiones antiguas**: siguen metrándose (el plugin filtra por `Metrado - Elemento`). "Migrar particiones y
  origen" las convierte a la forma nueva, rellena `ARBA - Origen`, `ARBA - Código` (si la partición lo contenía) y
  `Metrado - Elemento`, sin rearmar nada. Un add-in que encuentra barras antiguas suyas en un anfitrión no las
  reconoce como propias hasta migrarlas (no hay `ARBA - Origen`), así que ofrece migrar antes de rearmar.
- **Mezcla de versiones** durante la integración: la pestaña, el desplegable `Acero` y los nombres de parámetro
  no cambian, así que un add-in viejo y uno nuevo conviven en la misma cinta y el mismo modelo.
