using System;
using System.Collections.Generic;
using System.Linq;

namespace Arba.Comun
{
    /// <summary>
    /// Regla de coincidencia de nombres de tipo (de barra, de gancho, de etiqueta...), pura: primero el nombre
    /// exacto (sin distinguir mayúsculas); si no, los que contienen el fragmento. Un fragmento que coincide con
    /// varios tipos es AMBIGUO: <see cref="Unique"/> devuelve null y la ventana debe pedir que se elija uno.
    /// <see cref="First"/> conserva la regla antigua de seis add-ins (el primero que coincide, en silencio) para
    /// migrar sin cambiar de comportamiento; lo recomendado es <see cref="Unique"/> + <see cref="IsAmbiguous"/>.
    /// </summary>
    internal static class NameMatch
    {
        /// <summary>Candidatos: el exacto si existe (uno solo); si no, los que contienen el fragmento, en el orden dado.</summary>
        public static List<string> Candidates(IEnumerable<string> names, string name)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(name) || names == null) return list;
            string wanted = name.Trim();
            var all = names.Where(n => n != null).ToList();
            string exact = all.FirstOrDefault(n => string.Equals(n.Trim(), wanted, StringComparison.OrdinalIgnoreCase));
            if (exact != null) { list.Add(exact); return list; }
            list.AddRange(all.Where(n => n.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0));
            return list;
        }

        /// <summary>El único candidato, o null si no hay ninguno o hay varios (ambiguo).</summary>
        public static string Unique(IEnumerable<string> names, string name)
        {
            List<string> c = Candidates(names, name);
            return c.Count == 1 ? c[0] : null;
        }

        public static bool IsAmbiguous(IEnumerable<string> names, string name) => Candidates(names, name).Count > 1;

        /// <summary>
        /// Regla antigua (RebarGenerator.MatchName de zapatas, cimientos, columnas, losas, vigas y muros): exacto,
        /// si no el primer nombre que contiene el fragmento; null si ninguno. Nunca sustituye por otro tipo.
        /// </summary>
        public static string First(IEnumerable<string> names, string name)
        {
            List<string> c = Candidates(names, name);
            return c.Count > 0 ? c[0] : null;
        }
    }
}
