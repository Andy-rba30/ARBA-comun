using System;
using System.Collections.Generic;

namespace Arba.Comun
{
    /// <summary>Tipo de dato de un parámetro compartido del contrato.</summary>
    internal enum ArbaParamType { Text, Number, Integer }

    /// <summary>Un parámetro compartido del contrato (nombre, GUID fijo, tipo y categorías a las que se vincula).</summary>
    internal sealed class ArbaParam
    {
        public readonly string Key;
        public readonly string Name;
        public readonly Guid Guid;
        public readonly ArbaParamType Type;
        /// <summary>Categorías de Revit por su nombre de <c>BuiltInCategory</c> ("OST_Rebar"...). Se resuelven en Revit con Enum.Parse.</summary>
        public readonly string[] Categories;
        public readonly string Description;

        public ArbaParam(string key, string name, string guid, ArbaParamType type, string[] categories, string description)
        {
            Key = key; Name = name; Guid = new Guid(guid); Type = type; Categories = categories; Description = description;
        }

        public override string ToString() => Name;
    }

    /// <summary>Prefijo de partición de un add-in: a quién pertenece, qué prefijos antiguos sustituye y qué códigos usa.</summary>
    internal sealed class ArbaPrefix
    {
        public readonly string Prefix;
        /// <summary>Valor de "ARBA - Origen" de los elementos creados por ese add-in.</summary>
        public readonly string Origin;
        public readonly string Repo;
        /// <summary>Prefijos de las particiones anteriores al contrato ("CC", "LOSA", "MC"...).</summary>
        public readonly string[] Legacy;
        /// <summary>Códigos (capas / familias) conocidos del add-in: sirven para separar marca y código al leer una partición.</summary>
        public readonly string[] Codes;

        public ArbaPrefix(string prefix, string origin, string repo, string[] legacy, string[] codes)
        {
            Prefix = prefix; Origin = origin; Repo = repo; Legacy = legacy; Codes = codes;
        }

        public override string ToString() => Prefix + " (" + Origin + ")";
    }

    /// <summary>Un botón de la cinta ARBA: nombre interno único, texto, panel y desplegable.</summary>
    internal sealed class ArbaButton
    {
        public readonly string Name, Text, Panel, Pulldown, Repo;
        public ArbaButton(string name, string text, string panel, string pulldown, string repo)
        {
            Name = name; Text = text; Panel = panel; Pulldown = pulldown; Repo = repo;
        }
    }

    /// <summary>
    /// El contrato ARBA en código: parámetros compartidos con GUID fijo, categorías y prefijos de partición,
    /// nombres de la cinta y valores de texto. Refleja contrato.json; los tests comprueban que coinciden.
    /// </summary>
    internal static class ArbaContract
    {
        public const string Version = "1.0.0";

        // ---------------------------------------------------------------- cinta
        public const string TabName = "ARBA";
        public const string PanelIa = "IA";
        public const string PanelAcero = "Acero";
        public const string PanelMetrados = "Metrados";
        public const string PanelEncofrado = "Encofrado";
        public const string PulldownAcero = "Acero";
        /// <summary>Orden estricto de los paneles de la pestaña ARBA.</summary>
        public static readonly string[] OrderedPanels = { PanelIa, PanelAcero, PanelMetrados, PanelEncofrado };

