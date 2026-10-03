using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Arba.Comun
{
    /// <summary>
    /// Solo para la comprobación de compilación: toca la API pública de cada clase común para que un cambio de
    /// firma en RevitAPI de alguna versión salte aquí. No se incluye en los add-ins (no está en src/).
    /// </summary>
    internal static class CheckUsage
    {
        internal static string Touch(Document doc, UIControlledApplication app, Element host, Element rebar)
        {
            var warnings = new List<string>();
            bool ok = ArbaSharedParams.EnsureAll(doc, warnings);
            ok &= ArbaSharedParams.Ensure(doc, new[] { ArbaContract.Origen, ArbaContract.Codigo }, warnings);
            ElementId pid = ArbaSharedParams.IdOf(doc, ArbaContract.Material);
            string origen = ArbaOrigin.OriginOf(rebar);
            ok &= ArbaOrigin.WriteFor(rebar, host, ArbaContract.Zapatas, "inferior");
            List<Element> mine = ArbaOrigin.Find(doc, ArbaContract.Zapatas, host);
            int deleted = ArbaOrigin.Delete(doc, ArbaContract.Zapatas, host, out int bars);
            ArbaMetrado.WriteMiscelaneo(host, ArbaContract.PartidaRejillas, 12.5, 5);
            bool protegido = ArbaMetrado.PesoProtegido(host);

            string partition = ArbaPartition.BuildFor(host, ArbaContract.Vigas);
            string custom = ArbaPartition.BuildFor(host, ArbaContract.Losas, "{categoria} - {prefijo}-{marca}-{codigo}", new PartitionName.Source { Code = "baston" });
            ArbaPartitionInfo info = ArbaPartition.ParseOf(rebar);
            ok &= ArbaPartition.Write(rebar, partition);
            FilterRule rule = ArbaPartition.CategoryRule(ArbaContract.CatVigas);
            FilterRule rule2 = ArbaPartition.PrefixRule(ArbaContract.CatVigas, "VIG");
            string cat = ArbaPartition.CategoryOf(host);

            ArbaMigrationResult r = ArbaMigration.MigrateAll(doc);
            ArbaMigrationResult r2 = ArbaMigration.MigrateHost(doc, host, ArbaContract.Bloques);
            bool legacy = ArbaMigration.HasLegacy(doc, host, ArbaContract.Bloques);

            ArbaRibbon.Ensure(app);
            var data = new PushButtonData("ARBA_Acero_Prueba", "Prueba", "x.dll", "X.Y");
            ArbaRibbon.AddAcero(app, data);
            ArbaRibbon.AddMetrados(app, new PushButtonData("ARBA_Metrados_Prueba", "Prueba", "x.dll", "X.Z"));
            System.Windows.Media.Imaging.BitmapSource icon = ArbaRibbon.IconMetrados(32);

            RevitTheme.Apply(new System.Windows.Window());
            string match = NameMatch.Unique(new[] { "5/8\"" }, "5/8");
            long idv = ArbaRevit.IdValue(host.Id);
            ElementId back = ArbaRevit.IdFrom(idv);

            return ok + origen + pid + mine.Count + deleted + bars + protegido + partition + custom + info + rule + rule2 + cat +
                   r.Resumen() + r2.Migradas + legacy + icon.Width + match + back + ArbaContract.Version;
        }
    }
}
