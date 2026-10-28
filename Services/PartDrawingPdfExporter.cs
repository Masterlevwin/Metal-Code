using Metal_Code.Models;
using Metal_Code.Utils;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Colors = QuestPDF.Helpers.Colors;
using Path = System.Windows.Shapes.Path;
using Size = System.Windows.Size;

namespace Metal_Code.Services
{
    #region Данные для экспорта

    public class PartDrawingData
    {
        public Part Part { get; set; } = null!;
        public List<Point> CustomPoints { get; set; } = new();
        public List<double> BendPositionsX { get; set; } = new();
        public List<double> BendPositionsY { get; set; } = new();
        public double BendAngle { get; set; } = 90;
        public double BendCompensation { get; set; }
        public BendProfileDrawing? ProfileX { get; set; }
        public BendProfileDrawing? ProfileY { get; set; }
    }

    #endregion

    #region Экспортёр

    /// <summary>
    /// Сервис генерации PDF-чертежа детали.
    /// Использует Canvas + RenderTargetBitmap для рендеринга (надёжный подход).
    /// </summary>
    public class PartDrawingPdfExporter
    {
        private readonly PartDrawingData _data;
        private readonly Part _part;

        // ─── Константы рендеринга ───
        private const int RenderDpi = 150;                       // 150 DPI достаточно для печати
        private const double MmToPx = RenderDpi / 25.4;          // мм → пиксели
        private const double DimOffset = 10;                     // отступ размерной линии (мм)