        public static readonly ArbaButton[] Botones =
        {
            new ArbaButton("ARBA_Acero_Zapatas",       "Zapatas",                      PanelAcero,    PulldownAcero, "Acero-Zapatas"),
            new ArbaButton("ARBA_Acero_Cimientos",     "Cimientos/\nSobrecimientos",   PanelAcero,    PulldownAcero, "Acero-cimientos-corridos"),
            new ArbaButton("ARBA_Acero_Bloques",       "Bloques con foso",             PanelAcero,    PulldownAcero, "Fosa_transformadores"),
            new ArbaButton("ARBA_Acero_Vigas",         "Vigas",                        PanelAcero,    PulldownAcero, "Acero-vigas"),
            new ArbaButton("ARBA_Acero_Columnas",      "Columnas",                     PanelAcero,    PulldownAcero, "Acero-columnas"),
            new ArbaButton("ARBA_Acero_Losas",         "Losas",                        PanelAcero,    PulldownAcero, "Acero-losas"),
            new ArbaButton("ARBA_Acero_Muro",          "Muro de contencion",           PanelAcero,    PulldownAcero, "Acero-automatico"),
            new ArbaButton("ARBA_Metrados_Exportar",   "Exportar a\nExcel",            PanelMetrados, "",            "Exportacion-metrados-excel"),
            new ArbaButton("ARBA_Metrados_Automatico", "Metrado\nautomático",          PanelMetrados, "",            "Exportacion-metrados-excel"),
            new ArbaButton("ARBA_Metrados_Particion",  "Asignar\npartición",           PanelMetrados, "",            "Exportacion-metrados-excel"),
            new ArbaButton("ARBA_Metrados_Migrar",     "Migrar\nparticiones y origen", PanelMetrados, "",            "Exportacion-metrados-excel"),
        };

        // ---------------------------------------------------------------- categorías de la partición
        public const string CatCimientos = "CIMIENTOS";
        public const string CatVigas = "VIGAS";
        public const string CatColumnas = "COLUMNAS";
        public const string CatLosas = "LOSAS";
        public const string CatMuros = "MUROS";
        /// <summary>Categoría de un anfitrión que no es cimentación, viga, columna, losa ni muro.</summary>
        public const string CatOtros = "OTROS";
        /// <summary>Grupo "Conexiones y anclajes" del plugin de metrados (solo en "Metrado - Elemento").</summary>
        public const string ElementoConexiones = "CONEXIONES";
        /// <summary>Grupo de los misceláneos con "Metrado - Partida" (rejillas, ángulos): su propia tabla, fuera de Vigas/Otros.</summary>
        public const string ElementoMiscelaneos = "MISCELANEOS";

        public static readonly string[] Categories = { CatCimientos, CatVigas, CatColumnas, CatLosas, CatMuros };

        /// <summary>Categoría de la partición por nombre de BuiltInCategory del anfitrión.</summary>
        public static readonly KeyValuePair<string, string[]>[] CategoryBuiltIns =
        {
            new KeyValuePair<string, string[]>(CatCimientos, new[] { "OST_StructuralFoundation" }),
            new KeyValuePair<string, string[]>(CatVigas,     new[] { "OST_StructuralFraming" }),
            new KeyValuePair<string, string[]>(CatColumnas,  new[] { "OST_StructuralColumns", "OST_Columns" }),
            new KeyValuePair<string, string[]>(CatLosas,     new[] { "OST_Floors" }),
            new KeyValuePair<string, string[]>(CatMuros,     new[] { "OST_Walls" }),
        };

        // ---------------------------------------------------------------- partición
        /// <summary>Plantilla por defecto de la partición. {codigo} es opcional (vacío = desaparece con su separador).</summary>
        public const string PartitionTemplate = "{categoria} - {prefijo}-{marca}-{codigo}";
        /// <summary>Separador entre la categoría y el resto (espacio, guion, espacio).</summary>
        public const string CategorySeparator = " - ";
        public const char FieldSeparator = '-';

