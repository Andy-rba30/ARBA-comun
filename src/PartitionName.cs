using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arba.Comun
{
    /// <summary>
    /// Expande la plantilla del parámetro Partición de las barras. Comodines (sin distinguir mayúsculas):
    /// {categoria}, {prefijo}, {marca} (Marca del anfitrión; si está vacía, su Id), {id}, {codigo} (familia o capa
    /// propia del add-in), {tipo}, {familia} (familia de Revit), {conjunto}. Alias: {cara}, {estribo} y {ala} valen
    /// {codigo}; {capa} vale Source.Layer si el add-in la rellena (Bloques: u / v) y si no {codigo}; {familiarevit}
    /// vale {familia}. Un comodín vacío se elimina con el separador que lo
    /// acompaña ("VIG-{marca}" con marca vacía da "VIG" y no "VIG-"; "{categoria} - VIG-V1" sin categoría da
    /// "VIG-V1"). El separador de categoría " - " se conserva tal cual.
    /// Unifica las siete copias de PartitionName de los add-ins (misma regla de limpieza que seis de ellas).
    /// </summary>
    internal static class PartitionName
    {
        public sealed class Source
        {
            public string Mark = "", Id = "", TypeName = "", FamilyName = "", SetName = "";
            /// <summary>Código propio del add-in: capa, cara, estribo, ala, familia F1..F8.</summary>
            public string Code = "";
            /// <summary>Subcapa opcional cuando el código ya es otra cosa (Bloques: u / v dentro de F1). {capa} la usa si está; si no, usa Code.</summary>
            public string Layer = "";
            public string Category = "", Prefix = "";
        }

        private static readonly Regex Wildcard = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);
        private static readonly Regex Runs = new Regex(@"([-_ /.]){2,}", RegexOptions.Compiled);
        private static readonly Regex Edges = new Regex(@"^[-_ /.]+|[-_ /.]+$", RegexOptions.Compiled);

        public static string Expand(string template, Source s)
        {
            if (string.IsNullOrWhiteSpace(template)) return "";
            if (s == null) s = new Source();
            string mark = string.IsNullOrWhiteSpace(s.Mark) ? (s.Id ?? "") : s.Mark.Trim();

            string expanded = Wildcard.Replace(template, m =>
            {
                switch (m.Groups[1].Value.ToLowerInvariant())
                {
                    case "categoria": return (s.Category ?? "").Trim();
                    case "prefijo": return (s.Prefix ?? "").Trim();
                    case "marca": return mark;
                    case "id": return (s.Id ?? "").Trim();
                    case "capa": return !string.IsNullOrWhiteSpace(s.Layer) ? s.Layer.Trim() : (s.Code ?? "").Trim();
                    case "codigo": case "cara": case "estribo": case "ala": return (s.Code ?? "").Trim();
                    case "tipo": return (s.TypeName ?? "").Trim();
                    case "familia": case "familiarevit": return (s.FamilyName ?? "").Trim();
                    case "conjunto": return (s.SetName ?? "").Trim();
                    default: return m.Value;
                }
            });

            // El separador de categoría se respeta: se limpia cada lado por separado y se descartan los vacíos.
            string[] parts = expanded.Split(new[] { ArbaContract.CategorySeparator }, StringSplitOptions.None);
            var kept = new List<string>();
            foreach (string part in parts)
            {
                string p = Edges.Replace(Runs.Replace(part, "$1"), "").Trim();
                if (p.Length > 0) kept.Add(p);
            }
            return string.Join(ArbaContract.CategorySeparator, kept);
        }

        /// <summary>Lista de comodines para la ayuda de la interfaz.</summary>
        public const string Help = "{categoria} (CIMIENTOS, VIGAS, COLUMNAS, LOSAS o MUROS, según el anfitrión), {prefijo} (el del add-in), " +
                                   "{marca} (Marca del anfitrión; si está vacía, el Id), {id}, {codigo} (capa / familia propia del add-in; " +
                                   "también {capa}, {cara}, {estribo}, {ala}), {tipo} (nombre del tipo), {familia} (familia de Revit) y " +
                                   "{conjunto} (nombre del juego de barras).";
    }
}
