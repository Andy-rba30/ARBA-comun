# Prompts de integración, uno por repo

Pégalos en una sesión de Claude Code abierta en el repo indicado, **en este orden** (cada prompt termina pidiendo
que la sesión se detenga para que pruebes en Revit antes de fusionar):

| Orden | Prompt | Repo | Por qué en ese momento |
|---|---|---|---|
| 1 | `01-Exportacion-metrados-excel.md` | Exportacion-metrados-excel | Crea los parámetros, trae el botón de migración y la tabla de misceláneos: todo lo demás se ve ahí |
| 2 | `02-Acero-Zapatas.md` | Acero-Zapatas | El más pequeño y con tests: valida el patrón (partición, origen, borrar/rearmar, migración) |
| 3 | `03-Fosa_transformadores.md` | Fosa_transformadores | Único con borrar/rearmar propio y el que escribe partida, peso y pernos: cierra el circuito con el metrado |
| 4 | `04-Acero-cimientos-corridos.md` | Acero-cimientos-corridos | Comparte CIMIENTOS con Zapatas y Bloques; corrige el namespace duplicado |
| 5 | `05-Acero-vigas.md` | Acero-vigas | Mecánico |
| 6 | `06-Acero-columnas.md` | Acero-columnas | Mecánico |
| 7 | `07-Acero-losas.md` | Acero-losas | Mecánico, con tests |
| 8 | `08-Acero-automatico.md` | Acero-automatico (muros de contención) | El más distinto |

Antes de pegar cada prompt comprueba que el repo está en la rama que dice su cabecera (o en `main` si ya fusionaste
algo) y que `ARBA-comun` tiene la etiqueta `v1.0.0`.
