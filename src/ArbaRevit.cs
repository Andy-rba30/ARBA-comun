using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Arba.Comun
{
    /// <summary>
    /// Pequeñas diferencias de la API entre Revit 2021 y 2027 concentradas en un sitio (compilación condicional
    /// por REVIT#### / REVIT_2022_OR_OLDER / REVIT_2024_OR_NEWER, que define Arba.Comun.props).
    /// </summary>
    internal static class ArbaRevit
    {
        /// <summary>Valor numérico de un ElementId (int hasta 2023, long desde 2024).</summary>
        public static long IdValue(ElementId id)
        {
            if (id == null) return -1;
#if REVIT_2024_OR_NEWER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId IdFrom(long value)
        {
#if REVIT_2024_OR_NEWER
            return new ElementId(value);
#else
            return new ElementId((int)value);
#endif
        }

        /// <summary>BuiltInCategory de una categoría (InvalidCategory si es nula o no es una categoría predefinida).</summary>
        public static BuiltInCategory BuiltInOf(Category c)
        {
            if (c == null || c.Id == null) return BuiltInCategory.INVALID;
            long v = IdValue(c.Id);
            return v < 0 ? (BuiltInCategory)v : BuiltInCategory.INVALID;
        }

        /// <summary>Nombre "OST_..." de la categoría de un elemento ("" si no tiene o no es predefinida).</summary>
        public static string BuiltInNameOf(Element e)
        {
            BuiltInCategory bic = BuiltInOf(e?.Category);
            return bic == BuiltInCategory.INVALID ? "" : bic.ToString();
        }

        /// <summary>BuiltInCategory por su nombre "OST_..."; INVALID si no existe en esta versión de la API.</summary>
        public static BuiltInCategory ParseCategory(string ostName)
        {
            if (string.IsNullOrWhiteSpace(ostName)) return BuiltInCategory.INVALID;
            try
            {
                return (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), ostName.Trim(), true);
            }
            catch (ArgumentException) { return BuiltInCategory.INVALID; }
        }

        /// <summary>Regla de filtro "parámetro de texto = valor" (la firma con "distingue mayúsculas" desaparece en 2023).</summary>
        public static FilterRule EqualsRule(ElementId parameterId, string value)
        {
#if REVIT_2022_OR_OLDER
            return ParameterFilterRuleFactory.CreateEqualsRule(parameterId, value, false);
#else
            return ParameterFilterRuleFactory.CreateEqualsRule(parameterId, value);
#endif
        }

        /// <summary>Regla de filtro "parámetro de texto empieza por".</summary>
        public static FilterRule BeginsWithRule(ElementId parameterId, string value)
        {
#if REVIT_2022_OR_OLDER
            return ParameterFilterRuleFactory.CreateBeginsWithRule(parameterId, value, false);
#else
            return ParameterFilterRuleFactory.CreateBeginsWithRule(parameterId, value);
#endif
        }

        /// <summary>Texto de un parámetro ("" si es nulo, sin valor o no es de texto).</summary>
        public static string Text(Parameter p)
        {
            try
            {
                if (p == null || !p.HasValue) return "";
                return p.StorageType == StorageType.String ? (p.AsString() ?? "") : (p.AsValueString() ?? "");
            }
            catch (Exception) { return ""; }
        }

        public static bool SetText(Parameter p, string value)
        {
            try
            {
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.String) return false;
                string v = value ?? "";
                if (string.Equals(p.AsString() ?? "", v, StringComparison.Ordinal)) return true;
                return p.Set(v);
            }
            catch (Exception) { return false; }
        }

        public static bool SetDouble(Parameter p, double value)
        {
            try
            {
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Double) return false;
                if (p.HasValue && Math.Abs(p.AsDouble() - value) < 1e-9) return true;
                return p.Set(value);
            }
            catch (Exception) { return false; }
        }

        public static bool SetInteger(Parameter p, int value)
        {
            try
            {
                if (p == null || p.IsReadOnly || p.StorageType != StorageType.Integer) return false;
                if (p.HasValue && p.AsInteger() == value) return true;
                return p.Set(value);
            }
            catch (Exception) { return false; }
        }

        /// <summary>Ejemplares (no tipos) de las categorías dadas; vacío si ninguna existe en esta versión.</summary>
        public static FilteredElementCollector Instances(Document doc, IEnumerable<BuiltInCategory> categories)
        {
            var list = new List<BuiltInCategory>();
            foreach (BuiltInCategory b in categories) if (b != BuiltInCategory.INVALID) list.Add(b);
            var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
            if (list.Count == 1) return collector.OfCategory(list[0]);
            if (list.Count > 1) return collector.WherePasses(new ElementMulticategoryFilter(list));
            // ninguna categoría válida: un colector que no devuelve nada (ejemplar y tipo a la vez es imposible)
            return collector.WhereElementIsElementType();
        }
    }
}
