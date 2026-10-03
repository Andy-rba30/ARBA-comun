using Autodesk.Revit.DB;

namespace Arba.Comun
{
    /// <summary>
    /// Escritura de los parámetros de metrado del contrato en elementos que un add-in crea con peso propio
    /// (rejillas, ángulos): partida, material, peso, pernos y el grupo MISCELANEOS. El plugin de metrados respeta
    /// el peso de estos elementos (ver <see cref="PesoProtegido"/>) y los metra en su tabla de misceláneos.
    /// </summary>
    internal static class ArbaMetrado
    {
        /// <summary>
        /// Escribe partida, material (ACERO ESTRUCTURAL por defecto), peso en kg, pernos y "Metrado - Elemento" =
        /// MISCELANEOS. Los nulos no se tocan.
        /// </summary>
        public static void WriteMiscelaneo(Element e, string partida, double? pesoKg, int? pernos = null, string material = ArbaContract.MaterialAceroEstructural)
        {
            if (e == null) return;
            if (!string.IsNullOrWhiteSpace(partida)) ArbaSharedParams.SetText(e, ArbaContract.Partida, partida.Trim());
            if (!string.IsNullOrWhiteSpace(material)) ArbaSharedParams.SetText(e, ArbaContract.Material, material.Trim().ToUpperInvariant());
            if (pesoKg.HasValue) ArbaSharedParams.SetDouble(e, ArbaContract.Peso, System.Math.Round(pesoKg.Value, 3));
            if (pernos.HasValue) ArbaSharedParams.SetInteger(e, ArbaContract.Pernos, pernos.Value);
            ArbaSharedParams.SetText(e, ArbaContract.Elemento, ArbaContract.ElementoMiscelaneos);
        }

        /// <summary>
        /// True si el plugin de metrados NO debe sobrescribir "Metrado - Peso (kg)": el elemento NO es armadura
        /// (en las armaduras el único que escribe el peso es el plugin de metrados y debe actualizarlo), tiene origen
        /// ARBA y un peso mayor que cero escrito por su add-in (rejillas, ángulos).
        /// </summary>
        public static bool PesoProtegido(Element e) =>
            e != null && !ArbaPartition.IsRebar(e) && ArbaOrigin.IsArba(e) && ArbaSharedParams.GetDouble(e, ArbaContract.Peso) > 0;

        /// <summary>True si el elemento es un misceláneo del contrato (tiene "Metrado - Partida").</summary>
        public static bool EsMiscelaneo(Element e) => ArbaSharedParams.GetText(e, ArbaContract.Partida).Trim().Length > 0;

        /// <summary>Escribe "Metrado - Elemento" (grupo de metrado) si el elemento tiene el parámetro.</summary>
        public static bool WriteElemento(Element e, string grupo) => ArbaSharedParams.SetText(e, ArbaContract.Elemento, (grupo ?? "").Trim().ToUpperInvariant());

        /// <summary>
        /// Grupo de metrado ("Metrado - Elemento") que le toca a una armadura: lo que declara su partición (la
        /// categoría fija de su prefijo, o la del texto: "CIMIENTOS - ZAP-Z1", "LOSAS - CCO-12" → CIMIENTOS,
        /// "MUROS - MCO-M1", "CIMIENTOS") y, si la partición no declara ninguna (texto libre, antigua "MC-M1", vacía),
        /// <paramref name="hostGroup"/> (el grupo que el plugin de metrados asigna a la categoría del anfitrión) o,
        /// en su defecto, la categoría del anfitrión; OTROS sin anfitrión. Así una zapata o un cimiento modelados
        /// como suelo se metran en Cimentaciones y no en Losas.
        /// </summary>
        public static string ElementoFor(Element rebar, Element host, string hostGroup = null)
        {
            string declared = ArbaPartition.DeclaredCategory(ArbaPartition.ParseOf(rebar));
            if (declared.Length > 0) return declared;
            if (!string.IsNullOrWhiteSpace(hostGroup)) return hostGroup.Trim().ToUpperInvariant();
            return host != null ? ArbaPartition.CategoryOf(host) : ArbaContract.CatOtros;
        }
    }
}
