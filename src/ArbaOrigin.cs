using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace Arba.Comun
{
    /// <summary>
    /// Escribe y lee "ARBA - Origen", "ARBA - Código" y "ARBA - Anfitrión", y encuentra (o borra) los elementos
    /// que un add-in creó en un anfitrión, sin depender de Comentarios. Las armaduras conocen su anfitrión por la
    /// API; los demás elementos creados (rejillas, ángulos) lo llevan en "ARBA - Anfitrión".
    /// Los parámetros deben existir en el proyecto (ArbaSharedParams.Ensure) antes de escribir.
    /// </summary>
    internal static class ArbaOrigin
    {
        /// <summary>
        /// Marca un elemento recién creado: origen del add-in, código (capa / familia), anfitrión (solo si no es
        /// armadura) y "Metrado - Elemento" con la categoría del anfitrión. Devuelve false si no se pudo escribir el origen.
        /// </summary>
        public static bool WriteFor(Element created, Element host, ArbaPrefix prefix, string code)
        {
            if (created == null || prefix == null) return false;
            return Write(created, prefix.Origin, code, host?.Id, host != null ? ArbaPartition.CategoryOf(host) : null);
        }

        public static bool Write(Element e, string origin, string code, ElementId hostId = null, string metradoElemento = null)
        {
            if (e == null || string.IsNullOrWhiteSpace(origin)) return false;
            bool ok = ArbaSharedParams.SetText(e, ArbaContract.Origen, origin.Trim().ToUpperInvariant());
            ArbaSharedParams.SetText(e, ArbaContract.Codigo, (code ?? "").Trim());
            if (hostId != null && hostId != ElementId.InvalidElementId && !ArbaPartition.IsRebar(e))
                ArbaSharedParams.SetText(e, ArbaContract.Anfitrion, ArbaRevit.IdValue(hostId).ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(metradoElemento))
                ArbaSharedParams.SetText(e, ArbaContract.Elemento, metradoElemento.Trim().ToUpperInvariant());
            return ok;
        }

        public static string OriginOf(Element e) => ArbaSharedParams.GetText(e, ArbaContract.Origen).Trim().ToUpperInvariant();
        public static string CodeOf(Element e) => ArbaSharedParams.GetText(e, ArbaContract.Codigo).Trim();
        public static string HostTextOf(Element e) => ArbaSharedParams.GetText(e, ArbaContract.Anfitrion).Trim();

        /// <summary>True si el elemento lo creó algún add-in ARBA (tiene origen).</summary>
        public static bool IsArba(Element e) => OriginOf(e).Length > 0;

        /// <summary>True si el elemento lo creó el add-in del prefijo dado.</summary>
        public static bool IsFrom(Element e, ArbaPrefix prefix) =>
            prefix != null && string.Equals(OriginOf(e), prefix.Origin, StringComparison.OrdinalIgnoreCase);

        /// <summary>Prefijo (add-in) que creó el elemento, o null.</summary>
        public static ArbaPrefix PrefixOf(Element e) => ArbaContract.PrefixByOrigin(OriginOf(e));

        /// <summary>Anfitrión del elemento: por la API si es armadura; si no, por "ARBA - Anfitrión". InvalidElementId si no se sabe.</summary>
        public static ElementId HostIdOf(Element e)
        {
            if (e == null) return ElementId.InvalidElementId;
            if (ArbaPartition.IsRebar(e)) return ArbaPartition.RebarHostId(e);
            string t = HostTextOf(e);
            return long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) && v > 0
                ? ArbaRevit.IdFrom(v) : ElementId.InvalidElementId;
        }

        /// <summary>Elementos creados por el add-in del prefijo, opcionalmente solo los de un anfitrión y/o un código.</summary>
        public static List<Element> Find(Document doc, ArbaPrefix prefix, Element host = null, string code = null)
            => Find(doc, prefix?.Origin, host?.Id, code);

        public static List<Element> Find(Document doc, string origin, ElementId hostId = null, string code = null)
        {
            var result = new List<Element>();
            if (doc == null || string.IsNullOrWhiteSpace(origin)) return result;
            string wanted = origin.Trim().ToUpperInvariant();

            IEnumerable<Element> candidates = null;
            ElementId pid = ArbaSharedParams.IdOf(doc, ArbaContract.Origen);
            if (pid == null) return result;   // el parámetro no existe: nada lleva origen
            try
            {
                candidates = new FilteredElementCollector(doc).WhereElementIsNotElementType()
                    .WherePasses(new ElementParameterFilter(ArbaRevit.EqualsRule(pid, wanted))).ToElements();
            }
            catch (Exception)
            {
                candidates = null;
            }
            if (candidates == null)
            {
                // respaldo lento: todo ejemplar de las categorías del parámetro
                var cats = ArbaContract.Origen.Categories.Select(ArbaRevit.ParseCategory).ToList();
                candidates = ArbaRevit.Instances(doc, cats).ToElements();
            }

            foreach (Element e in candidates)
            {
                if (!string.Equals(OriginOf(e), wanted, StringComparison.OrdinalIgnoreCase)) continue;
                if (hostId != null && hostId != ElementId.InvalidElementId && HostIdOf(e) != hostId) continue;
                if (!string.IsNullOrWhiteSpace(code) && !string.Equals(CodeOf(e), code.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(e);
            }
            return result;
        }

        /// <summary>
        /// Borra lo que el add-in creó en el anfitrión (o en todo el modelo si es nulo). Dentro de una transacción.
        /// Devuelve el número de elementos borrados; <paramref name="barPositions"/> suma las barras de los conjuntos.
        /// </summary>
        public static int Delete(Document doc, ArbaPrefix prefix, Element host, out int barPositions, string code = null)
        {
            barPositions = 0;
            int n = 0;
            foreach (Element e in Find(doc, prefix, host, code))
            {
                try
                {
                    if (e is Autodesk.Revit.DB.Structure.Rebar rb) { try { barPositions += rb.NumberOfBarPositions; } catch (Exception) { } }
                    doc.Delete(e.Id);
                    n++;
                }
                catch (Exception) { }
            }
            return n;
        }

        public static int Delete(Document doc, ArbaPrefix prefix, Element host, string code = null) => Delete(doc, prefix, host, out _, code);
    }
}
