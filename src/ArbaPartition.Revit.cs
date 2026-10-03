using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace Arba.Comun
{
    internal static partial class ArbaPartition
    {
        /// <summary>CATEGORIA del contrato para un anfitrión: por su BuiltInCategory (OTROS si no está en la tabla o es nulo).</summary>
        public static string CategoryOf(Element host) => CategoryForBuiltIn(ArbaRevit.BuiltInNameOf(host));

        /// <summary>
        /// Categoría de lo que el add-in <paramref name="prefix"/> crea en <paramref name="host"/>: la fija del prefijo
        /// (CIMIENTOS para ZAP, CCO y BLQ aunque el anfitrión sea un suelo) o, si no la fija, la del anfitrión.
        /// </summary>
        public static string CategoryFor(Element host, ArbaPrefix prefix) => CategoryFor(prefix, host != null ? CategoryOf(host) : "");

        /// <summary>Marca (ALL_MODEL_MARK) del anfitrión; "" si no tiene.</summary>
        public static string MarkOf(Element host)
        {
            try { return host?.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString()?.Trim() ?? ""; }
            catch (Exception) { return ""; }
        }

        /// <summary>Id del anfitrión como texto (lo que usa {id} cuando la marca está vacía).</summary>
        public static string IdTextOf(Element host) => host == null ? "" : ArbaRevit.IdValue(host.Id).ToString();

        /// <summary>Nombre del tipo del anfitrión ("" si no tiene).</summary>
        public static string TypeNameOf(Element host)
        {
            try
            {
                if (host == null) return "";
                Element type = host.Document.GetElement(host.GetTypeId());
                return type?.Name?.Trim() ?? "";
            }
            catch (Exception) { return ""; }
        }

        /// <summary>
        /// Partición del contrato de un elemento creado en <paramref name="host"/> por el add-in <paramref name="prefix"/>.
        /// La categoría es la fija del prefijo o la del anfitrión (ver CategoryFor). Sin Marca, los add-ins de armado
        /// usan el Id y MAN el nombre del tipo (ver ArbaPrefix.MarkFallback).
        /// </summary>
        public static string BuildFor(Element host, ArbaPrefix prefix, string code = null, string template = null)
            => Build(CategoryFor(host, prefix), prefix?.Prefix, EffectiveMark(prefix, MarkOf(host), IdTextOf(host), TypeNameOf(host)), "", code, template);

        /// <summary>Partición con plantilla personalizada: rellena categoría, prefijo, marca e id del anfitrión; el resto viene en <paramref name="source"/>.</summary>
        public static string BuildFor(Element host, ArbaPrefix prefix, string template, PartitionName.Source source)
        {
            if (source == null) source = new PartitionName.Source();
            source.Category = CategoryFor(host, prefix);
            source.Prefix = prefix?.Prefix ?? "";
            if (string.IsNullOrWhiteSpace(source.Mark)) source.Mark = MarkOf(host);
            if (string.IsNullOrWhiteSpace(source.Id)) source.Id = IdTextOf(host);
            source.Mark = EffectiveMark(prefix, source.Mark, source.Id, TypeNameOf(host));
            return Build(template, source);
        }

        /// <summary>
        /// Parámetro Partición de una armadura: el predefinido NUMBER_PARTITION_PARAM y, si no existe, por nombre
        /// (inglés y español). Cuatro add-ins solo buscaban "Partition" y no escribían nada en Revit en español.
        /// </summary>
        public static Parameter PartitionParameter(Element rebar)
        {
            if (rebar == null) return null;
            Parameter p = null;
            try { p = rebar.get_Parameter(BuiltInParameter.NUMBER_PARTITION_PARAM); } catch (Exception) { }
            if (p == null) p = rebar.LookupParameter("Partition") ?? rebar.LookupParameter("Particion") ?? rebar.LookupParameter("Partición");
            return p;
        }

        public static string Read(Element rebar) => ArbaRevit.Text(PartitionParameter(rebar));

        public static bool Write(Element rebar, string partition)
        {
            if (string.IsNullOrEmpty(partition)) return false;
            return ArbaRevit.SetText(PartitionParameter(rebar), partition);
        }

        public static ArbaPartitionInfo ParseOf(Element rebar) => Parse(Read(rebar));

        /// <summary>Regla de filtro "Partición empieza por 'CATEGORIA - '" (para tablas y filtros de vista por categoría).</summary>
        public static FilterRule CategoryRule(string category)
            => ArbaRevit.BeginsWithRule(new ElementId(BuiltInParameter.NUMBER_PARTITION_PARAM), FilterPrefix(category));

        /// <summary>Regla de filtro "Partición empieza por 'CATEGORIA - PREFIJO-'".</summary>
        public static FilterRule PrefixRule(string category, string prefix)
            => ArbaRevit.BeginsWithRule(new ElementId(BuiltInParameter.NUMBER_PARTITION_PARAM), FilterPrefix(category, prefix));

        /// <summary>Id del anfitrión de una armadura, malla o refuerzo de sistema; InvalidElementId si no lo es.</summary>
        public static ElementId RebarHostId(Element rebar)
        {
            try
            {
                switch (rebar)
                {
                    case Rebar r: return r.GetHostId();
                    case RebarInSystem ris: return ris.GetHostId();
                    case FabricSheet fs: return fs.HostId;
                }
            }
            catch (Exception) { }
            return ElementId.InvalidElementId;
        }

        public static bool IsRebar(Element e) => e is Rebar || e is RebarInSystem || e is FabricSheet;
    }
}
