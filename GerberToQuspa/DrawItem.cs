using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using ClipperLib;
using GerberToQuspa.ViewModel;

namespace GerberToQuspa
{
    /// <summary>
    /// Penキャッシュヘルパー
    /// </summary>
    public static class PenCacheHelper
    {
        /// <summary>
        /// Penキャッシュ
        /// </summary>
        private static readonly Dictionary<string, Pen> _penCache            = new Dictionary<string, Pen>();

        /// <summary>
        /// Pen取得
        /// </summary>
        public static Pen GetPen(Brush brush, double thickness, PenLineCap startCap, PenLineCap endCap)
        {
            string key = brush.ToString() + "_" + thickness.ToString() + "_" + startCap.ToString() + "_" + endCap.ToString();

            Pen pen;
            if (_penCache.TryGetValue(key, out pen))
            {
                return pen;
            }

            pen = new Pen(brush, thickness);
            pen.StartLineCap = startCap;
            pen.EndLineCap = endCap;

            pen.Freeze();

            _penCache[key] = pen;

            return pen;
        }

        /// <summary>
        /// キャッシュクリア
        /// </summary>
        public static void Clear()
        {
            _penCache.Clear();
        }
    }

    /// <summary>
    /// 描画アイテムインターフェース
    /// </summary>
    public interface IDrawItem
    {
        IAperture Aperture { get; set; }
        Geometry Geometry { get; set; }
        Pen Stroke { get; set; }

        void Draw(DrawingContext dc);
        void DrawSelect(DrawingContext dc);
        Rect GetBounds();
        List<Point> CreateDispensePoints(OperationModel operationModel);

        //void Draw(StreamGeometryContext ctx);
    }

    /// <summary>
    /// 線
    /// </summary>
    public class DrawLine : IDrawItem
    {
        public IAperture Aperture { get; set; } = null;
        public Geometry Geometry { get; set; }
        public Pen Stroke { get; set; }

        private Point Start { get; set; }
        private Point End { get; set; }

        public DrawLine(Point start, Point end, double width, bool isLPD)
        {
            Start = start;
            End = end;
            Stroke = PenCacheHelper.GetPen(isLPD ? Brushes.Green : Brushes.Black, width, PenLineCap.Round, PenLineCap.Round);

            CreateGeometry();
        }

        public void Draw(DrawingContext dc)
        {
            dc.DrawGeometry(null, Stroke, Geometry);
        }

        public void DrawSelect(DrawingContext dc)
        {
        }

        public Rect GetBounds()
        {
            return new Rect(Start, End);
        }

        public List<Point> CreateDispensePoints(OperationModel operationModel)
        {
            return null;
        }

        private void CreateGeometry()
        {
            Geometry = new LineGeometry(Start, End);
            Geometry.Freeze();
        }

        public void Draw(StreamGeometryContext ctx)
        {
            ctx.BeginFigure(Start, false, false);
            ctx.LineTo(End, true, false);
        }
    }

    /// <summary>
    /// 円弧
    /// </summary>
    public class DrawArcLine : IDrawItem
    {
        public IAperture Aperture { get; set; } = null;
        public Geometry Geometry { get; set; }
        public Pen Stroke { get; set; }

        private Point Start { get; set; }
        private Point End { get; set; }
        private Point Center { get; set; }
        private bool IsCWArc { get; set; }
        private double Radius { get; set; }

        public DrawArcLine(Point start, Point end, double I, double J, double width, bool isCWArc, bool isLPD)
        {
            Start = start;
            End = end;
            Radius = Math.Sqrt(I * I + J * J);
            Center = new Point(Start.X + I, Start.Y + J);
            IsCWArc = isCWArc;
            Stroke = PenCacheHelper.GetPen(isLPD ? Brushes.Green : Brushes.Black, width, PenLineCap.Round, PenLineCap.Round);
            CreateGeometry();
        }

        public void Draw(DrawingContext dc)
        {
            dc.DrawGeometry(null, Stroke, Geometry);
        }

        public void DrawSelect(DrawingContext dc)
        {
        }

        public Rect GetBounds()
        {
            return new Rect(Start, End);
        }

        public List<Point> CreateDispensePoints(OperationModel operationModel)
        {
            return null;
        }

        private void CreateGeometry()
        {
            // 始点と終点が同じ → 完全な円
            if (Start == End)
            {
                Geometry = new EllipseGeometry(
                    Center,
                    Radius,
                    Radius);

                Geometry.Freeze();
                return;
            }

            StreamGeometry geo = new StreamGeometry();
            using (StreamGeometryContext ctx = geo.Open())
            {
                ctx.BeginFigure(Start, false, false);

                // 始点・終点を中心座標系に変換
                Vector startVector = Start - Center;
                Vector endVector = End - Center;

                // 始点・終点角度
                double startAngle = Math.Atan2(startVector.Y, startVector.X);
                double endAngle = Math.Atan2(endVector.Y, endVector.X);

                // SweepDirection
                SweepDirection sweep = (IsCWArc) ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

                // ArcSegment作成
                ArcSegment arc = new ArcSegment(
                    End,
                    new Size(Radius, Radius),
                    0,          // 回転角度
                    false,
                    sweep,
                    true        // isStroked
                );

                ctx.ArcTo(arc.Point, arc.Size, arc.RotationAngle, arc.IsLargeArc, arc.SweepDirection, true, false);
            }

            Geometry = geo;
            Geometry.Freeze();
        }

