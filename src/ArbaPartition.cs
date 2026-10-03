using System;
using System.Collections.Generic;
using System.Linq;

namespace Arba.Comun
{
    /// <summary>Forma de una partición leída.</summary>
    internal enum ArbaPartitionKind
    {
        /// <summary>No sigue ninguna forma conocida (texto del usuario).</summary>
        Unknown,
        /// <summary>Forma del contrato: "CATEGORIA - PREFIJO-marca[-codigo]".</summary>
        Contract,
        /// <summary>Partición de un add-in anterior al contrato, sin categoría: "ZAP-Z1", "CC-C1", "BLQ-FT-01-F1"...</summary>
        Legacy,
        /// <summary>Solo la categoría ("VIGAS"): lo que escribe el plugin de metrados en particiones vacías.</summary>
        CategoryOnly,
    }

    /// <summary>Resultado de leer una partición.</summary>
    internal sealed class ArbaPartitionInfo
    {
        public string Raw = "";
        public ArbaPartitionKind Kind = ArbaPartitionKind.Unknown;
        /// <summary>CIMIENTOS, VIGAS, COLUMNAS, LOSAS, MUROS u OTROS; "" si no la lleva.</summary>
        public string Category = "";
        /// <summary>Prefijo vigente (en una partición antigua, el que sustituye al antiguo).</summary>
        public string Prefix = "";
        /// <summary>Prefijo tal como aparece en el texto ("CC" en "CC-C1").</summary>
        public string PrefixInText = "";
        public string Mark = "";
        public string Code = "";
        public ArbaPrefix PrefixInfo;

        /// <summary>True si la partición la escribió un add-in ARBA (forma del contrato o antigua).</summary>
        public bool IsArba => Kind == ArbaPartitionKind.Contract || Kind == ArbaPartitionKind.Legacy;
        /// <summary>Valor de "ARBA - Origen" que corresponde al prefijo (null si no es ARBA).</summary>
        public string Origin => PrefixInfo?.Origin;

        public override string ToString() => Kind + ": [" + Category + "] " + Prefix + " marca=" + Mark + " codigo=" + Code;
    }

    /// <summary>
    /// Construye y lee particiones del contrato ("CATEGORIA - PREFIJO-marca-codigo") con tolerancia a las
    /// particiones antiguas ("ZAP-…", "BLQ-…"). Esta parte es pura (sin Revit); ArbaPartition.Revit.cs añade la
    /// lectura de la categoría del anfitrión y del parámetro Partición.
    /// </summary>
    internal static partial class ArbaPartition
    {
        /// <summary>
        /// Partición del contrato. <paramref name="category"/> es la del anfitrión (ver CategoryOf);
        /// <paramref name="mark"/> vacía usa <paramref name="id"/>; <paramref name="code"/> es opcional.
        /// Con <paramref name="template"/> nula se usa la plantilla por defecto del contrato.
        /// </summary>
        public static string Build(string category, string prefix, string mark, string id, string code = null, string template = null)
        {
            return PartitionName.Expand(string.IsNullOrWhiteSpace(template) ? ArbaContract.PartitionTemplate : template,
                new PartitionName.Source
                {
                    Category = Normalize(category), Prefix = (prefix ?? "").Trim().ToUpperInvariant(),
                    Mark = mark ?? "", Id = id ?? "", Code = code ?? ""
                });
        }

        public static string Build(ArbaPrefix prefix, string category, string mark, string id, string code = null, string template = null)
            => Build(category, prefix?.Prefix, mark, id, code, template);

        /// <summary>Partición del contrato con todos los comodines (tipo, familia, conjunto) para plantillas personalizadas.</summary>
        public static string Build(string template, PartitionName.Source source)
        {
            if (source == null) source = new PartitionName.Source();
            source.Category = Normalize(source.Category);
            source.Prefix = (source.Prefix ?? "").Trim().ToUpperInvariant();
            return PartitionName.Expand(string.IsNullOrWhiteSpace(template) ? ArbaContract.PartitionTemplate : template, source);
        }

        /// <summary>Texto de categoría normalizado (mayúsculas, sin espacios sobrantes); "" si es nulo.</summary>
        public static string Normalize(string category) => (category ?? "").Trim().ToUpperInvariant();

        /// <summary>True si el texto es una de las categorías del contrato (incluido OTROS).</summary>
        public static bool IsCategory(string text)
        {
            string t = Normalize(text);
            if (t == ArbaContract.CatOtros) return true;
            foreach (string c in ArbaContract.Categories) if (c == t) return true;
            return false;
        }

