using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Arba.Comun.Tests
{
    /// <summary>Pruebas de consola de las clases puras y de la coherencia contrato.json ↔ ArbaContract. Imprime OK/FALLO y termina con 1 si algo falla.</summary>
    internal static class Program
    {
        private static int _fail, _ok;

        private static void Check(bool cond, string what)
        {
            if (cond) { _ok++; Console.WriteLine("  OK    " + what); }
            else { _fail++; Console.WriteLine("  FALLO " + what); }
        }

        private static void Eq(string actual, string expected, string what) => Check(actual == expected, what + ": \"" + actual + "\"" + (actual == expected ? "" : " (esperado \"" + expected + "\")"));

        private static int Main()
        {
            Console.WriteLine("== Contrato en código: GUID y nombres únicos ==");
            Contract();
            Console.WriteLine("== contrato.json ↔ ArbaContract ==");
            Json();
            Console.WriteLine("== ArbaPartition.Build ==");
            Build();
            Console.WriteLine("== ArbaPartition.Parse ==");
            Parse();
            Console.WriteLine("== ArbaPartition.Upgrade y plantillas ==");
            Upgrade();
            Console.WriteLine("== PartitionName (compatibilidad con las plantillas de los add-ins) ==");
            Templates();
            Console.WriteLine("== NameMatch ==");
            Names();
            Console.WriteLine();
            Console.WriteLine(_ok + " comprobaciones correctas, " + _fail + " fallos");
            return _fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------
        private static void Contract()
        {
            var guids = ArbaContract.Parametros.Select(p => p.Guid).ToList();
            Check(guids.Distinct().Count() == guids.Count, "los " + guids.Count + " GUID son distintos");
            Check(guids.All(g => g != Guid.Empty), "ningún GUID vacío");
            var names = ArbaContract.Parametros.Select(p => p.Name).ToList();
            Check(names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count, "nombres de parámetro únicos");
            var keys = ArbaContract.Parametros.Select(p => p.Key).ToList();
            Check(keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == keys.Count, "claves de parámetro únicas");
            Check(ArbaContract.Parametros.All(p => p.Categories.Length > 0 && p.Categories.All(c => c.StartsWith("OST_"))), "todas las categorías son OST_*");
            Check(ArbaContract.Parametros.All(p => p.Categories.Distinct().Count() == p.Categories.Length), "sin categorías repetidas en un parámetro");

            var prefixes = ArbaContract.Prefijos.Select(p => p.Prefix).ToList();
            Check(prefixes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == prefixes.Count, "prefijos únicos");
            Check(prefixes.All(p => p.Length == 3 && p == p.ToUpperInvariant()), "prefijos de tres letras mayúsculas");
            var origins = ArbaContract.Prefijos.Select(p => p.Origin).ToList();
            Check(origins.Distinct(StringComparer.OrdinalIgnoreCase).Count() == origins.Count, "orígenes únicos");
            var legacy = ArbaContract.Prefijos.SelectMany(p => p.Legacy).ToList();
            Check(legacy.Distinct(StringComparer.OrdinalIgnoreCase).Count() == legacy.Count, "prefijos antiguos únicos");
            Check(legacy.All(l => ArbaContract.PrefixOf(l) == null || ArbaContract.PrefixByLegacy(l).Prefix == l),
                  "un prefijo antiguo igual al vigente solo apunta a sí mismo");

            var buttons = ArbaContract.Botones.Select(b => b.Name).ToList();
            Check(buttons.Distinct(StringComparer.OrdinalIgnoreCase).Count() == buttons.Count, "nombres internos de botón únicos");
            Check(buttons.All(b => b.StartsWith("ARBA_")), "nombres internos con prefijo ARBA_");
            Check(ArbaContract.Botones.All(b => ArbaContract.OrderedPanels.Contains(b.Panel)), "todos los botones van a un panel de la pestaña");
            Check(ArbaContract.OrderedPanels.SequenceEqual(new[] { "IA", "Acero", "Metrados", "Encofrado" }), "orden de paneles IA, Acero, Metrados, Encofrado");

            Check(ArbaContract.ParamOf("ARBA - Origen") == ArbaContract.Origen && ArbaContract.ParamOf("peso") == ArbaContract.Peso, "ParamOf por nombre y por clave");
            Check(ArbaContract.PrefixOf("zap") == ArbaContract.Zapatas && ArbaContract.PrefixByLegacy("cc") == ArbaContract.CimientosCorridos, "PrefixOf / PrefixByLegacy sin distinguir mayúsculas");
            Check(ArbaContract.PrefixByOrigin("Bloques") == ArbaContract.Bloques && ArbaContract.PrefixByOrigin("nada") == null, "PrefixByOrigin");
            Check(ArbaPartition.TemplateFollowsContract(ArbaContract.PartitionTemplate), "la plantilla por defecto cumple el contrato");
        }

        // ------------------------------------------------------------------
        private static string FindJson()
        {
            foreach (string dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.Combine(Directory.GetCurrentDirectory(), "..") })
            {
                string p = Path.GetFullPath(Path.Combine(dir, "contrato.json"));
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static string[] Strings(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()).ToArray();

        private static bool SameSet(IEnumerable<string> a, IEnumerable<string> b) =>
            new HashSet<string>(a, StringComparer.OrdinalIgnoreCase).SetEquals(b);

        private static void Json()
        {
            string path = FindJson();
            Check(path != null, "contrato.json encontrado" + (path != null ? " en " + path : ""));
            if (path == null) return;
            JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;

            Eq(root.GetProperty("version").GetString(), ArbaContract.Version, "version");

            // parámetros
            var jsonParams = root.GetProperty("parametros").EnumerateArray().ToList();
            Check(jsonParams.Count == ArbaContract.Parametros.Length, "mismo número de parámetros (" + jsonParams.Count + ")");
            foreach (JsonElement jp in jsonParams)
            {
                string key = jp.GetProperty("clave").GetString();
                ArbaParam p = ArbaContract.ParamOf(key);
                Check(p != null, "parámetro \"" + key + "\" existe en el código");
                if (p == null) continue;
                Eq(jp.GetProperty("nombre").GetString(), p.Name, key + ".nombre");
                Check(string.Equals(jp.GetProperty("guid").GetString(), p.Guid.ToString(), StringComparison.OrdinalIgnoreCase), key + ".guid = " + p.Guid.ToString().ToUpperInvariant());
                string tipo = jp.GetProperty("tipo").GetString();
                ArbaParamType t = tipo == "texto" ? ArbaParamType.Text : tipo == "numero" ? ArbaParamType.Number : ArbaParamType.Integer;
                Check(t == p.Type, key + ".tipo = " + tipo);
                Check(jp.GetProperty("ejemplar").GetBoolean(), key + " es de ejemplar");
                Check(SameSet(Strings(jp.GetProperty("categorias")), p.Categories), key + ".categorias (" + p.Categories.Length + ")");
            }
            var jsonGuids = jsonParams.Select(j => j.GetProperty("guid").GetString().ToUpperInvariant()).ToList();
            Check(jsonGuids.Distinct().Count() == jsonGuids.Count, "GUID únicos en el json");
            Check(jsonGuids.All(g => Guid.TryParse(g, out _)), "GUID válidos en el json");

            // partición
            JsonElement part = root.GetProperty("particion");
            Eq(part.GetProperty("plantilla").GetString(), ArbaContract.PartitionTemplate, "particion.plantilla");
            Eq(part.GetProperty("separadorCategoria").GetString(), ArbaContract.CategorySeparator, "particion.separadorCategoria");
            Eq(part.GetProperty("separador").GetString(), ArbaContract.FieldSeparator.ToString(), "particion.separador");
            Eq(part.GetProperty("categoriaPorDefecto").GetString(), ArbaContract.CatOtros, "particion.categoriaPorDefecto");
            var cats = part.GetProperty("categorias").EnumerateObject().ToList();
            Check(SameSet(cats.Select(c => c.Name), ArbaContract.Categories), "categorías de la partición");
            foreach (JsonProperty c in cats)
            {
                var code = ArbaContract.CategoryBuiltIns.FirstOrDefault(k => k.Key == c.Name);
                Check(code.Key != null && SameSet(Strings(c.Value), code.Value), "categoría " + c.Name + " → " + string.Join(", ", Strings(c.Value)));
            }
            var jsonPrefixes = part.GetProperty("prefijos").EnumerateArray().ToList();
            Check(jsonPrefixes.Count == ArbaContract.Prefijos.Length, "mismo número de prefijos (" + jsonPrefixes.Count + ")");
            foreach (JsonElement jp in jsonPrefixes)
            {
                string pre = jp.GetProperty("prefijo").GetString();
                ArbaPrefix p = ArbaContract.PrefixOf(pre);
                Check(p != null, "prefijo " + pre + " existe en el código");
                if (p == null) continue;
                Eq(jp.GetProperty("origen").GetString(), p.Origin, pre + ".origen");
                Check(SameSet(Strings(jp.GetProperty("antiguos")), p.Legacy), pre + ".antiguos = [" + string.Join(", ", p.Legacy) + "]");
                Check(SameSet(Strings(jp.GetProperty("codigos")), p.Codes), pre + ".codigos (" + p.Codes.Length + ")");
                string marcaVacia = jp.TryGetProperty("marcaVacia", out JsonElement mv) ? mv.GetString() : "id";
                Check((marcaVacia == "tipo") == (p.MarkFallback == ArbaMarkFallback.TypeName), pre + ".marcaVacia = " + marcaVacia);
            }
            var alias = part.GetProperty("alias").EnumerateObject().ToList();
            foreach (JsonProperty a in alias)
            {
                string t1 = "X-" + a.Name + "-Y", t2 = "X-" + a.Value.GetString() + "-Y";
                var src = new PartitionName.Source { Code = "c", FamilyName = "f" };
                Eq(PartitionName.Expand(t1, src), PartitionName.Expand(t2, src), "alias " + a.Name + " = " + a.Value.GetString());
            }

            // cinta
            JsonElement cinta = root.GetProperty("cinta");
            Eq(cinta.GetProperty("pestana").GetString(), ArbaContract.TabName, "cinta.pestana");
            Check(Strings(cinta.GetProperty("paneles")).SequenceEqual(ArbaContract.OrderedPanels), "cinta.paneles en el mismo orden");
            var desplegables = cinta.GetProperty("desplegables").EnumerateArray().ToList();
            Check(desplegables.Count == 1 && desplegables[0].GetProperty("nombre").GetString() == ArbaContract.PulldownAcero &&
                  desplegables[0].GetProperty("panel").GetString() == ArbaContract.PanelAcero, "desplegable Acero en el panel Acero");
            var jsonButtons = cinta.GetProperty("botones").EnumerateArray().ToList();
            Check(jsonButtons.Count == ArbaContract.Botones.Length, "mismo número de botones (" + jsonButtons.Count + ")");
            foreach (JsonElement jb in jsonButtons)
            {
                string name = jb.GetProperty("nombre").GetString();
                ArbaButton b = ArbaContract.Botones.FirstOrDefault(x => x.Name == name);
                Check(b != null, "botón " + name + " existe en el código");
                if (b == null) continue;
                Eq(jb.GetProperty("texto").GetString(), b.Text, name + ".texto");
                Eq(jb.GetProperty("panel").GetString(), b.Panel, name + ".panel");
                Eq(jb.GetProperty("desplegable").GetString(), b.Pulldown, name + ".desplegable");
            }
        }

        // ------------------------------------------------------------------
        private static void Build()
        {
            Eq(ArbaPartition.Build("CIMIENTOS", "ZAP", "Z1", "100"), "CIMIENTOS - ZAP-Z1", "zapata con marca, sin código");
            Eq(ArbaPartition.Build("CIMIENTOS", "ZAP", "", "123456"), "CIMIENTOS - ZAP-123456", "marca vacía usa el Id");
            Eq(ArbaPartition.Build("CIMIENTOS", "BLQ", "FT-01", "1", "F4"), "CIMIENTOS - BLQ-FT-01-F4", "bloque con marca con guion y código");
            Eq(ArbaPartition.Build(ArbaContract.Vigas, "vigas", " V-101 ", "7"), "VIGAS - VIG-V-101", "categoría en minúsculas y marca con espacios");
            Eq(ArbaPartition.Build("LOSAS", "LOS", "L2", "9", "baston"), "LOSAS - LOS-L2-baston", "losa con código");
            Eq(ArbaPartition.Build("", "VIG", "V1", "1"), "VIG-V1", "sin categoría no deja el separador huérfano");
            Eq(ArbaPartition.Build("OTROS", "CCO", "C1", "1"), "OTROS - CCO-C1", "categoría OTROS");
            Eq(ArbaPartition.Build("VIGAS", "VIG", "V1", "1", "", "{categoria} - {prefijo}-{marca}"), "VIGAS - VIG-V1", "plantilla sin código");
            Eq(ArbaPartition.Build("VIGAS", "VIG", "V1", "1", "sup", "{categoria} - {prefijo}-{marca}-{codigo}-{conjunto}"), "VIGAS - VIG-V1-sup", "comodín vacío al final");
            Eq(ArbaPartition.Build("{categoria} - {prefijo}-{marca}-{codigo} ({tipo})", new PartitionName.Source
                { Category = "columnas", Prefix = "col", Mark = "C3", Id = "1", Code = "", TypeName = "30x60" }), "COLUMNAS - COL-C3 (30x60)", "plantilla personalizada con tipo");
            Eq(ArbaPartition.EffectiveMark(ArbaContract.Zapatas, "Z1", "100", "Z1_1.5x1.5m"), "Z1", "marca efectiva: la marca manda");
            Eq(ArbaPartition.EffectiveMark(ArbaContract.Zapatas, "", "100", "Z1_1.5x1.5m"), "100", "ZAP sin marca usa el Id");
            Eq(ArbaPartition.EffectiveMark(ArbaContract.Manual, "", "100", "Z1_1.5x1.5m"), "Z1_1.5x1.5m", "MAN sin marca usa el nombre del tipo");
            Eq(ArbaPartition.EffectiveMark(ArbaContract.Manual, "", "100", ""), "", "MAN sin marca ni tipo: nada");
            Eq(ArbaPartition.Build("CIMIENTOS", "MAN", ArbaPartition.EffectiveMark(ArbaContract.Manual, "", "100", ""), ""), "CIMIENTOS - MAN", "partición MAN sin marca ni tipo");
            Eq(ArbaPartition.Build("CIMIENTOS", "MAN", "Zapata - 1.5", ""), "CIMIENTOS - MAN-Zapata - 1.5", "nombre de tipo con ' - ' se conserva");
            Check(ArbaPartition.Parse("CIMIENTOS - MAN-Zapata - 1.5").Mark == "Zapata - 1.5", "y se lee como marca entera");
            Check(ArbaContract.Prefijos.All(x => x == ArbaContract.Manual ? x.MarkFallback == ArbaMarkFallback.TypeName : x.MarkFallback == ArbaMarkFallback.Id), "solo MAN usa el tipo como respaldo de la marca");
            Eq(ArbaPartition.FilterPrefix("vigas"), "VIGAS - ", "FilterPrefix(categoría)");
            Eq(ArbaPartition.FilterPrefix("Vigas", "vig"), "VIGAS - VIG-", "FilterPrefix(categoría, prefijo)");
            Eq(ArbaPartition.CategoryForBuiltIn("OST_Floors"), "LOSAS", "OST_Floors → LOSAS");
            Eq(ArbaPartition.CategoryForBuiltIn("OST_StructuralFoundation"), "CIMIENTOS", "OST_StructuralFoundation → CIMIENTOS");
            Eq(ArbaPartition.CategoryForBuiltIn("OST_Columns"), "COLUMNAS", "OST_Columns → COLUMNAS");
            Eq(ArbaPartition.CategoryForBuiltIn("OST_GenericModel"), "OTROS", "categoría fuera de la tabla → OTROS");
            Eq(ArbaPartition.CategoryForBuiltIn(""), "OTROS", "sin categoría → OTROS");
        }

        private static void Parse()
        {
            ArbaPartitionInfo i = ArbaPartition.Parse("CIMIENTOS - ZAP-Z1");
            Check(i.Kind == ArbaPartitionKind.Contract && i.Category == "CIMIENTOS" && i.Prefix == "ZAP" && i.Mark == "Z1" && i.Code == "", "contrato: " + i);
            Check(i.IsArba && i.Origin == "ZAPATAS" && i.PrefixInfo == ArbaContract.Zapatas, "origen ZAPATAS");

            i = ArbaPartition.Parse("CIMIENTOS - BLQ-FT-01-F4");
            Check(i.Kind == ArbaPartitionKind.Contract && i.Mark == "FT-01" && i.Code == "F4", "marca con guion y código conocido: " + i);

            i = ArbaPartition.Parse("CIMIENTOS - ZAP-Z1-inferior-sec");
            Check(i.Mark == "Z1" && i.Code == "inferior-sec", "código con guion (el más largo primero): " + i);

            i = ArbaPartition.Parse("CIMIENTOS - ZAP-Z1-INFERIOR");
            Check(i.Mark == "Z1" && i.Code == "INFERIOR", "código sin distinguir mayúsculas: " + i);

            i = ArbaPartition.Parse("VIGAS - VIG-V-101-x");
            Check(i.Mark == "V-101-x" && i.Code == "", "sufijo desconocido se queda en la marca: " + i);

            i = ArbaPartition.Parse("VIGAS - VIG-V-101-x", new[] { "x" });
            Check(i.Mark == "V-101" && i.Code == "x", "código conocido pasado por el add-in: " + i);

            i = ArbaPartition.Parse("ZAP-Z-01");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Category == "" && i.Prefix == "ZAP" && i.Mark == "Z-01", "antigua ZAP: " + i);
            i = ArbaPartition.Parse("CC-C1");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Prefix == "CCO" && i.PrefixInText == "CC" && i.Mark == "C1", "antigua CC → CCO: " + i);
            i = ArbaPartition.Parse("LOSA-L1");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Prefix == "LOS" && i.Mark == "L1", "antigua LOSA → LOS: " + i);
            i = ArbaPartition.Parse("MC-M1");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Prefix == "MCO" && i.Origin == "MUROS DE CONTENCION", "antigua MC → MCO: " + i);
            i = ArbaPartition.Parse("BLQ-FT-01-F1");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Mark == "FT-01" && i.Code == "F1", "antigua BLQ con familia: " + i);
            i = ArbaPartition.Parse("BLQ-1234");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Mark == "1234" && i.Code == "", "antigua BLQ con id: " + i);
            i = ArbaPartition.Parse("col-c7");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Prefix == "COL" && i.Mark == "c7", "prefijo en minúsculas: " + i);

            i = ArbaPartition.Parse("VIGAS");
            Check(i.Kind == ArbaPartitionKind.CategoryOnly && i.Category == "VIGAS" && !i.IsArba, "solo categoría: " + i);
            i = ArbaPartition.Parse("  cimientos ");
            Check(i.Kind == ArbaPartitionKind.CategoryOnly && i.Category == "CIMIENTOS", "solo categoría con espacios y minúsculas: " + i);
            i = ArbaPartition.Parse("OTROS - ");
            Check(i.Kind == ArbaPartitionKind.CategoryOnly && i.Category == "OTROS", "categoría con separador y nada más: " + i);

            i = ArbaPartition.Parse("Muro de contención");
            Check(i.Kind == ArbaPartitionKind.Unknown && !i.IsArba && i.Origin == null, "texto del usuario: Unknown");
            i = ArbaPartition.Parse("VIGAS - ABC-1");
            Check(i.Kind == ArbaPartitionKind.Unknown && i.Category == "VIGAS", "categoría con prefijo desconocido: Unknown con categoría");
            i = ArbaPartition.Parse("");
            Check(i.Kind == ArbaPartitionKind.Unknown, "vacía: Unknown");
            i = ArbaPartition.Parse(null);
            Check(i.Kind == ArbaPartitionKind.Unknown && i.Raw == "", "nula: Unknown");
            i = ArbaPartition.Parse("ZAP");
            Check(i.Kind == ArbaPartitionKind.Legacy && i.Mark == "", "solo el prefijo: marca vacía");
            i = ArbaPartition.Parse("VIGAS - MAN-V7");
            Check(i.Kind == ArbaPartitionKind.Contract && i.Origin == "MANUAL", "prefijo MAN del plugin de metrados");
        }

        private static void Upgrade()
        {
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("ZAP-Z-01"), "CIMIENTOS"), "CIMIENTOS - ZAP-Z-01", "ZAP-Z-01 → contrato");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("CC-C1"), "MUROS"), "MUROS - CCO-C1", "CC-C1 en muro → MUROS - CCO-C1");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("BLQ-FT-01-F1"), "CIMIENTOS"), "CIMIENTOS - BLQ-FT-01-F1", "BLQ conserva la familia");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("LOSA-L1"), "LOSAS"), "LOSAS - LOS-L1", "LOSA → LOS");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("MC-M1"), "CIMIENTOS"), "CIMIENTOS - MCO-M1", "MC → MCO con la categoría real del anfitrión");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("CIMIENTOS - ZAP-Z1"), "CIMIENTOS"), "CIMIENTOS - ZAP-Z1", "ya conforme: igual");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("VIGAS"), "VIGAS"), "VIGAS", "solo categoría: no se toca");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("Bloque A"), "OTROS"), "Bloque A", "desconocida: no se toca");
            Eq(ArbaPartition.Upgrade(ArbaPartition.Parse("ZAP-Z1"), "CIMIENTOS", "{categoria} - {prefijo}-{marca}-{codigo}"), "CIMIENTOS - ZAP-Z1", "con plantilla explícita");

            Check(ArbaPartition.TemplateFollowsContract("{categoria} - {prefijo}-{marca}"), "plantilla corta cumple");
            Check(ArbaPartition.TemplateFollowsContract("{CATEGORIA} - {Prefijo}-{marca}-{familia}"), "mayúsculas indiferentes");
            Check(!ArbaPartition.TemplateFollowsContract("ZAP-{marca}"), "plantilla antigua no cumple");
            Check(!ArbaPartition.TemplateFollowsContract("{prefijo}-{marca}"), "sin categoría no cumple");
            Check(!ArbaPartition.TemplateFollowsContract(""), "vacía no cumple");
        }

        // ------------------------------------------------------------------
        private static void Templates()
        {
            // los mismos casos que prueban hoy Acero-Zapatas, Acero-losas y Fosa_transformadores
            Eq(PartitionName.Expand("ZAP-{marca}-{capa}", new PartitionName.Source { Mark = "Z-01", Code = "inferior" }), "ZAP-Z-01-inferior", "zapatas: marca y capa");
            Eq(PartitionName.Expand("ZAP-{marca}", new PartitionName.Source { Mark = "", Id = "1234" }), "ZAP-1234", "zapatas: sin marca usa el id");
            Eq(PartitionName.Expand("ZAP-{conjunto}", new PartitionName.Source()), "ZAP", "zapatas: comodín vacío sin separador huérfano");
            Eq(PartitionName.Expand("LOSA-{marca}-{capa}", new PartitionName.Source { Mark = "L-2", Code = "baston" }), "LOSA-L-2-baston", "losas: marca y capa");
            Eq(PartitionName.Expand("BLQ-{marca}-{codigo}", new PartitionName.Source { Mark = "FT-01", Code = "F4" }), "BLQ-FT-01-F4", "bloques: marca y familia (ahora {codigo})");
            Eq(PartitionName.Expand("BLQ-{marca}-{codigo}", new PartitionName.Source { Mark = "", Id = "1234", Code = "F1" }), "BLQ-1234-F1", "bloques: sin marca usa el id");
            Eq(PartitionName.Expand("BLQ-{marca}-{codigo}", new PartitionName.Source { Mark = "FT-01" }), "BLQ-FT-01", "bloques: comodín vacío");
            Eq(PartitionName.Expand("{codigo}/{conjunto}", new PartitionName.Source { Code = "F7", SetName = "murete 1 tramo 2" }), "F7/murete 1 tramo 2", "familia y conjunto");
            Eq(PartitionName.Expand("VIG-{marca}-{cara}", new PartitionName.Source { Mark = "V1", Code = "estribo" }), "VIG-V1-estribo", "vigas: {cara} es alias de {codigo}");
            Eq(PartitionName.Expand("COL-{marca}-{estribo}", new PartitionName.Source { Mark = "C1", Code = "2" }), "COL-C1-2", "columnas: {estribo} es alias de {codigo}");
            Eq(PartitionName.Expand("MC-{marca}-{ala}", new PartitionName.Source { Mark = "M1", Code = "ala A" }), "MC-M1-ala A", "muros: {ala} es alias de {codigo}");
            Eq(PartitionName.Expand("{familia}|{familiarevit}", new PartitionName.Source { FamilyName = "Zapata" }), "Zapata|Zapata", "{familia} y {familiarevit} son la familia de Revit");
            Eq(PartitionName.Expand("{tipo} {marca}", new PartitionName.Source { TypeName = "30x60", Mark = "V1" }), "30x60 V1", "tipo y marca con espacio");
            Eq(PartitionName.Expand("{MARCA}", new PartitionName.Source { Mark = "x" }), "x", "comodín en mayúsculas");
            Eq(PartitionName.Expand("{desconocido}-{marca}", new PartitionName.Source { Mark = "x" }), "{desconocido}-x", "comodín desconocido se conserva");
            Eq(PartitionName.Expand("", new PartitionName.Source { Mark = "x" }), "", "plantilla vacía da vacío");
            Eq(PartitionName.Expand("{categoria} - {prefijo}-{marca}", new PartitionName.Source { Category = "VIGAS", Prefix = "VIG", Mark = "V1" }), "VIGAS - VIG-V1", "separador de categoría intacto");
            Eq(PartitionName.Expand("{categoria} - {prefijo}-{marca}", new PartitionName.Source { Prefix = "VIG", Mark = "V1" }), "VIG-V1", "sin categoría desaparece el separador");
            Eq(PartitionName.Expand("{categoria} - {prefijo}-{marca}", new PartitionName.Source { Category = "VIGAS" }), "VIGAS", "solo categoría");
            Eq(PartitionName.Expand("A - B - {marca}", new PartitionName.Source()), "A - B", "varios separadores de categoría se respetan");
            Eq(PartitionName.Expand("{marca}", null), "", "fuente nula");
            Check(PartitionName.Help.Contains("{categoria}") && PartitionName.Help.Contains("{codigo}"), "la ayuda menciona los comodines nuevos");
        }

        private static void Names()
        {
            var names = new[] { "5/8\"", "Ø 5/8\"", "16M", "3/8\"", "Ø 3/8\"", "10M" };
            Eq(NameMatch.Unique(names, "5/8\""), "5/8\"", "exacto gana aunque otros lo contengan");
            Eq(NameMatch.Unique(names, "ø 5/8\""), "Ø 5/8\"", "exacto sin distinguir mayúsculas");
            List<string> amb = NameMatch.Candidates(names, "5/8");
            Check(NameMatch.IsAmbiguous(names, "5/8") && amb.Count == 2 && amb[0] == "5/8\"" && amb[1] == "Ø 5/8\"", "fragmento ambiguo: " + string.Join(", ", amb));
            Check(NameMatch.Unique(names, "3/8") == null && NameMatch.Candidates(names, "3/8").Count == 2, "3/8 ambiguo: 2 candidatos");
            Eq(NameMatch.Unique(names, "16"), "16M", "fragmento único");
            Check(NameMatch.Unique(names, "1/2") == null && NameMatch.Candidates(names, "1/2").Count == 0, "sin coincidencia");
            Check(NameMatch.Unique(names, "") == null && NameMatch.Unique(names, null) == null && NameMatch.Unique(null, "x") == null, "nombre o lista vacíos");
            Eq(NameMatch.First(names, "5/8"), "5/8\"", "First: regla antigua, el primero que coincide");
            Eq(NameMatch.First(names, "ø 5/8\""), "Ø 5/8\"", "First: exacto primero");
            Check(NameMatch.First(names, "1/2") == null, "First: sin coincidencia");
        }
    }
}