        public void Draw(StreamGeometryContext ctx)
        {
            ctx.BeginFigure(Start, false, false);

            // 始点・終点を中心座標系に変換
            Vector startVector = Start - Center;
            Vector endVector = End - Center;

            // 始点・終点角度
            double startAngle = Math.Atan2(startVector.Y, startVector.X);
            double endAngle = Math.Atan2(endVector.Y, endVector.X);

            // SweepDirection
            SweepDirection sweep = (IsCWArc) ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

            // ArcSegment作成
            ArcSegment arc = new ArcSegment(
                End,
                new Size(Radius, Radius),
                0,          // 回転角度
                false,
                sweep,
                true        // isStroked
            );

            ctx.ArcTo(arc.Point, arc.Size, arc.RotationAngle, arc.IsLargeArc, arc.SweepDirection, true, false);
        }
    }

    /// <summary>
    /// パッド
    /// </summary>
    public class DrawPad : IDrawItem
    {
        public IAperture Aperture { get; set; } = null;
        public Geometry Geometry { get; set; }
        public Pen Stroke { get; set; }

        private Point Pos { get; set; }
        private Brush Fill { get; set; } = Brushes.Green;
 
        public DrawPad(IAperture aperture, Point pos, bool isLPD)
        {
            Aperture = aperture;
            Fill = isLPD ? Brushes.Green : Brushes.Black;
            Fill.Freeze();
            Stroke = PenCacheHelper.GetPen(isLPD ? Brushes.Green : Brushes.Black, 1, PenLineCap.Flat, PenLineCap.Flat);
            Stroke.Freeze();

            Geometry = Aperture.CreateGeometry(pos, isLPD);
            Geometry.Freeze();

            Pos = pos;
        }

        public DrawPad(ApertureRect rect, Point start, Point end, bool isLPD)
        {
            Aperture = rect;
            Fill = isLPD ? Brushes.Green : Brushes.Black;
            Fill.Freeze();
            Stroke = PenCacheHelper.GetPen(isLPD ? Brushes.Green : Brushes.Black, 1, PenLineCap.Flat, PenLineCap.Flat);
            Stroke.Freeze();

            Vector direction = end - start;
            if (direction.LengthSquared > 0)
            {
                direction.Normalize();

                double angleRad = Math.Atan2(direction.Y, direction.X);
                double angleDeg = angleRad * 180.0 / Math.PI;

                double halfWidth = rect.Width / 2.0;
                double halfHeight = rect.Height / 2.0;

                Point[] points = new Point[6];
                if (angleDeg >= 0 && angleDeg < 90)
                {
                    points[0] = start + new Vector(-halfWidth, -halfHeight);
                    points[1] = start + new Vector(-halfWidth, +halfHeight);
                    points[2] = end + new Vector(-halfWidth, +halfHeight);
                    points[3] = end + new Vector(+halfWidth, +halfHeight);
                    points[4] = end + new Vector(+halfWidth, -halfHeight);
                    points[5] = start + new Vector(+halfWidth, -halfHeight);
                }
                else if (angleDeg >= 90 && angleDeg < 180)
                {
                    points[0] = start + new Vector(+halfWidth, -halfHeight);
                    points[1] = start + new Vector(+halfWidth, +halfHeight);
                    points[2] = end + new Vector(+halfWidth, +halfHeight);
                    points[3] = end + new Vector(-halfWidth, +halfHeight);
                    points[4] = end + new Vector(-halfWidth, -halfHeight);
                    points[5] = start + new Vector(-halfWidth, -halfHeight);
                }
                else if (angleDeg >=-90 && angleDeg < 0)
                {
                    points[0] = start + new Vector(+halfWidth, +halfHeight);
                    points[1] = start + new Vector(-halfWidth, +halfHeight);
                    points[2] = end + new Vector(-halfWidth, +halfHeight);
                    points[3] = end + new Vector(-halfWidth, -halfHeight);
                    points[4] = end + new Vector(+halfWidth, -halfHeight);
                    points[5] = start + new Vector(+halfWidth, -halfHeight);
                }
                else // angleDeg >= 270 && angleDeg < 360
                {
                    points[0] = start + new Vector(-halfWidth, +halfHeight);
                    points[1] = start + new Vector(+halfWidth, +halfHeight);
                    points[2] = end + new Vector(+halfWidth, +halfHeight);
                    points[3] = end + new Vector(+halfWidth, -halfHeight);
                    points[4] = end + new Vector(-halfWidth, -halfHeight);
                    points[5] = start + new Vector(-halfWidth, -halfHeight);
                }

                // StreamGeometry
                StreamGeometry streamGeo = new StreamGeometry();
                using (StreamGeometryContext ctx = streamGeo.Open())
                {
                    ctx.BeginFigure(points[0], true, true);
                    ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
                }

                var pg = Geometry.Combine(streamGeo, Aperture.CreateGeometry(start, isLPD), GeometryCombineMode.Union, null);
                pg = Geometry.Combine(pg, Aperture.CreateGeometry(end, isLPD), GeometryCombineMode.Union, null);

                Geometry = pg;
                Geometry.Freeze();
            }
            Pos = end;
        }

