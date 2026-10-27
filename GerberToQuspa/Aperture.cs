using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GerberToQuspa
{
    /// <summary>
    /// Geometryヘルパークラス
    /// </summary>
    public static class GeometryHelper
    {
        /// <summary>
        /// 穴あけ処理の実行
        /// </summary>
        /// <param name="item"></param>
        /// <param name="center"></param>
        /// <param name="holeRadius"></param>
        /// <returns></returns>
        public static Geometry ExcludeHole(Geometry item, Point center, double holeRadius)
        {
            if (holeRadius <= 0)
            {
                return item;
            }

            var hole = new EllipseGeometry(center, holeRadius, holeRadius);
            var result = new CombinedGeometry(GeometryCombineMode.Exclude, item, hole);

            return result;
        }

    }

    /// <summary>
    /// アパーチャインターフェース
    /// </summary>
    public interface IAperture
    {
        string key { get; set; }
        void SetParameter(List<decimal> param, CoordinateTransformer transformer);
        double GetWidth();
        Geometry CreateGeometry(Point pos, bool isLPD);
        string CreatShowKey();

        void Draw(Point pos, StreamGeometryContext ctx);
    }

    /// <summary>
    /// 円アパーチャ
    /// </summary>
    public class ApertureCircle : IAperture
    {
        public string key { get; set; }
        private long Diameter { get; set; }
        private long HoleDiameter { get; set; }

        public void SetParameter(List<decimal> param, CoordinateTransformer transformer)
        {
            Diameter = transformer.ParseRealToInternal(param[0]) / 2;
            HoleDiameter = transformer.ParseRealToInternal(param.ElementAtOrDefault(1)) / 2;
        }

        public double GetWidth()
        {
            //return 1000;
            double w = (Diameter >= 1) ? Diameter * 2 : 1;
            return (w < 25)? 25 : w;
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            var EllipseGeo = new EllipseGeometry(pos, Diameter, Diameter);
            Geometry geo = GeometryHelper.ExcludeHole(EllipseGeo, pos, HoleDiameter);
            geo = geo.GetFlattenedPathGeometry();
            geo.Freeze();
            return geo;
        }

        public string CreatShowKey()
        {
            return $"{key} [{(double)Diameter / 1000:F3}×{(double)Diameter / 1000:F3}mm] Circle";
        }

        public void Draw(Point pos, StreamGeometryContext ctx)
        {
            Point start = new Point(pos.X + Diameter, pos.Y);

            ctx.BeginFigure(start, true, true); // filled & closed

            // 上半分
            ctx.ArcTo(
                new Point(pos.X - Diameter, pos.Y),
                new Size(Diameter, Diameter),
                0,
                false,
                SweepDirection.Clockwise,
                true,
                false);

            // 下半分
            ctx.ArcTo(
                start,
                new Size(Diameter, Diameter),
                0,
                false,
                SweepDirection.Clockwise,
                true,
                false);
        }
    }

    /// <summary>
    /// 長方形アパーチャ
    /// </summary>
    public class ApertureRect : IAperture
    {
        public string key { get; set; }
        public long Width { get; set; }
        public long Height { get; set; }
        private long HoleDiameter { get; set; }

        public void SetParameter(List<decimal> param, CoordinateTransformer transformer)
        {
            Width = transformer.ParseRealToInternal(param[0]);
            Height = transformer.ParseRealToInternal(param[1]);
            HoleDiameter = transformer.ParseRealToInternal(param.ElementAtOrDefault(2)) / 2;
        }

        public double GetWidth()
        {
            return 10;
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            // 左上座標を求める
            double left = pos.X - Width / 2.0;
            double top = pos.Y - Height / 2.0;
            // Rectを作成
            Rect rect = new Rect(left, top, Width, Height);

            var RectangleGeo = new RectangleGeometry(rect);

            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Geometry geo = GeometryHelper.ExcludeHole(RectangleGeo, center, HoleDiameter);
            geo.Freeze();

            return geo;
        }

        public string CreatShowKey()
        {
            return $"{key} [{(double)Width / 1000:F3}×{(double)Height / 1000:F3}mm] Rect";
        }

        public void Draw(Point pos, StreamGeometryContext ctx)
        {

        }
    }

    /// <summary>
    /// 楕円形アパーチャ
    /// </summary>
    public class ApertureObround : IAperture
    {
        public string key { get; set; }
        private long Width { get; set; }
        private long Height { get; set; }
        private long HoleDiameter { get; set; }

        public void SetParameter(List<decimal> param, CoordinateTransformer transformer)
        {
            Width = transformer.ParseRealToInternal(param[0]);
            Height = transformer.ParseRealToInternal(param[1]);
            HoleDiameter = transformer.ParseRealToInternal(param.ElementAtOrDefault(2)) / 2;
        }

        public double GetWidth()
        {
            return 10;
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            double r;
            Geometry rect;
            Geometry circle1;
            Geometry circle2;

            if (Width >= Height)
            {
                // 横長
                r = Height / 2;
                rect = new RectangleGeometry(new Rect(pos.X - Width / 2 + r, pos.Y - r, Width - Height, Height));
                circle1 = new EllipseGeometry(new Point(pos.X - Width / 2 + r, pos.Y), r, r);
                circle2 = new EllipseGeometry(new Point(pos.X + Width / 2 - r, pos.Y), r, r);
            }
            else
            {
                // 縦長
                r = Width / 2;
                rect = new RectangleGeometry(new Rect(pos.X - r, pos.Y - Height / 2 + r, Width, Height - Width));
                circle1 = new EllipseGeometry(new Point(pos.X, pos.Y - Height / 2 + r), r, r);
                circle2 = new EllipseGeometry(new Point(pos.X, pos.Y + Height / 2 - r), r, r);
            }

            // 3つをUnion
            GeometryGroup group = new GeometryGroup()
            {
                FillRule = FillRule.Nonzero
            };

            group.Children.Add(rect);
            group.Children.Add(circle1);
            group.Children.Add(circle2);

            Geometry geo = GeometryHelper.ExcludeHole(group, pos, HoleDiameter);
            geo.Freeze();

            return geo;
        }

        public string CreatShowKey()
        {
            return $"{key} [{(double)Width / 1000:F3}×{(double)Height / 1000:F3}mm] Oval";
        }

        public void Draw(Point pos, StreamGeometryContext ctx)
        {

        }
    }

    /// <summary>
    /// 多角形アパーチャ
    /// </summary>
    public class AperturePolygon : IAperture
    {
        public string key { get; set; }
        private long OuterDiameter { get; set; }
        private long Vertices { get; set; }
        private long Rotation { get; set; }
        private long HoleDiameter { get; set; }

        public void SetParameter(List<decimal> param, CoordinateTransformer transformer)
        {
            OuterDiameter = transformer.ParseRealToInternal(param[0]);
            Vertices = (long)param[1];
            Rotation = (long)param[2];
            HoleDiameter = transformer.ParseRealToInternal(param.ElementAtOrDefault(3)) / 2;
        }

        public double GetWidth()
        {
            return 10;
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            // 回転角度をラジアンに変換
            double rotationRad = Rotation * Math.PI / 180.0;

            // 頂点計算
            Point[] points = new Point[Vertices];
            for (int i = 0; i < Vertices; i++)
            {
                double angle = 2 * Math.PI * i / Vertices - Math.PI / 2 + rotationRad;
                points[i] = new Point(
                    pos.X + Rotation * Math.Cos(angle),
                    pos.Y + Rotation * Math.Sin(angle)
                );
            }

            // StreamGeometryで多角形を作成
            StreamGeometry StreamGeo = new StreamGeometry();
            using (StreamGeometryContext ctx = StreamGeo.Open())
            {
                ctx.BeginFigure(points[0], true, true);          // 塗りつぶしあり、閉じる
                ctx.PolyLineTo(points.Skip(1).ToList(), true, true);
            }

            Geometry geo = GeometryHelper.ExcludeHole(StreamGeo, pos, HoleDiameter);
            geo.Freeze();

            return geo;
        }

        public string CreatShowKey()
        {
            return $"{key} [{(double)Vertices}×{(double)OuterDiameter / 1000:F3}mm] Polygon";
        }

        public void Draw(Point pos, StreamGeometryContext ctx)
        {

        }
    }

    /// <summary>
    /// アパーチャマクロ
    /// </summary>
    public class ApertureMacro : IAperture
    {
        public string key { get; set; }
        private Dictionary<int, double> _macroVariables = new Dictionary<int, double>();
        private Macro Macro { get; set; }

        public ApertureMacro(Macro macro)
        {
            Macro = macro.Clone();
        }

        public void SetParameter(List<decimal> param, CoordinateTransformer ct)
        {
            for (int i = 0; i < param.Count(); i++)
            {
                _macroVariables.Add(i + 1, (double)param[i]);
            }
            Macro.ToInternalValue(_macroVariables, ct);
        }
       
        public double GetWidth()
        {
            return 10;
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            return Macro.CreateGeometry(pos, isLPD);
        }

        public string CreatShowKey()
        {
            Geometry geo = CreateGeometry(new Point(0, 0), true);
            Rect rect = geo.Bounds;
            return $"{key} [{rect.Width / 1000:F3}×{rect.Height / 1000:F3}mm] Macro";
        }

        public void Draw(Point pos, StreamGeometryContext ctx)
        {

        }
    }

    /// <summary>
    /// リージョンステートメント
    /// </summary>
    public class PolygonPoint
    {
        public InterpMode mode { get; set; }
        public long PointX { get; set; }
        public long PointY { get; set; }
        public long centerOffsetX { get; set; }
        public long centerOffsetY { get; set; }

        public Point CenterPos(Point currentPos)
        {
            return new Point(currentPos.X + centerOffsetX, currentPos.Y + centerOffsetY);
        }
    }

    public class ApertureRS : IAperture
    {
        public string key { get; set; }
        public IReadOnlyList<PolygonPoint> Points { get; set; }

        public void SetParameter(List<decimal> param, CoordinateTransformer transformer)
        {
        }

        public double GetWidth()
        {
            return 10;
        }

        public Geometry CreateGeometry(Point pos, bool isLPD)
        {
            Vector offset = new Vector(pos.X, pos.Y);

            // StreamGeometryで多角形を作成
            StreamGeometry geo = new StreamGeometry();
            geo.FillRule = FillRule.Nonzero;

            using (StreamGeometryContext ctx = geo.Open())
            {
                Point currentPos = new Point();
                for (int index = 0; index < Points.Count(); index++)
                {
                    PolygonPoint item = Points[index];
                    if (index == 0)
                    {
                        Point startPos = new Point(item.PointX + offset.X, item.PointY + offset.Y);
                        ctx.BeginFigure(startPos, true, true);          // 塗りつぶしあり、閉じる
                        currentPos = startPos;
                        continue;
                    }

                    switch (item.mode)
                    {
                        case InterpMode.Linear:
                            Point p = new Point(item.PointX + offset.X, item.PointY + offset.Y);
                            ctx.LineTo(p, true, false);
                            break;
                        case InterpMode.CWArc:
                        case InterpMode.CCWArc:
                            {
                                Point end = new Point(item.PointX + offset.X, item.PointY + offset.Y);
                                Point center = item.CenterPos(currentPos);

                                // 半径
                                double radius = Math.Sqrt(item.centerOffsetX * item.centerOffsetX + item.centerOffsetY * item.centerOffsetY);

                                // SweepDirection
                                SweepDirection sweep = (item.mode == InterpMode.CWArc) ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

                                bool isFullCircle = currentPos.X == end.X && currentPos.Y == end.Y;
                                if (isFullCircle)
                                {
                                    // 中間点（180度位置）
                                    Vector startVec = currentPos - center;
                                    Vector midVec = new Vector(-startVec.X, -startVec.Y);
                                    Point mid = center + midVec;

                                    // 1つ目 180°
                                    ctx.ArcTo(mid, new Size(radius, radius), 0, false, sweep, true, false);

                                    // 2つ目 180°
                                    ctx.ArcTo(end, new Size(radius, radius), 0, false, sweep, true, false);
                                }
                                else
                                {
                                    ctx.ArcTo(end, new Size(radius, radius), 0, false, sweep, true, false);
                                }                                
                                break;
                            }
                    }
                    currentPos = new Point(item.PointX + offset.X, item.PointY + offset.Y);
                }
            }
            geo.Freeze();
            return geo;
        }

        public string CreatShowKey()
        {
            Geometry geo = CreateGeometry(new Point(0, 0), true);
            Rect rect = geo.Bounds;
            return $"[{rect.Width / 1000:F3}×{rect.Height / 1000:F3}mm] Region";
        }

        public void Draw(Point pos, StreamGeometryContext ctx)
        {

        }
    }
}