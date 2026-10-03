using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace Arba.Comun
{
    /// <summary>Resultado de una migración de particiones y origen.</summary>
    internal sealed class ArbaMigrationResult
    {
        public int Revisadas, Migradas, YaConformes, SoloCategoria, Desconocidas, SinAnfitrion;
        public int ParticionesCambiadas, OrigenEscrito, ElementoCorregido;
        public readonly Dictionary<string, int> PorPrefijo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Avisos = new List<string>();

        public string Resumen()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Armaduras revisadas: " + Revisadas);
            sb.AppendLine("Migradas (partición nueva y/u origen escrito): " + Migradas);
            sb.AppendLine("  particiones reescritas: " + ParticionesCambiadas + ", orígenes escritos: " + OrigenEscrito + ", \"Metrado - Elemento\" corregidos: " + ElementoCorregido);
            sb.AppendLine("Ya conformes con el contrato: " + YaConformes);
            sb.AppendLine("Solo categoría (del plugin de metrados), sin tocar: " + SoloCategoria);
            sb.AppendLine("Sin forma reconocida, sin tocar: " + Desconocidas);
            if (SinAnfitrion > 0) sb.AppendLine("Sin anfitrión (categoría tomada de la partición u OTROS): " + SinAnfitrion);
            if (PorPrefijo.Count > 0)
                sb.AppendLine("Por add-in: " + string.Join(", ", PorPrefijo.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
            if (Avisos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Avisos:");
                foreach (string a in Avisos) sb.AppendLine("  • " + a);
            }
            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Migra modelos existentes al contrato SIN rearmar: convierte las particiones antiguas ("ZAP-Z1" →
    /// "CIMIENTOS - ZAP-Z1", "BLQ-FT-01-F1" → "CIMIENTOS - BLQ-FT-01-F1", "LOSA-L1" → "LOSAS - LOS-L1"...) y las
    /// de 1.0.3 con la categoría del anfitrión en vez de la fija del prefijo ("LOSAS - CCO-12" → "CIMIENTOS - CCO-12"),
    /// rellena "ARBA - Origen" (y "ARBA - Código" si la partición lo contenía) y deja "Metrado - Elemento" igual a la
    /// categoría de la partición (la fija del prefijo o la del anfitrión real, ver ArbaPartition.CategoryFor).
    /// Asegura antes los parámetros del contrato. Dentro de una transacción.
    /// </summary>
    internal static class ArbaMigration
    {
        /// <summary>Todas las armaduras del modelo (barras, refuerzo de sistema, mallas).</summary>
        public static List<Element> AllRebar(Document doc)
        {
            var list = new List<Element>();
            list.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Rebar)).ToElements());
            list.AddRange(new FilteredElementCollector(doc).OfClass(typeof(RebarInSystem)).ToElements());
            list.AddRange(new FilteredElementCollector(doc).OfClass(typeof(FabricSheet)).ToElements());
            return list;
        }

        /// <summary>Armaduras alojadas en un anfitrión: las barras por RebarHostData (rápido) y las mallas y refuerzos de sistema por su anfitrión.</summary>
        public static List<Element> RebarOf(Document doc, Element host)
        {
            var list = new List<Element>();
            if (host == null) return list;
            var seen = new HashSet<ElementId>();
            try
            {
                RebarHostData hd = RebarHostData.GetRebarHostData(host);
                if (hd != null)
                    foreach (Rebar rb in hd.GetRebarsInHost())
                        if (seen.Add(rb.Id)) list.Add(rb);
            }
            catch (Exception) { }
            foreach (Element r in new FilteredElementCollector(doc).OfClass(typeof(RebarInSystem)).ToElements())
                if (ArbaPartition.RebarHostId(r) == host.Id && seen.Add(r.Id)) list.Add(r);
            foreach (Element r in new FilteredElementCollector(doc).OfClass(typeof(FabricSheet)).ToElements())
                if (ArbaPartition.RebarHostId(r) == host.Id && seen.Add(r.Id)) list.Add(r);
            return list;
        }

        /// <summary>Migra todo el modelo (todos los prefijos conocidos).</summary>
        /// <summary>Parámetros que necesita la migración de armaduras: origen, código y Metrado - Elemento.</summary>
        public static readonly ArbaParam[] RebarParams = { ArbaContract.Origen, ArbaContract.Codigo, ArbaContract.Elemento };

        public static ArbaMigrationResult MigrateAll(Document doc, bool overwriteOrigin = false, string template = null, IEnumerable<ArbaParam> ensure = null)
            => Run(doc, AllRebar(doc), null, overwriteOrigin, template, ensure);

        /// <summary>
        /// Migra las armaduras de un anfitrión; con <paramref name="only"/> solo las de ese add-in. Asegura antes los
        /// parámetros de <paramref name="ensure"/> (por defecto los ocho del contrato; un add-in de armado puede pasar
        /// <see cref="RebarParams"/> para crear solo los tres suyos).
        /// </summary>
        public static ArbaMigrationResult MigrateHost(Document doc, Element host, ArbaPrefix only = null, bool overwriteOrigin = false, string template = null, IEnumerable<ArbaParam> ensure = null)
            => Run(doc, RebarOf(doc, host), only, overwriteOrigin, template, ensure);

        /// <summary>Migra las armaduras de varios anfitriones (selección).</summary>
        public static ArbaMigrationResult MigrateHosts(Document doc, IEnumerable<Element> hosts, ArbaPrefix only = null, bool overwriteOrigin = false, string template = null, IEnumerable<ArbaParam> ensure = null)
        {
            var list = new List<Element>();
            foreach (Element h in hosts) list.AddRange(RebarOf(doc, h));
            return Run(doc, list, only, overwriteOrigin, template, ensure);
        }

        /// <summary>True si el anfitrión tiene armaduras de ese add-in anteriores al contrato (partición antigua y sin origen).</summary>
        public static bool HasLegacy(Document doc, Element host, ArbaPrefix prefix)
        {
            foreach (Element r in RebarOf(doc, host))
            {
                ArbaPartitionInfo info = ArbaPartition.ParseOf(r);
                if (info.IsArba && info.PrefixInfo == prefix && !ArbaOrigin.IsArba(r)) return true;
            }
            return false;
        }

        private static ArbaMigrationResult Run(Document doc, IEnumerable<Element> rebars, ArbaPrefix only, bool overwriteOrigin, string template, IEnumerable<ArbaParam> ensure)
        {
            var r = new ArbaMigrationResult();
            if (doc == null) return r;

            bool ok = ensure == null ? ArbaSharedParams.EnsureAll(doc, r.Avisos) : ArbaSharedParams.Ensure(doc, ensure, r.Avisos);
            if (!ok)
                r.Avisos.Add("No se pudieron asegurar todos los parámetros del contrato; la migración puede quedar incompleta.");
            doc.Regenerate();

            foreach (Element rebar in rebars)
            {
                r.Revisadas++;
                ArbaPartitionInfo info;
                try { info = ArbaPartition.ParseOf(rebar); }
                catch (Exception ex) { r.Avisos.Add("Armadura " + ArbaRevit.IdValue(rebar.Id) + ": " + ex.Message); continue; }

                if (info.Kind == ArbaPartitionKind.CategoryOnly) { r.SoloCategoria++; continue; }
                if (!info.IsArba) { r.Desconocidas++; continue; }
                if (only != null && info.PrefixInfo != only) continue;

                Element host = null;
                try { host = doc.GetElement(ArbaPartition.RebarHostId(rebar)); } catch (Exception) { }
                string category;
                if (host != null) category = ArbaPartition.CategoryFor(host, info.PrefixInfo);
                else
                {
                    category = ArbaPartition.CategoryFor(info.PrefixInfo, info.Category);
                    if (string.IsNullOrWhiteSpace(info.PrefixInfo?.Category)) r.SinAnfitrion++;
                }

                bool changed = false;
                try
                {
                    string wanted = ArbaPartition.Upgrade(info, category, template);
                    if (!string.Equals(wanted, info.Raw.Trim(), StringComparison.Ordinal) && ArbaPartition.Write(rebar, wanted))
                    {
                        r.ParticionesCambiadas++;
                        changed = true;
                    }

                    if (overwriteOrigin || !ArbaOrigin.IsArba(rebar))
                    {
                        string code = info.Code.Length > 0 ? info.Code : ArbaOrigin.CodeOf(rebar);
                        if (ArbaOrigin.Write(rebar, info.Origin, code, null, category))
                        {
                            r.OrigenEscrito++;
                            changed = true;
                        }
                    }
                    else if (!string.Equals(ArbaSharedParams.GetText(rebar, ArbaContract.Elemento).Trim(), category, StringComparison.Ordinal)
                             && ArbaSharedParams.SetText(rebar, ArbaContract.Elemento, category))
                    {
                        // 1.0.3 escribía la categoría del anfitrión (LOSAS en un cimiento dibujado como suelo).
                        r.ElementoCorregido++;
                        changed = true;
                    }
                }
                catch (Exception ex)
                {
                    r.Avisos.Add("Armadura " + ArbaRevit.IdValue(rebar.Id) + " (" + info.Raw + "): " + ex.Message);
                    continue;
                }

                if (changed) r.Migradas++; else r.YaConformes++;
                r.PorPrefijo[info.Prefix] = (r.PorPrefijo.TryGetValue(info.Prefix, out int n) ? n : 0) + 1;
            }
            return r;
        }
    }
}