        // ─── Кисти ───
        private static readonly Brush ContourBrush = Brushes.Black;
        private static readonly Brush FillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));
        private static readonly Brush DimBrush = new SolidColorBrush(Color.FromRgb(0, 90, 180));
        private static readonly Brush HoleBrush = new SolidColorBrush(Color.FromRgb(200, 40, 40));
        private static readonly Brush HoleFillBrush = new SolidColorBrush(Color.FromArgb(40, 200, 40, 40));
        private static readonly Brush BendBrush = new SolidColorBrush(Color.FromRgb(220, 130, 0));

        static PartDrawingPdfExporter()
        {
            FillBrush.Freeze();
            DimBrush.Freeze();
            HoleBrush.Freeze();
            HoleFillBrush.Freeze();
            BendBrush.Freeze();
        }

        public PartDrawingPdfExporter(PartDrawingData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _part = data.Part ?? throw new ArgumentException("Part не задан");
        }

        #region Публичный метод

        public void Export(string filePath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            byte[] mainImageBytes = RenderDrawingImage();
            byte[]? isoImageBytes = RenderIsometricView();  // ★ может быть null

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(IsLargePart()
                        ? PageSizes.A3.Landscape()
                        : PageSizes.A4.Landscape());
                    page.PageColor(Colors.White);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, mainImageBytes, isoImageBytes));
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(filePath);
        }

        #endregion

        #region Структура документа

        private bool IsLargePart()
        {
            if (IsPipePart(_part.PartType))
                return _part.Length > 2000;
            return _part.Width > 1500 || _part.Height > 1000;
        }

        private void ComposeHeader(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(_part.Title ?? "Деталь")
                        .FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                    row.AutoItem().AlignRight()
                        .Text($"Дата: {DateTime.Now:dd.MM.yyyy}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                column.Item().PaddingVertical(4)
                      .LineHorizontal(1.5f).LineColor(Colors.Blue.Medium);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Материал: {_part.Metal ?? "—"}").FontSize(10);
                    row.RelativeItem().Text($"Толщина: {_part.Destiny:0.#} мм").FontSize(10);
                    row.RelativeItem().Text(GetDimensionsText()).FontSize(10);
                    row.RelativeItem().AlignRight()
                        .Text($"Кол-во: {_part.Count} шт").FontSize(10);
                });
            });
        }

        private string GetDimensionsText()
        {
            if (IsPipePart(_part.PartType))
                return $"Длина: {_part.Length:0} мм";
            return _part.PartType == PartType.Round
                ? $"⌀{_part.Width:0} мм"
                : $"{_part.Width:0}×{_part.Height:0} мм";
        }

        private void ComposeContent(IContainer container, byte[] mainImageBytes, byte[]? isoImageBytes)
        {
            container.Column(column =>
            {
                column.Spacing(10);

                // ─── Развёртка + Изометрия ───
                column.Item().Row(row =>
                {
                    // Развёртка (основная часть — 3/4 ширины)
                    row.RelativeItem(3)
                       .Border(1).BorderColor(Colors.Grey.Lighten2)
                       .Element(c => c.Image(mainImageBytes).FitArea());

                    if (isoImageBytes != null)
                    {
                        row.ConstantItem(15);

                        // Изометрия (1/4 ширины)
                        row.RelativeItem(1)
                           .Column(isoColumn =>
                           {
                               isoColumn.Item()
                                        .PaddingBottom(4)
                                        .AlignCenter()
                                        .Text("Изометрия")
                                        .FontSize(11).SemiBold()
                                        .FontColor(Colors.Blue.Darken2);

                               isoColumn.Item()
                                        .Border(1).BorderColor(Colors.Grey.Lighten2)
                                        .Element(c => c.Image(isoImageBytes).FitArea());
                           });
                    }
                });

                // Таблица отверстий
                if (HasHoles())
                    column.Item().Element(DrawHolesSection);

                // Информация о гибке
                if (HasBends())
                    column.Item().Element(DrawBendsSection);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text("Metal-Code")
                    .FontSize(8).FontColor(Colors.Grey.Medium);
                row.RelativeItem().AlignCenter()
                    .Text("Чертёж сформирован автоматически. Проверьте размеры перед изготовлением.")
                    .FontSize(8).FontColor(Colors.Grey.Medium).Italic();
                row.RelativeItem().AlignRight()
                    .Text("Страница 1").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }

        #endregion

        #region ★ Рендеринг изображения через Canvas

        /// <summary>
        /// Создаёт изображение чертежа через Canvas и рендерит в PNG.
        /// </summary>
        private byte[] RenderDrawingImage()
        {
            // ★ Размеры для АЛЬБОМНОЙ ориентации
            // A4 альбом: 297×210 мм, поля 25×2 → область 247×160 мм
            // A3 альбом: 420×297 мм, поля 25×2 → область 370×247 мм
            double contentWmm = IsLargePart() ? 370 : 247;
            double contentHmm = IsLargePart() ? 155 : 100;

            double imgWidthPx = contentWmm * MmToPx;
            double imgHeightPx = contentHmm * MmToPx;

            var canvas = new Canvas
            {
                Width = imgWidthPx,
                Height = imgHeightPx,
                Background = Brushes.White
            };

            // Область чертежа с отступами под размеры
            double left = DimOffset * 2.5 * MmToPx;
            double top = DimOffset * 1.5 * MmToPx;
            double right = imgWidthPx - DimOffset * 2.5 * MmToPx;
            double bottom = imgHeightPx - DimOffset * 2.0 * MmToPx;
            double drawW = right - left;
            double drawH = bottom - top;

            if (IsPipePart(_part.PartType))
                DrawPipeView(canvas, left, top, drawW, drawH);
            else if (_part.PartType == PartType.Custom)
                DrawCustomView(canvas, left, top, drawW, drawH);
            else
                DrawSheetView(canvas, left, top, drawW, drawH);

            canvas.Measure(new Size(imgWidthPx, imgHeightPx));
            canvas.Arrange(new Rect(0, 0, imgWidthPx, imgHeightPx));
            canvas.UpdateLayout();

            return RenderCanvasToPng(canvas, imgWidthPx, imgHeightPx);
        }

        /// <summary>
        /// Рендерит Canvas в PNG-байты через RenderTargetBitmap.
        /// </summary>
        private static byte[] RenderCanvasToPng(Canvas canvas, double width, double height)
        {
            // ★ КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ:
            // WPF задаёт размеры в дип-пикселях (96 DPI),
            // а RenderTargetBitmap рендерит в физических пикселях при RenderDpi.
            // Нужно масштабировать размер битмапа на коэффициент RenderDpi/96.
            double scaleFactor = RenderDpi / 96.0;
            int bitmapWidth = (int)Math.Ceiling(width * scaleFactor);
            int bitmapHeight = (int)Math.Ceiling(height * scaleFactor);

            var bitmap = new RenderTargetBitmap(
                bitmapWidth, bitmapHeight,
                RenderDpi, RenderDpi,
                PixelFormats.Pbgra32);
            bitmap.Render(canvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        /// <summary>
        /// Рендерит изометрический вид через существующий IsometricPreviewRenderer.
        /// Возвращает null, если изометрия недоступна.
        /// </summary>
        private byte[]? RenderIsometricView()
        {
            if (_part.PartType != PartType.Rectangle) return null;
            if (_data.ProfileX == null && _data.ProfileY == null) return null;
            if (_part.Width <= 0 || _part.Height <= 0) return null;

            try
            {
                double isoWidthPx = 250 * MmToPx;
                double isoHeightPx = 180 * MmToPx;

                var canvas = new Canvas
                {
                    Width = isoWidthPx,
                    Height = isoHeightPx,
                    Background = Brushes.White
                };

                // Сначала измеряем, чтобы ActualWidth/ActualHeight были корректны
                canvas.Measure(new Size(isoWidthPx, isoHeightPx));
                canvas.Arrange(new Rect(0, 0, isoWidthPx, isoHeightPx));
                canvas.UpdateLayout();

                // Рендерим изометрию через существующий рендерер
                IsometricPreviewRenderer.RenderInto(
                    canvas,
                    _part.Width,
                    _part.Height,
                    _part.Destiny,
                    _data.ProfileX,
                    _data.ProfileY);

                // Обновляем после добавления элементов
                canvas.UpdateLayout();

                return RenderCanvasToPng(canvas, isoWidthPx, isoHeightPx);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"⚠️ Ошибка рендеринга изометрии: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Листовые детали

        private void DrawSheetView(Canvas canvas, double left, double top,
            double drawW, double drawH)
        {
            double partW = Math.Max(_part.Width, 1);
            double partH = Math.Max(_part.Height, 1);

            double scale = Math.Min(drawW / partW, drawH / partH);
            double w = partW * scale;
            double h = partH * scale;
            double x0 = left + (drawW - w) / 2;
            double y0 = top + (drawH - h) / 2;

            // ─── Контур ───
            switch (_part.PartType)
            {
                case PartType.Rectangle:
                    canvas.Children.Add(new Rectangle
                    {
                        Width = w,
                        Height = h,
                        Fill = FillBrush,
                        Stroke = Brushes.Black,
                        StrokeThickness = 2.5,
                        RenderTransform = new TranslateTransform(x0, y0)
                    });
                    break;

                case PartType.Round:
                    double cx = x0 + w / 2, cy = y0 + h / 2, r = Math.Min(w, h) / 2;
                    canvas.Children.Add(new Ellipse
                    {
                        Width = r * 2,
                        Height = r * 2,
                        Fill = FillBrush,
                        Stroke = Brushes.Black,
                        StrokeThickness = 2.5,
                        RenderTransform = new TranslateTransform(cx - r, cy - r)
                    });
                    break;

                case PartType.Triangle:
                    var triangle = new PathGeometry();
                    var fig = new PathFigure
                    {
                        StartPoint = new Point(x0, y0 + h),
                        IsClosed = true,
                        IsFilled = true
                    };
                    fig.Segments.Add(new LineSegment(new Point(x0 + w, y0 + h), true));
                    fig.Segments.Add(new LineSegment(new Point(x0, y0), true));
                    triangle.Figures.Add(fig);
                    canvas.Children.Add(new Path
                    {
                        Data = triangle,
                        Fill = FillBrush,
                        Stroke = Brushes.Black,
                        StrokeThickness = 2.5
                    });
                    break;
            }

            // ─── Отверстия (исправлено: координаты от центра) ───
            DrawSheetHoles(canvas, x0, y0, w, h, scale);

            // ─── Линии гибов ───
            DrawSheetBendLines(canvas, x0, y0, w, h, scale);

            // ─── Габаритные размеры ───
            if (_part.PartType == PartType.Round)
            {
                DrawDiameterDimension(canvas, x0 + w / 2, y0 + h / 2, Math.Min(w, h) / 2, scale);
            }
            else
            {
                DrawWidthDimension(canvas, x0, y0 + h, w, partW);
                DrawHeightDimension(canvas, x0 + w, y0, h, partH);
            }

            // ─── Размеры до линий гибов ───
            DrawBendDimensions(canvas, x0, y0, w, h, scale);
        }

        #endregion

        #region ★ Отверстия — исправлено позиционирование

        private void DrawSheetHoles(Canvas canvas, double x0, double y0,
            double w, double h, double scale)
        {
            var holePositions = PartPreviewGenerator.CalculateHolePositions(_part);
            if (holePositions == null) return;

            // ★ Координаты от CalculateHolePositions — относительно ЦЕНТРА детали.
            // Центр детали на канвасе:
            double centerX = x0 + w / 2;
            double centerY = y0 + h / 2;

            foreach (var (position, hole) in holePositions)
            {
                double hx = centerX + position.X * scale;
                double hy = centerY + position.Y * scale;
                double hr = hole.Diameter / 2.0 * scale;

                canvas.Children.Add(new Ellipse
                {
                    Width = hr * 2,
                    Height = hr * 2,
                    Fill = HoleFillBrush,
                    Stroke = HoleBrush,
                    StrokeThickness = 1.5,
                    RenderTransform = new TranslateTransform(hx - hr, hy - hr)
                });
            }
        }

        #endregion

        #region ★ Размерные линии до гибов

        /// <summary>
        /// Рисует размерные линии от края детали до каждой линии гиба.
        /// </summary>
        private void DrawBendDimensions(Canvas canvas, double x0, double y0,
            double w, double h, double scale)
        {
            double bd = _data.BendCompensation;
            double dimY = y0 - DimOffset * MmToPx;  // над деталью

            // Размеры по X (горизонтальные, над деталью)
            if (_data.BendPositionsX.Count > 0)
            {
                double prevX = x0;
                int idx = 1;
                foreach (double pos in _data.BendPositionsX.OrderBy(p => p))
                {
                    if (pos <= 0 || pos >= _part.Width) continue;
                    double bendX = x0 + pos * scale;

                    // Размерная линия от предыдущего края/гиба до текущего гиба
                    DrawHorizontalDim(canvas, prevX, bendX, dimY, pos - (idx > 1 ? _data.BendPositionsX.OrderBy(p => p).ElementAt(idx - 2) : 0));
                    prevX = bendX;
                    idx++;
                }
                // Последний размер: от последнего гиба до правого края
                DrawHorizontalDim(canvas, prevX, x0 + w, dimY, _part.Width - _data.BendPositionsX.OrderBy(p => p).LastOrDefault());
            }

            // Размеры по Y (вертикальные, слева от детали)
            if (_data.BendPositionsY.Count > 0)
            {
                double dimX = x0 - DimOffset * MmToPx;  // слева от детали
                double prevY = y0;
                int idx = 1;
                foreach (double pos in _data.BendPositionsY.OrderBy(p => p))
                {
                    if (pos <= 0 || pos >= _part.Height) continue;
                    double bendY = y0 + pos * scale;

                    DrawVerticalDim(canvas, dimX, prevY, bendY, pos - (idx > 1 ? _data.BendPositionsY.OrderBy(p => p).ElementAt(idx - 2) : 0));
                    prevY = bendY;
                    idx++;
                }
                DrawVerticalDim(canvas, dimX, prevY, y0 + h, _part.Height - _data.BendPositionsY.OrderBy(p => p).LastOrDefault());
            }
        }

        private void DrawHorizontalDim(Canvas canvas, double x1, double x2, double y, double mmValue)
        {
            if (Math.Abs(x2 - x1) < 2) return;

            // ★ Толщина линии увеличена с 1.0 до 1.2
            canvas.Children.Add(new Line { X1 = x1, Y1 = y, X2 = x2, Y2 = y, Stroke = DimBrush, StrokeThickness = 1.2 });
            canvas.Children.Add(new Line { X1 = x1, Y1 = y - 4, X2 = x1, Y2 = y + 4, Stroke = DimBrush, StrokeThickness = 1.2 });
            canvas.Children.Add(new Line { X1 = x2, Y1 = y - 4, X2 = x2, Y2 = y + 4, Stroke = DimBrush, StrokeThickness = 1.2 });

            // ★ Размер шрифта увеличен с 9 до 12
            AddTextBlock(canvas, $"{mmValue:0}", (x1 + x2) / 2, y - 16, 12, DimBrush, centerAlign: true);
        }

        private void DrawVerticalDim(Canvas canvas, double x, double y1, double y2, double mmValue)
        {
            if (Math.Abs(y2 - y1) < 2) return;

            canvas.Children.Add(new Line { X1 = x, Y1 = y1, X2 = x, Y2 = y2, Stroke = DimBrush, StrokeThickness = 1.2 });
            canvas.Children.Add(new Line { X1 = x - 4, Y1 = y1, X2 = x + 4, Y2 = y1, Stroke = DimBrush, StrokeThickness = 1.2 });
            canvas.Children.Add(new Line { X1 = x - 4, Y1 = y2, X2 = x + 4, Y2 = y2, Stroke = DimBrush, StrokeThickness = 1.2 });

            // ★ Размер шрифта увеличен с 9 до 12
            var tb = CreateTextBlock($"{mmValue:0}", 12, DimBrush);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tb.RenderTransform = new RotateTransform(-90, 0, 0);
            Canvas.SetLeft(tb, x - 14);
            Canvas.SetTop(tb, (y1 + y2) / 2 + tb.DesiredSize.Width / 2);
            canvas.Children.Add(tb);
        }
        #endregion

        #region Габаритные размерные линии (исправленные)

        private void DrawWidthDimension(Canvas canvas, double x0, double bottomY,
            double w, double mmValue)
        {
            double y = bottomY + DimOffset * MmToPx;

            // ★ Толщина линии увеличена для баланса с крупным текстом
            canvas.Children.Add(new Line { X1 = x0, Y1 = y, X2 = x0 + w, Y2 = y, Stroke = DimBrush, StrokeThickness = 1.5 });
            canvas.Children.Add(new Line { X1 = x0, Y1 = y - 5, X2 = x0, Y2 = y + 5, Stroke = DimBrush, StrokeThickness = 1.5 });
            canvas.Children.Add(new Line { X1 = x0 + w, Y1 = y - 5, X2 = x0 + w, Y2 = y + 5, Stroke = DimBrush, StrokeThickness = 1.5 });
            canvas.Children.Add(new Line { X1 = x0, Y1 = bottomY + 2, X2 = x0, Y2 = y, Stroke = DimBrush, StrokeThickness = 0.8 });
            canvas.Children.Add(new Line { X1 = x0 + w, Y1 = bottomY + 2, X2 = x0 + w, Y2 = y, Stroke = DimBrush, StrokeThickness = 0.8 });

            // ★ Размер шрифта увеличен с 11 до 14
            AddTextBlock(canvas, $"{mmValue:0} мм", x0 + w / 2, y + 4, 14, DimBrush, centerAlign: true);
        }

        private void DrawHeightDimension(Canvas canvas, double rightX, double y0,
            double h, double mmValue)
        {
            double x = rightX + DimOffset * MmToPx;

            canvas.Children.Add(new Line { X1 = x, Y1 = y0, X2 = x, Y2 = y0 + h, Stroke = DimBrush, StrokeThickness = 1.5 });
            canvas.Children.Add(new Line { X1 = x - 5, Y1 = y0, X2 = x + 5, Y2 = y0, Stroke = DimBrush, StrokeThickness = 1.5 });
            canvas.Children.Add(new Line { X1 = x - 5, Y1 = y0 + h, X2 = x + 5, Y2 = y0 + h, Stroke = DimBrush, StrokeThickness = 1.5 });
            canvas.Children.Add(new Line { X1 = rightX + 2, Y1 = y0, X2 = x, Y2 = y0, Stroke = DimBrush, StrokeThickness = 0.8 });
            canvas.Children.Add(new Line { X1 = rightX + 2, Y1 = y0 + h, X2 = x, Y2 = y0 + h, Stroke = DimBrush, StrokeThickness = 0.8 });

            // ★ Размер шрифта увеличен с 11 до 14
            var tb = CreateTextBlock($"{mmValue:0} мм", 14, DimBrush);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            tb.RenderTransform = new RotateTransform(-90, 0, 0);
            Canvas.SetLeft(tb, x + 6);
            Canvas.SetTop(tb, y0 + h / 2 + tb.DesiredSize.Width / 2);
            canvas.Children.Add(tb);
        }

        private void DrawDiameterDimension(Canvas canvas, double cx, double cy,
            double r, double mmScale)
        {
            double mmValue = mmScale > 0 ? (r * 2) / mmScale : 0;
            double dx = r * Math.Cos(Math.PI / 4);
            double dy = r * Math.Sin(Math.PI / 4);

            canvas.Children.Add(new Line
            {
                X1 = cx - dx,
                Y1 = cy + dy,
                X2 = cx + dx,
                Y2 = cy - dy,
                Stroke = DimBrush,
                StrokeThickness = 1.5
            });

            // ★ Размер шрифта увеличен с 11 до 14
            AddTextBlock(canvas, $"⌀{mmValue:0}", cx + dx + 10, cy - dy - 12, 14, DimBrush);
        }
        #endregion

        #region Отрисовка произвольной формы

        private void DrawCustomView(Canvas canvas, double left, double top,
            double drawW, double drawH)
        {
            if (_data.CustomPoints.Count < 3) return;

            double minX = _data.CustomPoints.Min(p => p.X);
            double maxX = _data.CustomPoints.Max(p => p.X);
            double minY = _data.CustomPoints.Min(p => p.Y);
            double maxY = _data.CustomPoints.Max(p => p.Y);
            double cw = Math.Max(maxX - minX, 1);
            double ch = Math.Max(maxY - minY, 1);

            double scale = Math.Min(drawW / cw, drawH / ch);
            double w = cw * scale;
            double h = ch * scale;
            double ox = left + (drawW - w) / 2;
            double oy = top + (drawH - h) / 2;

            // Контур
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = TransformPoint(_data.CustomPoints[0], minX, minY, scale, ox, oy),
                IsClosed = true,
                IsFilled = true
            };
            for (int i = 1; i < _data.CustomPoints.Count; i++)
            {
                figure.Segments.Add(new LineSegment(
                    TransformPoint(_data.CustomPoints[i], minX, minY, scale, ox, oy), true));
            }
            geometry.Figures.Add(figure);

            canvas.Children.Add(new Path
            {
                Data = geometry,
                Fill = FillBrush,
                Stroke = ContourBrush,
                StrokeThickness = 2.5
            });

            // Отверстия
            foreach (var hole in _part.PlacedHoles)
            {
                var hp = TransformPoint(new Point(hole.X, hole.Y), minX, minY, scale, ox, oy);
                double hr = hole.Diameter / 2.0 * scale;
                canvas.Children.Add(new Ellipse
                {
                    Width = hr * 2,
                    Height = hr * 2,
                    Fill = HoleFillBrush,
                    Stroke = HoleBrush,
                    StrokeThickness = 1.5,
                    RenderTransform = new TranslateTransform(hp.X - hr, hp.Y - hr)
                });
            }

            // Габаритные размеры
            DrawWidthDimension(canvas, ox, oy + h, w, cw);
            DrawHeightDimension(canvas, ox + w, oy, h, ch);
        }

        private static Point TransformPoint(Point p, double minX, double minY,
            double scale, double ox, double oy)
        {
            return new Point(
                ox + (p.X - minX) * scale,
                oy + (p.Y - minY) * scale);
        }

        #endregion

        #region Отрисовка трубных деталей

        private void DrawPipeView(Canvas canvas, double left, double top,
            double drawW, double drawH)
        {
            double sectionW = drawW * 0.35;
            double sideW = drawW * 0.55;
            double gap = drawW * 0.10;

            double secX = left;
            double sideX = left + sectionW + gap;
            double midY = top + drawH / 2;

            // ─── Сечение профиля ───
            double secDim = Math.Max(Math.Max(_part.Width, _part.Height), 1);
            double secScale = Math.Min(sectionW / secDim, (drawH * 0.6) / secDim);
            double sw = _part.Width * secScale;
            double sh = Math.Max(_part.Height, _part.Width * 0.3) * secScale;
            double sx = secX + (sectionW - sw) / 2;
            double sy = midY - sh / 2;

            if (_part.PartType == PartType.RoundTube || _part.PartType == PartType.Circle)
            {
                double r = sw / 2;
                canvas.Children.Add(new Ellipse
                {
                    Width = sw,
                    Height = sw,
                    Fill = FillBrush,
                    Stroke = ContourBrush,
                    StrokeThickness = 2.5,
                    RenderTransform = new TranslateTransform(sx + sw / 2 - r, midY - r)
                });
            }
            else
            {
                canvas.Children.Add(new Rectangle
                {
                    Width = sw,
                    Height = sh,
                    Fill = FillBrush,
                    Stroke = ContourBrush,
                    StrokeThickness = 2.5,
                    RenderTransform = new TranslateTransform(sx, sy)
                });
            }

            // ─── Боковой вид ───
            double sideLen = Math.Max(_part.Length, 1);
            double sideScale = sideW / sideLen;
            double sideLenPx = sideLen * sideScale;
            double sideHPx = Math.Max(sh, 10);
            double sideY = midY - sideHPx / 2;

            canvas.Children.Add(new Rectangle
            {
                Width = sideLenPx,
                Height = sideHPx,
                Fill = FillBrush,
                Stroke = ContourBrush,
                StrokeThickness = 2.5,
                RenderTransform = new TranslateTransform(sideX, sideY)
            });

            // ─── Подпись типа ───
            AddTextBlock(canvas, GetPipeTypeName(_part.PartType),
                sx + sw / 2, sy - 30, 12, DimBrush, centerAlign: true);

            // ─── Размеры ───
            DrawWidthDimension(canvas, sideX, sideY + sideHPx, sideLenPx, sideLen);

            if (_part.PartType == PartType.RoundTube || _part.PartType == PartType.Circle)
            {
                DrawDiameterDimension(canvas, sx + sw / 2, midY, sw / 2, secScale);
            }
            else
            {
                DrawHeightDimension(canvas, sx + sw, sy, sh, _part.Height);
            }
        }

        #endregion

        #region Вспомогательные методы рисования

        /// <summary>
        /// Добавляет текстовую метку на Canvas.
        /// </summary>
        private static void AddTextBlock(Canvas canvas, string text, double x, double y,
            double fontSize, Brush color, bool centerAlign = false)
        {
            var textBlock = CreateTextBlock(text, fontSize, color);

            if (centerAlign)
            {
                // Измеряем текст для центрирования
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(textBlock, x - textBlock.DesiredSize.Width / 2);
                Canvas.SetTop(textBlock, y);
            }
            else
            {
                Canvas.SetLeft(textBlock, x);
                Canvas.SetTop(textBlock, y);
            }

            canvas.Children.Add(textBlock);
        }

        private static TextBlock CreateTextBlock(string text, double fontSize, Brush color)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = color,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI")
            };
        }

        #endregion

        #region Таблица отверстий

        private bool HasHoles()
        {
            if (_part.PartType == PartType.Custom)
                return _part.PlacedHoles.Count > 0;
            return _part.HoleGroups.Count > 0;
        }

        private void DrawHolesSection(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(6).Text("Отверстия").FontSize(13).SemiBold()
                      .FontColor(Colors.Blue.Darken2);

                column.Item().Table(table =>
                {
                    if (_part.PartType == PartType.Custom)
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(40);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("№").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Диаметр").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("X (мм)").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Y (мм)").SemiBold();
                        });

                        int num = 1;
                        foreach (var hole in _part.PlacedHoles)
                        {
                            table.Cell().Padding(4).Text($"{num++}");
                            table.Cell().Padding(4).Text($"⌀{hole.Diameter:0.#}");
                            table.Cell().Padding(4).Text($"{hole.X:0.0}");
                            table.Cell().Padding(4).Text($"{hole.Y:0.0}");
                        }
                    }
                    else
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(40);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("№").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Диаметр").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Кол-во").SemiBold();
                            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Расположение").SemiBold();
                        });

                        int num = 1;
                        foreach (var group in _part.HoleGroups)
                        {
                            table.Cell().Padding(4).Text($"{num++}");
                            table.Cell().Padding(4).Text($"⌀{group.Diameter:0.#} мм");
                            table.Cell().Padding(4).Text($"{group.Count} шт");
                            table.Cell().Padding(4).Text("Равномерно по сетке");
                        }
                    }
                });
            });
        }

        #endregion

        #region Информация о гибке

        private bool HasBends()
        {
            return _part.PartType == PartType.Rectangle &&
                   (_data.BendPositionsX.Count > 0 || _data.BendPositionsY.Count > 0);
        }

        private void DrawBendsSection(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().PaddingBottom(6).Text("Гибка").FontSize(13).SemiBold()
                      .FontColor(Colors.Blue.Darken2);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1);
                        cols.RelativeColumn(3);
                    });

                    table.Cell().Padding(4).Background(Colors.Grey.Lighten4)
                        .Text("Угол гиба").SemiBold();
                    table.Cell().Padding(4).Text($"{_data.BendAngle:0}°");

                    table.Cell().Padding(4).Background(Colors.Grey.Lighten4)
                        .Text("Вычет на гиб").SemiBold();
                    table.Cell().Padding(4).Text($"{_data.BendCompensation:F2} мм");

                    // ★ Позиции по ширине с направлениями
                    if (_data.BendPositionsX.Count > 0)
                    {
                        var dirsX = GetBendDirections(_data.ProfileX);
                        table.Cell().Padding(4).Background(Colors.Grey.Lighten4)
                            .Text("Позиции по ширине").SemiBold();

                        var parts = _data.BendPositionsX
                            .Where(p => p > 0 && p < _part.Width)
                            .Select((p, i) =>
                            {
                                bool isUp = i < dirsX.Count && dirsX[i];
                                return $"{p:0.0} мм {(isUp ? "▲" : "▼")}";
                            });
                        table.Cell().Padding(4).Text(string.Join("; ", parts));
                    }

                    // ★ Позиции по высоте с направлениями
                    if (_data.BendPositionsY.Count > 0)
                    {
                        var dirsY = GetBendDirections(_data.ProfileY);
                        table.Cell().Padding(4).Background(Colors.Grey.Lighten4)
                            .Text("Позиции по высоте").SemiBold();

                        var parts = _data.BendPositionsY
                            .Where(p => p > 0 && p < _part.Height)
                            .Select((p, i) =>
                            {
                                bool isUp = i < dirsY.Count && dirsY[i];
                                return $"{p:0.0} мм {(isUp ? "▲" : "▼")}";
                            });
                        table.Cell().Padding(4).Text(string.Join("; ", parts));
                    }
                });
            });
        }

        private void DrawSheetBendLines(Canvas canvas, double x0, double y0,
            double w, double h, double scale)
        {
            var dashArray = new DoubleCollection { 7, 4 };

            var directionsX = GetBendDirections(_data.ProfileX);
            var directionsY = GetBendDirections(_data.ProfileY);

            string angleText = $"{_data.BendAngle:0}°";

            // ─── Вертикальные линии гибов (по оси X) ───
            int idxX = 0;
            foreach (double pos in _data.BendPositionsX)
            {
                if (pos <= 0 || pos >= _part.Width) continue;
                double x = x0 + pos * scale;

                bool isUp = idxX < directionsX.Count && directionsX[idxX];
                var brush = isUp ? Brushes.Green : Brushes.Red;

                canvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = y0,
                    X2 = x,
                    Y2 = y0 + h,
                    Stroke = brush,
                    StrokeThickness = 1.8,
                    StrokeDashArray = dashArray
                });

                // ★ Подпись ВНУТРИ детали: слева от линии, центрирована по вертикали
                string label = isUp ? $"ВВЕРХ {angleText}" : $"ВНИЗ {angleText}";
                DrawBendLabelVertical(canvas, label, x, y0, h, brush);  // ★ передаём h

                idxX++;
            }

            // ─── Горизонтальные линии гибов (по оси Y) ───
            int idxY = 0;
            foreach (double pos in _data.BendPositionsY)
            {
                if (pos <= 0 || pos >= _part.Height) continue;
                double y = y0 + pos * scale;

                bool isUp = idxY < directionsY.Count && directionsY[idxY];
                var brush = isUp ? Brushes.Green : Brushes.Red;

                canvas.Children.Add(new Line
                {
                    X1 = x0,
                    Y1 = y,
                    X2 = x0 + w,
                    Y2 = y,
                    Stroke = brush,
                    StrokeThickness = 1.8,
                    StrokeDashArray = dashArray
                });

                // ★ Подпись ВНУТРИ детали: над линией, центрирована по горизонтали
                string label = isUp ? $"ВВЕРХ {angleText}" : $"ВНИЗ {angleText}";
                DrawBendLabelHorizontal(canvas, label, x0, y, w, brush);  // ★ передаём w

                idxY++;
            }
        }

        /// <summary>
        /// Рисует вертикальную надпись (повёрнутую на -90°) ВНУТРИ детали слева от линии гиба.
        /// Текст центрирован по вертикали относительно линии гиба.
        /// </summary>
        private void DrawBendLabelVertical(Canvas canvas, string text, double lineX, double topY,
            double detailHeight, Brush color)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,  // ★ увеличено с 9 до 12
                Foreground = color,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                Background = Brushes.White  // ★ белый фон для читаемости
            };

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // Поворачиваем текст на -90° (читается снизу вверх)
            textBlock.RenderTransform = new RotateTransform(-90, 0, 0);

            // ★ Центрируем по вертикали относительно линии гиба (середина высоты детали)
            double centerY = topY + detailHeight / 2;

            // После поворота на -90°: ширина текста становится высотой, высота — шириной
            // Расположение слева от линии (отступ 5px)
            Canvas.SetLeft(textBlock, lineX - textBlock.DesiredSize.Height - 5);
            Canvas.SetTop(textBlock, centerY - textBlock.DesiredSize.Width / 2);  // ★ центрирование по Y

            canvas.Children.Add(textBlock);
        }

        /// <summary>
        /// Рисует горизонтальную надпись ВНУТРИ детали над линией гиба.
        /// Текст центрирован по горизонтали относительно линии гиба.
        /// </summary>
        private void DrawBendLabelHorizontal(Canvas canvas, string text, double leftX, double lineY,
            double detailWidth, Brush color)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,  // ★ увеличено с 9 до 12
                Foreground = color,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                Background = Brushes.White  // ★ белый фон для читаемости
            };

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            // ★ Центрируем по горизонтали относительно линии гиба (середина ширины детали)
            double centerX = leftX + detailWidth / 2;

            // Расположение над линией (отступ 5px)
            Canvas.SetLeft(textBlock, centerX - textBlock.DesiredSize.Width / 2);  // ★ центрирование по X
            Canvas.SetTop(textBlock, lineY - textBlock.DesiredSize.Height - 5);

            canvas.Children.Add(textBlock);
        }

        /// <summary>
        /// Определяет направление каждого гиба по точкам профиля.
        /// Возвращает список: true = гиб вверх (к зрителю), false = гиб вниз (от зрителя).
        /// </summary>
        private static List<bool> GetBendDirections(BendProfileDrawing? profile)
        {
            var directions = new List<bool>();
            if (profile == null || !profile.IsFinished || profile.Points.Count < 3)
                return directions;

            var points = profile.Points;

            for (int i = 1; i < points.Count - 1; i++)
            {
                // Вектор предыдущего сегмента
                double v1x = points[i].X - points[i - 1].X;
                double v1y = points[i].Y - points[i - 1].Y;

                // Вектор следующего сегмента
                double v2x = points[i + 1].X - points[i].X;
                double v2y = points[i + 1].Y - points[i].Y;

                // Знак векторного произведения определяет направление поворота
                double cross = v1x * v2y - v1y * v2x;
                directions.Add(cross > 0);
            }

            return directions;
        }

        #endregion

        #region Вспомогательные

        private static bool IsPipePart(PartType type)
        {
            return type == PartType.RoundTube || type == PartType.RectangularTube ||
                   type == PartType.Angle || type == PartType.Channel || type == PartType.IBeam ||
                   type == PartType.Circle || type == PartType.SquareBar;
        }

        private static string GetPipeTypeName(PartType type) => type switch
        {
            PartType.RoundTube => "Круглая труба",
            PartType.RectangularTube => "Профильная труба",
            PartType.Angle => "Уголок",
            PartType.Channel => "Швеллер",
            PartType.IBeam => "Двутавр",
            PartType.Circle => "Круглый пруток",
            PartType.SquareBar => "Квадратный пруток",
            _ => "Профиль"
        };

        #endregion
    }

    #endregion
}