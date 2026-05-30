using System;
using System.Windows;

namespace Module.Views
{
    public partial class CadAlignmentPrincipleWindow : Window
    {
        public CadAlignmentPrincipleWindow()
        {
            InitializeComponent();
        }

        private void OnExportDxfClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var filePath = System.IO.Path.Combine(desktop,
                    $"CAD_Alignment_Arc_{DateTime.Now:yyyyMMdd_HHmmss}.dxf");
                GenerateDxfFile(filePath);
                MessageBox.Show($"DXF 文件已导出到:\n{filePath}",
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateDxfFile(string filePath)
        {
            using (var sw = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // DXF HEADER
                WritePair(sw, "0", "SECTION");
                WritePair(sw, "2", "HEADER");
                WritePair(sw, "9", "$ACADVER"); WritePair(sw, "1", "AC1015");
                WritePair(sw, "9", "$INSBASE");
                WritePair(sw, "10", "0.0"); WritePair(sw, "20", "0.0"); WritePair(sw, "30", "0.0");
                WritePair(sw, "0", "ENDSEC");

                // ENTITIES
                WritePair(sw, "0", "SECTION");
                WritePair(sw, "2", "ENTITIES");

                // 4组弦线点位数据（与SVG/XAML几何完全一致: 圆心230,305 R=138）
                var pairs = new (string name, double tx, double ty, double ax, double ay)[]
                {
                    ("P1-P2", 288.3, 430.1, 171.7, 430.1),
                    ("P3-P4", 355.1, 246.7, 309.2, 192.0),
                    ("P5-P6", 150.8, 192.0, 104.9, 246.7),
                    ("Pa-Pb", 92.3, 293.0, 117.0, 225.8),
                };

                // 拟合圆弧
                var (cx, cy, r) = FitArc(pairs);
                DxfArc(sw, cx, cy, r, 0, 360, "7");

                // 4条弦线 + 8个点标记 + 标签
                var colors = new[] { "1", "3", "5", "30" };
                for (int i = 0; i < pairs.Length; i++)
                {
                    var p = pairs[i];
                    DxfLine(sw, p.tx, p.ty, p.ax, p.ay, colors[i]);
                    DxfCircle(sw, p.tx, p.ty, 2.0, colors[i]);
                    DxfCircle(sw, p.ax, p.ay, 2.0, colors[i]);
                    DxfText(sw, p.tx - 7, p.ty + 5, p.name.Split('-')[0], 2.2, colors[i]);
                    DxfText(sw, p.ax + 3, p.ay + 5, p.name.Split('-')[1], 2.2, colors[i]);
                }

                // 圆心标记
                DxfCircle(sw, cx, cy, 3.5, "0");
                DxfText(sw, cx + 5, cy + 4, "O (Mox,Moy)", 2.2, "0");

                // 标题
                DxfText(sw, cx - 60, cy + r + 20, "CAD Alignment - 4 Chord Pairs on Arc (R=138)", 3.0, "7");

                WritePair(sw, "0", "ENDSEC");
                WritePair(sw, "0", "EOF");
            }
        }

        private static (double cx, double cy, double r) FitArc((string n, double tx, double ty, double ax, double ay)[] pairs)
        {
            int n = pairs.Length;
            double sx = 0, sy = 0;
            foreach (var p in pairs) { sx += p.tx; sy += p.ty; }
            double mx = sx / n, my = sy / n;

            double suu = 0, svv = 0, suv = 0, uuu = 0, vvv = 0;
            foreach (var p in pairs)
            {
                double u = p.tx - mx, v = p.ty - my;
                suu += u * u; svv += v * v; suv += u * v;
                uuu += u * (u * u + v * v); vvv += v * (u * u + v * v);
            }
            double det = suu * svv - suv * suv;
            if (Math.Abs(det) < 1e-10) return (mx, my, 120);

            double uc = (svv * uuu - suv * vvv) / (2 * det);
            double vc = (suu * vvv - suv * uuu) / (2 * det);
            return (mx + uc, my + vc, Math.Sqrt(uc * uc + vc * vc + (suu + svv) / n));
        }

        #region DXF 写入辅助

        private static void WritePair(System.IO.StreamWriter sw, string code, string val)
        { sw.WriteLine($"  {code}"); sw.WriteLine(val); }

        private static void DxfLine(System.IO.StreamWriter sw, double x1, double y1, double x2, double y2, string color)
        {
            WritePair(sw, "0", "LINE"); WritePair(sw, "8", "0"); WritePair(sw, "62", color);
            WritePair(sw, "10", x1.ToString("F4")); WritePair(sw, "20", y1.ToString("F4"));
            WritePair(sw, "30", "0.0");
            WritePair(sw, "11", x2.ToString("F4")); WritePair(sw, "21", y2.ToString("F4"));
            WritePair(sw, "31", "0.0");
        }

        private static void DxfArc(System.IO.StreamWriter sw, double cx, double cy, double r, double sa, double ea, string color)
        {
            WritePair(sw, "0", "ARC"); WritePair(sw, "8", "0"); WritePair(sw, "62", color);
            WritePair(sw, "10", cx.ToString("F4")); WritePair(sw, "20", cy.ToString("F4")); WritePair(sw, "30", "0.0");
            WritePair(sw, "40", r.ToString("F4"));
            WritePair(sw, "50", sa.ToString("F4")); WritePair(sw, "51", ea.ToString("F4"));
        }

        private static void DxfCircle(System.IO.StreamWriter sw, double cx, double cy, double r, string color)
        {
            WritePair(sw, "0", "CIRCLE"); WritePair(sw, "8", "0"); WritePair(sw, "62", color);
            WritePair(sw, "10", cx.ToString("F4")); WritePair(sw, "20", cy.ToString("F4")); WritePair(sw, "30", "0.0");
            WritePair(sw, "40", r.ToString("F4"));
        }

        private static void DxfText(System.IO.StreamWriter sw, double x, double y, string text, double h, string color)
        {
            WritePair(sw, "0", "TEXT"); WritePair(sw, "8", "0"); WritePair(sw, "62", color);
            WritePair(sw, "10", x.ToString("F4")); WritePair(sw, "20", y.ToString("F4")); WritePair(sw, "30", "0.0");
            WritePair(sw, "40", h.ToString("F2")); WritePair(sw, "1", text);
        }

        #endregion
    }
}