        public void Draw(DrawingContext dc)
        {
            dc.DrawGeometry(Fill, Stroke, Geometry);
        }

        public void DrawSelect(DrawingContext dc)
        {
            Pen newPen = Stroke.Clone();
            Brush newBrush = Fill.Clone();
            if (Stroke.Brush == Brushes.Green)
            {
                newPen.Brush = Brushes.White;
                newBrush = Brushes.White;
            }
            dc.DrawGeometry(newBrush, newPen, Geometry);
        }

        public Rect GetBounds()
        {
            return Geometry.Bounds;
        }

        public List<Point> CreateDispensePoints(OperationModel operationModel)
        {
            //return null;
            return (operationModel.IsAutoPlace) ? AutoPlaceDispensePoints(operationModel) : ManualPlaceDispensePoints(operationModel);
        }

        /// <summary>
        /// 塗布点をパッド上に自動配置
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        private List<Point> AutoPlaceDispensePoints(OperationModel operationModel)
        {
            double pitchX = operationModel.Px * 1000;
            double pitchY = operationModel.Py * 1000;

            var patterns = new[]
            {
                Tuple.Create(0.0, 0.0),
                Tuple.Create(pitchX / 2, 0.0),
                Tuple.Create(0.0, pitchY / 2),
                Tuple.Create(pitchX / 2, pitchY / 2)
            };

            List<Point> best = null;
            int bestCount = 0;
            foreach (var p in patterns)
            {
                var circles = PlaceCircles(Geometry, operationModel.Size * 1000, pitchX, pitchY, p.Item1, p.Item2);
                if (circles.Count > bestCount)
                {
                    bestCount = circles.Count;
                    best = circles;
                }
            }

            // 中心合わせ
            Rect bounds = Geometry.Bounds;
            Point geometryCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            Point circlesCenter = new Point(best.Average(c => c.X), best.Average(c => c.Y));

            Vector offset = geometryCenter - circlesCenter;
            for (int i = 0; i < best.Count; i++)
            {
                Point p = best[i] + offset;
                // Y反転
                p.Y *= -1;
                best[i] = p;
            }

            return best;
        }

        /// <summary>
        /// パッド内に収まるかをチェック
        /// </summary>
        /// <param name="union"></param>
        /// <param name="radius"></param>
        /// <param name="pitchX"></param>
        /// <param name="pitchY"></param>
        /// <param name="offsetX"></param>
        /// <param name="offsetY"></param>
        /// <returns></returns>
        private List<Point> PlaceCircles(Geometry union, double radius, double pitchX, double pitchY, double offsetX, double offsetY)
        {
            var result = new List<Point>();

            Rect bounds = union.Bounds;

            for (double y = bounds.Top + offsetY; y <= bounds.Bottom; y += pitchY)
            {
                for (double x = bounds.Left + offsetX; x <= bounds.Right; x += pitchX)
                {
                    Point center = new Point(x, y);

                    // まず中心だけチェック（超高速）
                    if (!union.FillContains(center))
                    {
                        continue;
                    }

                    double r = radius * 0.707106; // √2/2
                    if (!union.FillContains(new Point(center.X + r, center.Y + r))) continue;
                    if (!union.FillContains(new Point(center.X - r, center.Y + r))) continue;
                    if (!union.FillContains(new Point(center.X + r, center.Y - r))) continue;
                    if (!union.FillContains(new Point(center.X - r, center.Y - r))) continue;

                    result.Add(center);
                }
            }
            return result;
        }

        /// <summary>
        /// 塗布点をパッド上に個数指定して配置
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        private List<Point> ManualPlaceDispensePoints(OperationModel operationModel)
        {
            Rect bounds = Geometry.Bounds;

            double pitchX = bounds.Width / operationModel.CountX;
            double pitchY = bounds.Height / operationModel.CountY;

            List<Point> dispensePoints = new List<Point>();
            for (int i = 0; i < operationModel.CountX; i++)
            {
                for (int j = 0; j < operationModel.CountY; j++)
                {
                    dispensePoints.Add(new Point(i * pitchX, j * pitchY));
                }
            }

            Point geometryCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            Point pointCenter = new Point(dispensePoints.Average(c => c.X), dispensePoints.Average(c => c.Y));

            Vector offset = geometryCenter - pointCenter;
            for (int i = 0; i < dispensePoints.Count; i++)
            {
                Point p = dispensePoints[i] + offset;
                // Y反転
                p.Y *= -1;
                dispensePoints[i] = p;
            }
            return dispensePoints;
        }

        public void Draw(StreamGeometryContext ctx)
        {
            Aperture.Draw(Pos, ctx);
        }
    }
}
