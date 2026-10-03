using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace Arba.Comun
{
    /// <summary>
    /// Asegura los parámetros compartidos del contrato (definición con GUID fijo + vínculo de ejemplar a sus
    /// categorías) y da acceso a ellos por GUID. Las definiciones se crean desde un archivo de parámetros
    /// compartidos TEMPORAL en %TEMP%, restaurando siempre el archivo del usuario (try/finally) y borrando el
    /// temporal. Si el proyecto ya tiene un parámetro homónimo que no es el del contrato (de proyecto no
    /// compartido, o compartido con otro GUID), se migra: se leen sus valores, se quita su vínculo, se vincula el
    /// del contrato y se vuelven a escribir los valores, avisando. Todo lo que escribe exige una transacción abierta.
    /// </summary>
    internal static class ArbaSharedParams
    {
        // ------------------------------------------------------------------ acceso

        /// <summary>Parámetro del contrato en un elemento: por GUID y, si no, por nombre. Null si el elemento no lo tiene.</summary>
        public static Parameter Get(Element e, ArbaParam p)
        {
            if (e == null || p == null) return null;
            try
            {
                Parameter q = e.get_Parameter(p.Guid);
                if (q != null) return q;
            }
            catch (Exception) { }
            try { return e.LookupParameter(p.Name); }
            catch (Exception) { return null; }
        }

        public static bool Has(Element e, ArbaParam p) => Get(e, p) != null;

        public static string GetText(Element e, ArbaParam p) => ArbaRevit.Text(Get(e, p));

        public static double GetDouble(Element e, ArbaParam p)
        {
            Parameter q = Get(e, p);
            try { return q != null && q.HasValue && q.StorageType == StorageType.Double ? q.AsDouble() : 0; }
            catch (Exception) { return 0; }
        }

        public static int GetInteger(Element e, ArbaParam p)
        {
            Parameter q = Get(e, p);
            try { return q != null && q.HasValue && q.StorageType == StorageType.Integer ? q.AsInteger() : 0; }
            catch (Exception) { return 0; }
        }

        public static bool SetText(Element e, ArbaParam p, string value) => ArbaRevit.SetText(Get(e, p), value);
        public static bool SetDouble(Element e, ArbaParam p, double value) => ArbaRevit.SetDouble(Get(e, p), value);
        public static bool SetInteger(Element e, ArbaParam p, int value) => ArbaRevit.SetInteger(Get(e, p), value);

        /// <summary>Id del parámetro compartido en el proyecto (para reglas de filtro y campos de tabla); null si aún no existe.</summary>
        public static ElementId IdOf(Document doc, ArbaParam p)
        {
            try { return SharedParameterElement.Lookup(doc, p.Guid)?.Id; }
            catch (Exception) { return null; }
        }

        /// <summary>True si la definición del contrato existe y está vinculada en el proyecto.</summary>
        public static bool IsBound(Document doc, ArbaParam p) => FindBoundByGuid(doc, p.Guid) != null;

        // ------------------------------------------------------------------ asegurar

        /// <summary>Asegura todos los parámetros del contrato. Dentro de una transacción. Devuelve false si alguno falló (ver avisos).</summary>
        public static bool EnsureAll(Document doc, IList<string> warnings)
        {
            bool ok = true;
            foreach (ArbaParam p in ArbaContract.Parametros) ok &= Ensure(doc, p, warnings);
            return ok;
        }

        /// <summary>Asegura los parámetros indicados (p. ej. solo los de ARBA - Origen/Código para un add-in de armado).</summary>
        public static bool Ensure(Document doc, IEnumerable<ArbaParam> parameters, IList<string> warnings)
        {
            bool ok = true;
            foreach (ArbaParam p in parameters) ok &= Ensure(doc, p, warnings);
            return ok;
        }

        /// <summary>
        /// Asegura un parámetro: definición compartida con su GUID, vínculo de ejemplar a todas sus categorías
        /// (completa las que falten), migración de un homónimo antiguo. Dentro de una transacción abierta.
        /// </summary>
        public static bool Ensure(Document doc, ArbaParam p, IList<string> warnings)
        {
            if (warnings == null) warnings = new List<string>();
            if (doc == null || p == null) return false;
            if (doc.IsFamilyDocument) { warnings.Add(p.Name + ": no se vincula en un documento de familia."); return false; }

            try
            {
                Application app = doc.Application;
                CategorySet categories = CategorySetOf(doc, p);
                if (categories.IsEmpty)
                {
                    warnings.Add(p.Name + ": ninguna de sus categorías admite parámetros en este proyecto.");
                    return false;
                }

                // 1. Ya vinculado con el GUID del contrato: solo completar categorías.
                Definition bound = FindBoundByGuid(doc, p.Guid);
                if (bound != null)
                {
                    CompleteCategories(doc, bound, categories, warnings);
                    return true;
                }

                // 2. Homónimo que no es el del contrato: migrar (valores, vínculo).
                Definition legacy = FindBoundByName(doc, p.Name);
                if (legacy != null) return MigrateLegacy(doc, legacy, p, categories, warnings);

                // 3. Nuevo.
                ExternalDefinition def = CreateDefinition(app, p, warnings);
                if (def == null) return false;
                if (!Bind(doc, def, categories))
                {
                    warnings.Add("No se pudo vincular el parámetro \"" + p.Name + "\" a sus categorías.");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                warnings.Add("No se pudo crear el parámetro \"" + p.Name + "\": " + ex.Message);
                return false;
            }
        }

        private static CategorySet CategorySetOf(Document doc, ArbaParam p)
        {
            CategorySet set = doc.Application.Create.NewCategorySet();
            foreach (string name in p.Categories)
            {
                BuiltInCategory bic = ArbaRevit.ParseCategory(name);
                if (bic == BuiltInCategory.INVALID) continue;
                Category c = null;
                try { c = Category.GetCategory(doc, bic); } catch (Exception) { }
                if (c != null && c.AllowsBoundParameters) set.Insert(c);
            }
            return set;
        }

        private static bool Bind(Document doc, Definition def, CategorySet categories)
        {
            InstanceBinding binding = doc.Application.Create.NewInstanceBinding(categories);
            bool ok = doc.ParameterBindings.Insert(def, binding, Group());
            if (!ok) ok = doc.ParameterBindings.ReInsert(def, binding, Group());
            return ok;
        }

        private static void CompleteCategories(Document doc, Definition def, CategorySet wanted, IList<string> warnings)
        {
            var binding = doc.ParameterBindings.get_Item(def) as InstanceBinding;
            if (binding == null)
            {
                // vinculado como parámetro de tipo: el contrato lo exige de ejemplar
                warnings.Add("\"" + def.Name + "\" está vinculado como parámetro de tipo; el contrato lo exige de ejemplar. Se vuelve a vincular.");
                doc.ParameterBindings.Remove(def);
                Bind(doc, def, wanted);
                return;
            }
            bool missing = false;
            foreach (Category c in wanted)
            {
                if (!binding.Categories.Contains(c)) { binding.Categories.Insert(c); missing = true; }
            }
            if (missing) doc.ParameterBindings.ReInsert(def, binding, Group());
        }

        /// <summary>Definición vinculada cuyo parámetro compartido tiene este GUID; null si no está.</summary>
        public static Definition FindBoundByGuid(Document doc, Guid guid)
        {
            try
            {
                SharedParameterElement spe = SharedParameterElement.Lookup(doc, guid);
                if (spe == null) return null;
                InternalDefinition def = spe.GetDefinition();
                return def != null && doc.ParameterBindings.Contains(def) ? def : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>Definición vinculada con ese nombre (sin distinguir mayúsculas); null si no hay.</summary>
        public static Definition FindBoundByName(Document doc, string name)
        {
            DefinitionBindingMapIterator it = doc.ParameterBindings.ForwardIterator();
            it.Reset();
            while (it.MoveNext())
            {
                Definition d = it.Key;
                if (d != null && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) return d;
            }
            return null;
        }

        /// <summary>GUID del parámetro compartido de una definición vinculada; null si es un parámetro de proyecto no compartido.</summary>
        public static Guid? GuidOf(Document doc, Definition def)
        {
            var idef = def as InternalDefinition;
            if (idef == null) return null;
            try { return (doc.GetElement(idef.Id) as SharedParameterElement)?.GuidValue; }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------------ migración de homónimos

        private sealed class LegacyValue
        {
            public ElementId Id;
            public string Text;
            public double Number;
            public int Integer;
            public StorageType Storage;
        }

        private static bool MigrateLegacy(Document doc, Definition legacy, ArbaParam p, CategorySet categories, IList<string> warnings)
        {
            Guid? legacyGuid = GuidOf(doc, legacy);
            string kind = legacyGuid.HasValue ? "compartido con otro GUID (" + legacyGuid.Value.ToString().ToUpperInvariant() + ")" : "de proyecto no compartido";

            // valores actuales
            var values = new List<LegacyValue>();
            var legacyCategories = new List<BuiltInCategory>();
            var legacyBinding = doc.ParameterBindings.get_Item(legacy) as ElementBinding;
            if (legacyBinding != null)
                foreach (Category c in legacyBinding.Categories)
                {
                    BuiltInCategory bic = ArbaRevit.BuiltInOf(c);
                    if (bic != BuiltInCategory.INVALID) legacyCategories.Add(bic);
                }
            if (legacyCategories.Count > 0)
            {
                foreach (Element e in ArbaRevit.Instances(doc, legacyCategories).ToElements())
                {
                    Parameter lp = null;
                    try { lp = e.get_Parameter(legacy); } catch (Exception) { }
                    if (lp == null || !lp.HasValue) continue;
                    var v = new LegacyValue { Id = e.Id, Storage = lp.StorageType };
                    try
                    {
                        switch (lp.StorageType)
                        {
                            case StorageType.String: v.Text = lp.AsString(); break;
                            case StorageType.Double: v.Number = lp.AsDouble(); break;
                            case StorageType.Integer: v.Integer = lp.AsInteger(); break;
                            default: continue;
                        }
                    }
                    catch (Exception) { continue; }
                    if (v.Storage == StorageType.String && string.IsNullOrEmpty(v.Text)) continue;
                    values.Add(v);
                }
            }

            // sustituir el vínculo dentro de una subtransacción: si algo falla no se pierde nada
            using (var sub = new SubTransaction(doc))
            {
                sub.Start();
                try
                {
                    if (!doc.ParameterBindings.Remove(legacy))
                        throw new InvalidOperationException("Revit no permitió quitar el vínculo del parámetro antiguo.");
                    ExternalDefinition def = CreateDefinition(doc.Application, p, warnings);
                    if (def == null) throw new InvalidOperationException("no se pudo crear la definición compartida.");
                    if (!Bind(doc, def, categories)) throw new InvalidOperationException("no se pudo vincular la definición compartida.");
                    doc.Regenerate();

                    int written = 0, lost = 0;
                    foreach (LegacyValue v in values)
                    {
                        Element e = doc.GetElement(v.Id);
                        Parameter np = e?.get_Parameter(p.Guid);
                        if (np == null || np.IsReadOnly) { lost++; continue; }
                        if (WriteConverted(np, v)) written++; else lost++;
                    }
                    sub.Commit();
                    warnings.Add("\"" + p.Name + "\": existía como parámetro " + kind + "; se ha sustituido por el compartido del contrato " +
                                 "(" + written + " valor(es) conservado(s)" + (lost > 0 ? ", " + lost + " no se pudieron copiar" : "") + ").");
                    return true;
                }
                catch (Exception ex)
                {
                    sub.RollBack();
                    warnings.Add("\"" + p.Name + "\": existe como parámetro " + kind + " y no se pudo migrar (" + ex.Message +
                                 "). Se deja como está; el contrato lo busca por GUID y no lo verá.");
                    return false;
                }
            }
        }

        private static bool WriteConverted(Parameter np, LegacyValue v)
        {
            try
            {
                switch (np.StorageType)
                {
                    case StorageType.String:
                        string s = v.Storage == StorageType.String ? v.Text
                                 : v.Storage == StorageType.Double ? v.Number.ToString(CultureInfo.InvariantCulture)
                                 : v.Integer.ToString(CultureInfo.InvariantCulture);
                        return np.Set(s ?? "");
                    case StorageType.Double:
                        if (v.Storage == StorageType.Double) return np.Set(v.Number);
                        if (v.Storage == StorageType.Integer) return np.Set((double)v.Integer);
                        return double.TryParse((v.Text ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && np.Set(d);
                    case StorageType.Integer:
                        if (v.Storage == StorageType.Integer) return np.Set(v.Integer);
                        if (v.Storage == StorageType.Double) return np.Set((int)Math.Round(v.Number));
                        return int.TryParse((v.Text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && np.Set(i);
                }
            }
            catch (Exception) { }
            return false;
        }

        // ------------------------------------------------------------------ definición desde un archivo temporal

        /// <summary>Cabecera de un archivo de parámetros compartidos vacío, tal como lo escribe Revit (UTF-16 LE con BOM).</summary>
        private const string EmptySharedFile =
            "# This is a Revit shared parameter file.\r\n" +
            "# Do not edit manually.\r\n" +
            "*META\tVERSION\tMINVERSION\r\n" +
            "META\t2\t1\r\n" +
            "*GROUP\tID\tNAME\r\n" +
            "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE\r\n";

        /// <summary>
        /// Crea (u obtiene) la definición compartida del parámetro en un archivo temporal propio. Restaura siempre
        /// el archivo de parámetros compartidos del usuario y borra el temporal.
        /// </summary>
        public static ExternalDefinition CreateDefinition(Application app, ArbaParam p, IList<string> warnings)
        {
            string original = null;
            try { original = app.SharedParametersFilename; } catch (Exception) { }

            string temp = Path.Combine(Path.GetTempPath(), "ARBA-comun-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllText(temp, EmptySharedFile, Encoding.Unicode);
                app.SharedParametersFilename = temp;
                DefinitionFile df = null;
                try { df = app.OpenSharedParameterFile(); } catch (Exception) { }
                if (df == null)
                {
                    // segundo intento con un archivo vacío (es lo que usaba el plugin de metrados)
                    File.WriteAllText(temp, string.Empty);
                    app.SharedParametersFilename = temp;
                    df = app.OpenSharedParameterFile();
                }
                if (df == null)
                {
                    warnings.Add("No se pudo abrir el archivo temporal de parámetros compartidos (" + temp + ").");
                    return null;
                }

                DefinitionGroup group = df.Groups.get_Item(ArbaContract.SharedFileGroup) ?? df.Groups.Create(ArbaContract.SharedFileGroup);
                var existing = group.Definitions.get_Item(p.Name) as ExternalDefinition;
                if (existing != null) return existing;

#if REVIT2021
                var options = new ExternalDefinitionCreationOptions(p.Name, DataTypeOf(p.Type))
#else
                var options = new ExternalDefinitionCreationOptions(p.Name, DataTypeOf(p.Type))
#endif
                {
                    GUID = p.Guid,
                    Description = p.Description ?? string.Empty,
                    UserModifiable = true,
                    Visible = true,
                };
                return group.Definitions.Create(options) as ExternalDefinition;
            }
            finally
            {
                try { app.SharedParametersFilename = original ?? string.Empty; } catch (Exception) { }
                try { if (File.Exists(temp)) File.Delete(temp); } catch (Exception) { }
            }
        }

#if REVIT2021
        private static ParameterType DataTypeOf(ArbaParamType t)
        {
            switch (t)
            {
                case ArbaParamType.Number: return ParameterType.Number;
                case ArbaParamType.Integer: return ParameterType.Integer;
                default: return ParameterType.Text;
            }
        }

        private static BuiltInParameterGroup Group() => BuiltInParameterGroup.PG_DATA;
#else
        private static ForgeTypeId DataTypeOf(ArbaParamType t)
        {
            switch (t)
            {
                case ArbaParamType.Number: return SpecTypeId.Number;
                case ArbaParamType.Integer: return SpecTypeId.Int.Integer;
                default: return SpecTypeId.String.Text;
            }
        }

        private static ForgeTypeId Group() => GroupTypeId.Data;
#endif
    }
}
