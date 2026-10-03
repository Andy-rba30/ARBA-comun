using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace Arba.Comun
{
    /// <summary>
    /// Gestión compartida de la pestaña "ARBA" y sus paneles (IA, Acero, Metrados, Encofrado), siempre en el
    /// mismo orden sin importar qué add-in cargue primero. Cada add-in trae esta clase en su propio ensamblado
    /// (código fuente común) y todos escriben en la misma pestaña, el mismo panel y el mismo desplegable "Acero".
    /// Unifica las siete copias de ArbaRibbon de los add-ins de armado y añade el panel "Metrados".
    /// </summary>
    internal static class ArbaRibbon
    {
        public const string TabName = ArbaContract.TabName;
        public const string PanelIaName = ArbaContract.PanelIa;
        public const string PanelAceroName = ArbaContract.PanelAcero;
        public const string PanelMetradosName = ArbaContract.PanelMetrados;
        public const string PanelEncofradoName = ArbaContract.PanelEncofrado;
        /// <summary>Nombre interno y texto del desplegable de armado (se conserva "Acero" por compatibilidad con los add-ins ya instalados).</summary>
        public const string PulldownAceroName = ArbaContract.PulldownAcero;

        /// <summary>
        /// Crea la pestaña "ARBA" si no existe y los paneles en orden estricto. Cada panel nuevo inicia oculto
        /// (Visible = false) y se muestra cuando alguien le añade un botón.
        /// </summary>
        public static void Ensure(UIControlledApplication app)
        {
            try { app.CreateRibbonTab(TabName); }
            catch (Exception) { /* ya creada por otro add-in */ }

            foreach (string panelName in ArbaContract.OrderedPanels)
            {
                if (FindPanel(app, panelName) == null)
                {
                    RibbonPanel panel = app.CreateRibbonPanel(TabName, panelName);
                    panel.Visible = false;
                }
            }
        }

        private static RibbonPanel FindPanel(UIControlledApplication app, string panelName)
        {
            var existing = app.GetRibbonPanels(TabName);
            if (existing == null) return null;
            foreach (RibbonPanel p in existing)
                if (string.Equals(p.Name, panelName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        /// <summary>Panel de la pestaña ARBA; si no existe lo crea (oculto).</summary>
        public static RibbonPanel GetPanel(UIControlledApplication app, string panelName)
        {
            RibbonPanel panel = FindPanel(app, panelName);
            if (panel != null) return panel;
            RibbonPanel created = app.CreateRibbonPanel(TabName, panelName);
            created.Visible = false;
            return created;
        }

        /// <summary>
        /// Busca un PulldownButton con el nombre dado en el panel; si no existe lo crea con su icono y le añade el
        /// PushButton. Al añadir, pone el panel visible. Los add-ins de armado llaman
        /// <c>AddToPulldown(app, PanelAceroName, PulldownAceroName, data)</c>.
        /// </summary>
        public static PushButton AddToPulldown(UIControlledApplication app, string panelName, string pulldownName, PushButtonData data)
        {
            RibbonPanel panel = GetPanel(app, panelName);

            PulldownButton pulldown = null;
            var items = panel.GetItems();
            if (items != null)
                foreach (RibbonItem item in items)
                    if (item is PulldownButton pb && string.Equals(pb.Name, pulldownName, StringComparison.OrdinalIgnoreCase))
                    {
                        pulldown = pb;
                        break;
                    }

            if (pulldown == null)
            {
                var pbData = new PulldownButtonData(pulldownName, pulldownName);
                if (string.Equals(pulldownName, PulldownAceroName, StringComparison.OrdinalIgnoreCase))
                {
                    pbData.ToolTip = "Herramientas de armado de acero (contrato ARBA " + ArbaContract.Version + ")";
                    pbData.LargeImage = IconAcero(32);
                    pbData.Image = IconAcero(16);
                }
                else if (string.Equals(pulldownName, PanelEncofradoName, StringComparison.OrdinalIgnoreCase))
                {
                    pbData.ToolTip = "Herramientas de metrado de encofrado";
                    pbData.LargeImage = IconEncofrado(32);
                    pbData.Image = IconEncofrado(16);
                }
                else if (string.Equals(pulldownName, PanelMetradosName, StringComparison.OrdinalIgnoreCase))
                {
                    pbData.ToolTip = "Metrados de concreto y acero";
                    pbData.LargeImage = IconMetrados(32);
                    pbData.Image = IconMetrados(16);
                }
                pulldown = panel.AddItem(pbData) as PulldownButton;
            }

            PushButton button = pulldown?.AddPushButton(data);
            panel.Visible = true;
            return button;
        }

        /// <summary>Añade el botón de armado al desplegable "Acero" del panel "Acero".</summary>
        public static PushButton AddAcero(UIControlledApplication app, PushButtonData data)
            => AddToPulldown(app, PanelAceroName, PulldownAceroName, data);

        /// <summary>Añade un botón suelto a un panel (el plugin de metrados usa el panel "Metrados"). Pone el panel visible.</summary>
        public static PushButton AddButton(UIControlledApplication app, string panelName, PushButtonData data)
        {
            RibbonPanel panel = GetPanel(app, panelName);
            var button = panel.AddItem(data) as PushButton;
            panel.Visible = true;
            return button;
        }

        /// <summary>Añade un botón suelto al panel "Metrados".</summary>
        public static PushButton AddMetrados(UIControlledApplication app, PushButtonData data) => AddButton(app, PanelMetradosName, data);

        // ------------------------------------------------------------------ iconos vectoriales

        /// <summary>Icono del desplegable Acero (sección en L con estribos y barras), el mismo en todos los add-ins.</summary>
        public static BitmapSource IconAcero(int size)
        {
            double s = size / 32.0;
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var concrete = new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9));
                var edge = new Pen(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), 1.2 * s);
                var stirrup1 = new Pen(new SolidColorBrush(Color.FromRgb(0x1F, 0x7A, 0x7A)), 1.6 * s) { LineJoin = PenLineJoin.Round };
                var stirrup2 = new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x2E)), 1.6 * s) { LineJoin = PenLineJoin.Round };
                var bar = new SolidColorBrush(Color.FromRgb(0x8B, 0x2E, 0x2E));

                var outline = new StreamGeometry();
                using (StreamGeometryContext g = outline.Open())
                {
                    g.BeginFigure(new Point(2 * s, 2 * s), true, true);
                    g.LineTo(new Point(30 * s, 2 * s), true, false);
                    g.LineTo(new Point(30 * s, 14 * s), true, false);
                    g.LineTo(new Point(14 * s, 14 * s), true, false);
                    g.LineTo(new Point(14 * s, 30 * s), true, false);
                    g.LineTo(new Point(2 * s, 30 * s), true, false);
                }
                dc.DrawGeometry(concrete, edge, outline);

                dc.DrawRectangle(null, stirrup1, new Rect(5 * s, 5 * s, 22 * s, 6 * s));
                dc.DrawRectangle(null, stirrup2, new Rect(5 * s, 5 * s, 6 * s, 22 * s));

                double rr = 1.7 * s;
                foreach (Point p in new[]
                {
                    new Point(5 * s, 5 * s), new Point(27 * s, 5 * s), new Point(27 * s, 11 * s),
                    new Point(11 * s, 11 * s), new Point(11 * s, 27 * s), new Point(5 * s, 27 * s), new Point(5 * s, 11 * s)
                })
                    dc.DrawEllipse(bar, null, p, rr, rr);
            }
            return Render(visual, size);
        }

        /// <summary>Icono para el desplegable y botones de Encofrado (sección con tableros de madera).</summary>
        public static BitmapSource IconEncofrado(int size)
        {
            double s = size / 32.0;
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var concrete = new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9));
                var edge = new Pen(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), 1.2 * s);
                var board = new Pen(new SolidColorBrush(Color.FromRgb(0xC8, 0x7A, 0x1E)), 2.6 * s)
                {
                    StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat
                };

                var outline = new StreamGeometry();
                using (StreamGeometryContext g = outline.Open())
                {
                    g.BeginFigure(new Point(3 * s, 30 * s), true, true);
                    g.LineTo(new Point(29 * s, 30 * s), true, false);
                    g.LineTo(new Point(29 * s, 23 * s), true, false);
                    g.LineTo(new Point(19 * s, 23 * s), true, false);
                    g.LineTo(new Point(18 * s, 2 * s), true, false);
                    g.LineTo(new Point(13 * s, 2 * s), true, false);
                    g.LineTo(new Point(10 * s, 23 * s), true, false);
                    g.LineTo(new Point(3 * s, 23 * s), true, false);
                }
                dc.DrawGeometry(concrete, edge, outline);

                dc.DrawLine(board, new Point(11.6 * s, 3 * s), new Point(8.6 * s, 22.5 * s));
                dc.DrawLine(board, new Point(19.5 * s, 3 * s), new Point(20.5 * s, 22.5 * s));
                dc.DrawLine(board, new Point(1.6 * s, 23 * s), new Point(1.6 * s, 30 * s));
                dc.DrawLine(board, new Point(30.4 * s, 23 * s), new Point(30.4 * s, 30 * s));
            }
            return Render(visual, size);
        }

        /// <summary>Icono de Metrados (tabla con filas y una columna de totales resaltada).</summary>
        public static BitmapSource IconMetrados(int size)
        {
            double s = size / 32.0;
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var paper = new SolidColorBrush(Color.FromRgb(0xF4, 0xF4, 0xF4));
                var edge = new Pen(new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), 1.2 * s);
                var line = new Pen(new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)), 1.0 * s);
                var header = new SolidColorBrush(Color.FromRgb(0x2F, 0x7B, 0xD9));
                var total = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x2E));

                dc.DrawRectangle(paper, edge, new Rect(3 * s, 3 * s, 26 * s, 26 * s));
                dc.DrawRectangle(header, null, new Rect(3.6 * s, 3.6 * s, 24.8 * s, 5 * s));
                for (int i = 1; i <= 3; i++)
                    dc.DrawLine(line, new Point(3 * s, (8.6 + i * 5) * s), new Point(29 * s, (8.6 + i * 5) * s));
                dc.DrawLine(line, new Point(12 * s, 8.6 * s), new Point(12 * s, 29 * s));
                dc.DrawLine(line, new Point(21 * s, 8.6 * s), new Point(21 * s, 29 * s));
                dc.DrawRectangle(total, null, new Rect(21.6 * s, 24.2 * s, 6.8 * s, 4.2 * s));
            }
            return Render(visual, size);
        }

        private static BitmapSource Render(DrawingVisual visual, int size)
        {
            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            bmp.Freeze();
            return bmp;
        }
    }
}