        public static readonly ArbaPrefix[] Prefijos =
        {
            new ArbaPrefix("ZAP", "ZAPATAS",             "Acero-Zapatas",            new[] { "ZAP" },  new[] { "inferior", "inferior-sec", "superior", "superior-sec" }),
            new ArbaPrefix("CCO", "CIMIENTOS CORRIDOS",  "Acero-cimientos-corridos", new[] { "CC" },   new[] { "superior", "inferior", "estribo" }),
            new ArbaPrefix("BLQ", "BLOQUES",             "Fosa_transformadores",     new[] { "BLQ" },  new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" }),
            new ArbaPrefix("VIG", "VIGAS",               "Acero-vigas",              new[] { "VIG" },  new[] { "superior", "inferior", "estribo" }),
            new ArbaPrefix("COL", "COLUMNAS",            "Acero-columnas",           new[] { "COL" },  new string[0]),
            new ArbaPrefix("LOS", "LOSAS",               "Acero-losas",              new[] { "LOSA" }, new[] { "inferior", "inferior-sec", "superior", "superior-sec", "baston", "temperatura" }),
            new ArbaPrefix("MCO", "MUROS DE CONTENCION", "Acero-automatico",         new[] { "MC" },   new string[0]),
            new ArbaPrefix("MUR", "MUROS",               "(reservado: futuro add-in de placas / muros estructurales)", new string[0], new string[0]),
            new ArbaPrefix("MAN", "MANUAL",              "Exportacion-metrados-excel ('Asignar partición' en acero no creado por ARBA)", new string[0], new string[0]),
        };

        public static ArbaPrefix Zapatas => Prefijos[0];
        public static ArbaPrefix CimientosCorridos => Prefijos[1];
        public static ArbaPrefix Bloques => Prefijos[2];
        public static ArbaPrefix Vigas => Prefijos[3];
        public static ArbaPrefix Columnas => Prefijos[4];
        public static ArbaPrefix Losas => Prefijos[5];
        public static ArbaPrefix MurosContencion => Prefijos[6];
        public static ArbaPrefix Muros => Prefijos[7];
        public static ArbaPrefix Manual => Prefijos[8];

        /// <summary>Prefijo vigente por su texto ("ZAP"), sin distinguir mayúsculas; null si no existe.</summary>
        public static ArbaPrefix PrefixOf(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return null;
            string p = prefix.Trim();
            foreach (ArbaPrefix x in Prefijos)
                if (string.Equals(x.Prefix, p, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }

        /// <summary>Prefijo vigente que sustituye a un prefijo antiguo ("CC" -> CCO); null si no es antiguo.</summary>
        public static ArbaPrefix PrefixByLegacy(string legacy)
        {
            if (string.IsNullOrWhiteSpace(legacy)) return null;
            string p = legacy.Trim();
            foreach (ArbaPrefix x in Prefijos)
                foreach (string l in x.Legacy)
                    if (string.Equals(l, p, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }

        /// <summary>Prefijo por el valor de "ARBA - Origen" ("ZAPATAS" -> ZAP); null si no existe.</summary>
        public static ArbaPrefix PrefixByOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin)) return null;
            string o = origin.Trim();
            foreach (ArbaPrefix x in Prefijos)
                if (string.Equals(x.Origin, o, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }

        // ---------------------------------------------------------------- valores de texto
        public const string MaterialConcreto = "CONCRETO";
        public const string MaterialAceroEstructural = "ACERO ESTRUCTURAL";
        public const string MaterialMadera = "MADERA";
        public const string MaterialOtro = "OTRO";

        public const string PartidaRejillas = "ESTRUCTURAS METÁLICAS - REJILLAS";
        public const string PartidaAngulos = "ESTRUCTURAS METÁLICAS - ÁNGULOS";

        // ---------------------------------------------------------------- parámetros compartidos
        /// <summary>Grupo del archivo de parámetros compartidos temporal.</summary>
        public const string SharedFileGroup = "ARBA";

        private static readonly string[] CatRefuerzo = { "OST_Rebar", "OST_FabricReinforcement" };
        private static readonly string[] CatMiscelaneos = { "OST_StructuralFraming", "OST_StructuralColumns", "OST_GenericModel", "OST_StructConnections", "OST_StructuralStiffener", "OST_Roofs" };
        private static readonly string[] CatAnfitriones = { "OST_StructuralFraming", "OST_StructuralColumns", "OST_StructuralFoundation", "OST_Floors", "OST_Walls", "OST_StructuralStiffener", "OST_StructConnections", "OST_GenericModel", "OST_Roofs" };

        private static string[] Join(params string[][] sets)
        {
            var list = new List<string>();
            foreach (string[] s in sets)
                foreach (string c in s)
                    if (!list.Contains(c)) list.Add(c);
            return list.ToArray();
        }

        public static readonly ArbaParam Origen = new ArbaParam("Origen", "ARBA - Origen", "778D89FB-06FB-4455-B0CB-9F3CE40655F3",
            ArbaParamType.Text, Join(CatRefuerzo, CatMiscelaneos),
            "Add-in ARBA que creó el elemento: ZAPATAS, CIMIENTOS CORRIDOS, BLOQUES, VIGAS, COLUMNAS, LOSAS, MUROS DE CONTENCION, MUROS, MANUAL.");

        public static readonly ArbaParam Codigo = new ArbaParam("Codigo", "ARBA - Código", "AADB7B24-22B1-4D5B-8F24-E2E4468C16B3",
            ArbaParamType.Text, Join(CatRefuerzo, CatMiscelaneos),
            "Familia o capa propia del add-in dentro del anfitrión (F1, inferior, estribo, REJILLA P1...).");

        public static readonly ArbaParam Anfitrion = new ArbaParam("Anfitrion", "ARBA - Anfitrión", "F5CE04ED-FD80-4C5E-88EF-1EA46162E8C9",
            ArbaParamType.Text, CatMiscelaneos,
            "Id del elemento anfitrión (solo en elementos creados que no son armaduras: rejillas, ángulos).");

        public static readonly ArbaParam Partida = new ArbaParam("Partida", "Metrado - Partida", "379229AB-6C40-4CFC-81B7-10DF6468B842",
            ArbaParamType.Text, CatMiscelaneos,
            "Partida de metrado de los misceláneos (ESTRUCTURAS METÁLICAS - REJILLAS...).");

        public static readonly ArbaParam Material = new ArbaParam("Material", "Metrado - Material", "5B7E3C1A-2D4F-4A6B-9C8D-0E1F2A3B4C5D",
            ArbaParamType.Text, CatAnfitriones,
            "Clasificación para el metrado: CONCRETO, ACERO ESTRUCTURAL, MADERA u OTRO.");

        public static readonly ArbaParam Peso = new ArbaParam("Peso", "Metrado - Peso (kg)", "7D2A9F4E-6B1C-4C3D-8E5F-1A2B3C4D5E6F",
            ArbaParamType.Number, Join(CatRefuerzo, new[] { "OST_StructuralFraming", "OST_StructuralColumns", "OST_StructuralStiffener", "OST_StructConnections", "OST_GenericModel", "OST_Roofs" }),
            "Peso en kg. El plugin de metrados no sobrescribe un valor > 0 si ARBA - Origen no está vacío.");

        public static readonly ArbaParam Pernos = new ArbaParam("Pernos", "Metrado - Pernos (und)", "E63C6327-2A69-469F-A237-947AF6BF6AB7",
            ArbaParamType.Integer, new[] { "OST_StructuralFraming", "OST_StructuralColumns", "OST_GenericModel", "OST_StructConnections", "OST_StructuralStiffener" },
            "Pernos de anclaje o expansión del elemento (unidades).");

        public static readonly ArbaParam Elemento = new ArbaParam("Elemento", "Metrado - Elemento", "9A4C7E21-3B5D-4F8A-A6C2-2D3E4F5A6B7C",
            ArbaParamType.Text, Join(CatRefuerzo, CatAnfitriones),
            "Grupo de metrado: VIGAS, COLUMNAS, CIMIENTOS, LOSAS, MUROS, CONEXIONES, OTROS o MISCELANEOS (en el refuerzo, el del anfitrión).");

        public static readonly ArbaParam[] Parametros = { Origen, Codigo, Anfitrion, Partida, Material, Peso, Pernos, Elemento };

        /// <summary>Parámetro por su clave ("Origen") o por su nombre ("ARBA - Origen"); null si no existe.</summary>
        public static ArbaParam ParamOf(string keyOrName)
        {
            if (string.IsNullOrWhiteSpace(keyOrName)) return null;
            foreach (ArbaParam p in Parametros)
                if (string.Equals(p.Key, keyOrName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Name, keyOrName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
    }
}
