using Autodesk.Revit.DB;

namespace Arba.Comun
{
    /// <summary>
    /// Escritura de los parámetros de metrado del contrato en elementos que un add-in crea con peso propio
    /// (rejillas, ángulos): partida, material, peso y pernos. El plugin de metrados respeta el peso de estos
    /// elementos (ver <see cref="PesoProtegido"/>).
    /// </summary>
    internal static class ArbaMetrado
    {
        /// <summary>Escribe partida, material (ACERO ESTRUCTURAL por defecto), peso en kg y pernos. Los nulos no se tocan.</summary>
        public static void WriteMiscelaneo(Element e, string partida, double? pesoKg, int? pernos = null, string material = ArbaContract.MaterialAceroEstructural)
        {
            if (e == null) return;
            if (!string.IsNullOrWhiteSpace(partida)) ArbaSharedParams.SetText(e, ArbaContract.Partida, partida.Trim());
            if (!string.IsNullOrWhiteSpace(material)) ArbaSharedParams.SetText(e, ArbaContract.Material, material.Trim().ToUpperInvariant());
            if (pesoKg.HasValue) ArbaSharedParams.SetDouble(e, ArbaContract.Peso, System.Math.Round(pesoKg.Value, 3));
            if (pernos.HasValue) ArbaSharedParams.SetInteger(e, ArbaContract.Pernos, pernos.Value);
        }

        /// <summary>
        /// True si el plugin de metrados NO debe sobrescribir "Metrado - Peso (kg)": el elemento tiene origen ARBA
        /// y un peso mayor que cero escrito por su add-in.
        /// </summary>
        public static bool PesoProtegido(Element e) => ArbaOrigin.IsArba(e) && ArbaSharedParams.GetDouble(e, ArbaContract.Peso) > 0;

        /// <summary>Escribe "Metrado - Elemento" (grupo de metrado) si el elemento tiene el parámetro.</summary>
        public static bool WriteElemento(Element e, string grupo) => ArbaSharedParams.SetText(e, ArbaContract.Elemento, (grupo ?? "").Trim().ToUpperInvariant());
    }
}
