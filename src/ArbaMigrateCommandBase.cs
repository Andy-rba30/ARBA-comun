using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Arba.Comun
{
    /// <summary>
    /// Base del comando "Migrar particiones y origen": migra la selección (anfitriones y/o armaduras) o, si no
    /// hay nada seleccionado, todo el modelo, y muestra el resumen. Cada add-in lo expone así:
    /// <code>
    /// [Transaction(TransactionMode.Manual)]
    /// [Regeneration(RegenerationOption.Manual)]
    /// public class MigrarCommand : ArbaMigrateCommandBase { }
    /// </code>
    /// (los atributos van en la clase concreta: Revit los lee ahí). Con <see cref="OnlyPrefix"/> se limita a un add-in.
    /// </summary>
    internal abstract class ArbaMigrateCommandBase : IExternalCommand
    {
        protected virtual string Title => "Migrar particiones y origen";

        /// <summary>Null = todos los prefijos del contrato; un prefijo = solo las armaduras de ese add-in.</summary>
        protected virtual ArbaPrefix OnlyPrefix => null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show(Title, "Abra un proyecto de Revit antes de ejecutar el comando.");
                return Result.Cancelled;
            }
            Document doc = uidoc.Document;

            try
            {
                var seleccion = new List<Element>();
                foreach (ElementId id in uidoc.Selection.GetElementIds())
                {
                    Element e = doc.GetElement(id);
                    if (e != null) seleccion.Add(e);
                }
                bool todo = seleccion.Count == 0;

                var td = new TaskDialog(Title)
                {
                    MainInstruction = todo ? "Migrar todo el modelo al contrato ARBA " + ArbaContract.Version
                                           : "Migrar la selección (" + seleccion.Count + " elemento(s)) al contrato ARBA " + ArbaContract.Version,
                    MainContent = "Convierte las particiones antiguas (ZAP-…, CC-…, BLQ-…, VIG-…, COL-…, LOSA-…, MC-…) a la forma " +
                                  "\"CATEGORIA - PREFIJO-marca\", rellena \"ARBA - Origen\", \"ARBA - Código\" y \"Metrado - Elemento\" y crea los " +
                                  "parámetros compartidos del contrato si faltan. No crea ni borra ninguna barra. Se puede deshacer con Ctrl+Z.",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = TaskDialogResult.Yes,
                };
                if (td.Show() != TaskDialogResult.Yes) return Result.Cancelled;

                ArbaMigrationResult result;
                using (var t = new Transaction(doc, Title))
                {
                    t.Start();
                    if (todo) result = ArbaMigration.MigrateAll(doc);
                    else
                    {
                        var hosts = seleccion.Where(e => !ArbaPartition.IsRebar(e)).ToList();
                        var rebars = seleccion.Where(ArbaPartition.IsRebar).ToList();
                        result = ArbaMigration.MigrateHosts(doc, hosts, OnlyPrefix);
                        if (rebars.Count > 0)
                        {
                            ArbaMigrationResult r2 = ArbaMigration.MigrateHosts(doc,
                                rebars.Select(x => doc.GetElement(ArbaPartition.RebarHostId(x))).Where(h => h != null).Distinct(), OnlyPrefix);
                            result.Avisos.AddRange(r2.Avisos);
                            result.Revisadas += r2.Revisadas; result.Migradas += r2.Migradas; result.YaConformes += r2.YaConformes;
                            result.ParticionesCambiadas += r2.ParticionesCambiadas; result.OrigenEscrito += r2.OrigenEscrito;
                        }
                    }
                    t.Commit();
                }

                var fin = new TaskDialog(Title)
                {
                    MainInstruction = result.Migradas > 0 ? "Migración terminada" : "Nada que migrar",
                    MainContent = result.Resumen(),
                    CommonButtons = TaskDialogCommonButtons.Close,
                };
                fin.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(Title, "Ocurrió un error:\n\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