        /// <summary>Categoría de la partición para un nombre de BuiltInCategory ("OST_Floors" -> LOSAS); OTROS si no está en la tabla.</summary>
        public static string CategoryForBuiltIn(string builtInCategoryName)
        {
            if (string.IsNullOrWhiteSpace(builtInCategoryName)) return ArbaContract.CatOtros;
            foreach (KeyValuePair<string, string[]> kv in ArbaContract.CategoryBuiltIns)
                foreach (string ost in kv.Value)
                    if (string.Equals(ost, builtInCategoryName.Trim(), StringComparison.OrdinalIgnoreCase)) return kv.Key;
            return ArbaContract.CatOtros;
        }

        /// <summary>
        /// Lee una partición. Reconoce la forma del contrato, las antiguas (prefijo sin categoría) y las de solo
        /// categoría. El código se separa de la marca solo cuando coincide con un código conocido del prefijo (o con
        /// uno de <paramref name="knownCodes"/>), porque la marca puede llevar guiones ("FT-01").
        /// </summary>
        public static ArbaPartitionInfo Parse(string text, IEnumerable<string> knownCodes = null)
        {
            var info = new ArbaPartitionInfo { Raw = text ?? "" };
            string t = (text ?? "").Trim();
            if (t.Length == 0) return info;

            string body = t;
            int sep = t.IndexOf(ArbaContract.CategorySeparator, StringComparison.Ordinal);
            if (sep > 0)
            {
                string left = Normalize(t.Substring(0, sep));
                if (IsCategory(left))
                {
                    info.Category = left;
                    body = t.Substring(sep + ArbaContract.CategorySeparator.Length).Trim();
                }
            }
            if (info.Category.Length == 0 && IsCategory(t))
            {
                info.Category = Normalize(t);
                info.Kind = ArbaPartitionKind.CategoryOnly;
                return info;
            }
            if (info.Category.Length > 0 && body.Length == 0)
            {
                info.Kind = ArbaPartitionKind.CategoryOnly;
                return info;
            }

            int dash = body.IndexOf(ArbaContract.FieldSeparator);
            string first = (dash < 0 ? body : body.Substring(0, dash)).Trim();
            string rest = dash < 0 ? "" : body.Substring(dash + 1).Trim();

            ArbaPrefix p = ArbaContract.PrefixOf(first);
            if (p == null) p = ArbaContract.PrefixByLegacy(first);
            if (p == null) return info;   // Unknown (con la categoría, si la llevaba)

            info.PrefixInfo = p;
            info.Prefix = p.Prefix;
            info.PrefixInText = first;
            info.Kind = info.Category.Length > 0 ? ArbaPartitionKind.Contract : ArbaPartitionKind.Legacy;

            var codes = new List<string>(p.Codes);
            if (knownCodes != null) codes.AddRange(knownCodes.Where(c => !string.IsNullOrWhiteSpace(c)));
            foreach (string code in codes.OrderByDescending(c => c.Length))
            {
                string suffix = ArbaContract.FieldSeparator + code;
                if (rest.Length > suffix.Length && rest.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    info.Mark = rest.Substring(0, rest.Length - suffix.Length).Trim();
                    info.Code = rest.Substring(rest.Length - code.Length);
                    return info;
                }
            }
            info.Mark = rest;
            return info;
        }

        /// <summary>Partición del contrato equivalente a una antigua (o a una del contrato con otro prefijo), con la categoría dada.</summary>
        public static string Upgrade(ArbaPartitionInfo info, string category, string template = null)
        {
            if (info == null || !info.IsArba) return info?.Raw ?? "";
            return Build(category, info.Prefix, info.Mark, "", info.Code, template);
        }

        /// <summary>True si la plantilla empieza por "{categoria} - {prefijo}-", que es lo que exige el contrato.</summary>
        public static bool TemplateFollowsContract(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return false;
            string t = template.Trim().ToLowerInvariant().Replace(" ", "");
            return t.StartsWith("{categoria}-{prefijo}-", StringComparison.Ordinal);
        }

        /// <summary>Prefijo "CATEGORIA - PREFIJO-" con el que empiezan las particiones de un add-in en una categoría (para filtros "empieza por").</summary>
        public static string FilterPrefix(string category, string prefix)
            => Normalize(category) + ArbaContract.CategorySeparator + (prefix ?? "").Trim().ToUpperInvariant() + ArbaContract.FieldSeparator;

        /// <summary>Prefijo "CATEGORIA - " de todas las particiones del contrato en una categoría.</summary>
        public static string FilterPrefix(string category) => Normalize(category) + ArbaContract.CategorySeparator;
    }
}
