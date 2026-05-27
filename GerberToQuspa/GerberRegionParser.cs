using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace GerberToQuspa
{
    public static class PolygonUtil
    {
        public static bool IsCounterClockwise(IReadOnlyList<PolygonPoint> points)
        {
            long sum = 0;

            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];

                sum += (p2.PointX - p1.PointX) * (p2.PointY + p1.PointY);
            }
            return sum < 0;
        }

        public static int GetStartIndex(IReadOnlyList<PolygonPoint> points)
        {
            int index = 0;

            for (int i = 1; i < points.Count; i++)
            {
                bool smallerX = points[i].PointX < points[index].PointX;

                bool sameXSmallerY =
                    points[i].PointX ==
                    points[index].PointX &&
                    points[i].PointY <
                    points[index].PointY;

                if (smallerX || sameXSmallerY)
                {
                    index = i;
                }
            }
            return index;
        }
    }

    public class RegionInfo
    {
        public IReadOnlyList<PolygonPoint> OriginalPoints { get; set; }
        public IReadOnlyList<PolygonPoint> NormalizedPoints { get; set; }

        public Point Center { get; set; }
        public long MinX { get; set; }
        public long MaxX { get; set; }
        public long MinY { get; set; }
        public long MaxY { get; set; }
        public decimal AreaMm2 { get; set; }
        public bool IsCircle { get; set; }
        public bool HasArc { get; set; }
        public double Radius { get; set; }
        public string Key { get; set; }                
    }

    class GerberRegionParser
    {
        private readonly GerberFile file;
        private readonly CoordinateTransformer ct;

        public GerberRegionParser(GerberFile file, CoordinateTransformer ct)
        {
            this.file = file;
            this.ct = ct;
        }

        /// <summary>
        /// Region(G36/G37) を解析し、DrawPad を生成する。
        /// </summary>
        public List<DrawPad> Parse(ProgressUnit unit, ref long currentPosX, ref long currentPosY)
        {
            List<RegionInfo> regionList = AnalyzeRegion(unit, ref currentPosX, ref currentPosY);
            if (regionList.Count == 0)
            {
                return null;
            }

            List<DrawPad> psdList = new List<DrawPad>();
            foreach (var item in regionList)
            {
                IAperture aperture;
                if (!file.Apertures.TryGetValue(item.Key, out aperture))
                {
                    // 新規アパーチャとして保存しておく
                    aperture = new ApertureRS();
                    aperture.key = item.Key;
                    ((ApertureRS)aperture).Points = item.NormalizedPoints;
                    file.Apertures.Add(item.Key, aperture);
                }
                psdList.Add(new DrawPad(aperture, item.Center, true));
            }
            return psdList;
        }

        /// <summary>
        /// Region(G36/G37) を解析し、正規化済みRegion情報を生成する。
        /// </summary>
        private List<RegionInfo> AnalyzeRegion(ProgressUnit unit, ref long currentPosX, ref long currentPosY)
        {
            var pointsList = ReadPoints(unit, ref currentPosX, ref currentPosY);

            List<RegionInfo> regionList = new List<RegionInfo>();
            foreach (var item in pointsList)
            {
                RegionInfo region = new RegionInfo();
                region.OriginalPoints = item.AsReadOnly();
                CalcBounds(region);
                region.NormalizedPoints = NormalizePoints(item, region.Center).AsReadOnly();
                region.AreaMm2 = CalcPolygonArea(region.NormalizedPoints);
                region.IsCircle = IsCircle(region.NormalizedPoints);
                region.Key = BuildKey(region);

                regionList.Add(region);
            }
            return regionList;
        }
        
        /// <summary>
        /// Region読み込み
        /// </summary>
        /// <param name="currentPosX"></param>
        /// <param name="currentPosY"></param>
        /// <param name="unit"></param>
        /// <returns></returns>
        private List<List<PolygonPoint>> ReadPoints(ProgressUnit unit, ref long currentPosX, ref long currentPosY)
        {
            List<List<PolygonPoint>> regionList = new List<List<PolygonPoint>>();

            InterpMode mode = InterpMode.Linear;
            string str;
            List<PolygonPoint> points = new List<PolygonPoint>();
            do
            {
                str = unit.ReadStatement();
                if (str == null || str.StartsWith("G37"))
                {
                    regionList.Add(points);
                    break;
                }
                if (str.Contains("D02") || str.Contains("D2"))
                {
                    if(points.Count >= 2)
                    {
                        regionList.Add(points);
                    }
                    points = new List<PolygonPoint>();
                }

                if (str.Contains("G01") || str.Contains("G1")) mode = InterpMode.Linear;
                if (str.Contains("G02") || str.Contains("G2")) mode = InterpMode.CWArc;
                if (str.Contains("G03") || str.Contains("G3")) mode = InterpMode.CCWArc;

                var xMatch = Regex.Match(str, @"X(-?\d+)");
                var yMatch = Regex.Match(str, @"Y(-?\d+)");
                var iMatch = Regex.Match(str, @"I(-?\d+)");
                var jMatch = Regex.Match(str, @"J(-?\d+)");

                long x = xMatch.Success ? ct.ToInternalX(xMatch.Groups[1].Value) : currentPosX;
                long y = yMatch.Success ? ct.ToInternalY(yMatch.Groups[1].Value) : currentPosY;
                long i = iMatch.Success ? ct.ToInternalX(iMatch.Groups[1].Value) : 0;
                long j = jMatch.Success ? ct.ToInternalY(jMatch.Groups[1].Value) : 0;

                // 座標抽出                                    
                if (str.StartsWith("X") || str.StartsWith("Y") || str.StartsWith("G01") || str.StartsWith("G02") || str.StartsWith("G03") || str.StartsWith("G1") || str.StartsWith("G2") || str.StartsWith("G3"))
                {
                    PolygonPoint item = new PolygonPoint
                    {
                        mode = mode,
                        PointX = x,
                        PointY = y,
                        centerOffsetX = i,
                        centerOffsetY = j,
                    };
                    points.Add(item);
                }
                currentPosX = x;
                currentPosY = y;
            } while (true);

            return regionList;
        }

        /// <summary>
        /// BoundingBox計算
        /// </summary>
        /// <param name="region"></param>
        private void CalcBounds(RegionInfo region)
        {
            region.MinX = region.OriginalPoints.Min(p => p.PointX);
            region.MaxX = region.OriginalPoints.Max(p => p.PointX);
            region.MinY = region.OriginalPoints.Min(p => p.PointY);
            region.MaxY = region.OriginalPoints.Max(p => p.PointY);

            region.Center = new Point(CalcCenter(region.MinX, region.MaxX), CalcCenter(region.MinY, region.MaxY));
        }        
        private long CalcCenter(long min, long max)
        {
            long sum = min + max;
            return (sum >= 0) ? (sum + 1) / 2 : (sum - 1) / 2;
        }

        /// <summary>
        /// 正規化
        /// </summary>
        public List<PolygonPoint> NormalizePoints(List<PolygonPoint> points, Point center)
        {
            // 全点を移動
            var normalized = points.Select(p => new PolygonPoint
            {
                mode = p.mode,
                PointX = p.PointX - (long)center.X,
                PointY = p.PointY - (long)center.Y,
                centerOffsetX = p.centerOffsetX,
                centerOffsetY = p.centerOffsetY,
            })
            .ToList();

            if (PolygonUtil.IsCounterClockwise(normalized))
            {
                normalized.Reverse();
            }

            int startIndex = PolygonUtil.GetStartIndex(normalized);
            return Enumerable
                .Range(0, normalized.Count)
                .Select(i => normalized[(startIndex + i) % normalized.Count])
                .ToList();
        }

        /// <summary>
        /// 円を形成しているか？
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private bool IsCircle(IReadOnlyList<PolygonPoint> points)
        {
            if(points.Count != 2)
            {
                return false;
            }

            PolygonPoint startPoint = points[0];
            PolygonPoint arcPoint = points[1];

            if (arcPoint.mode != InterpMode.CWArc && arcPoint.mode != InterpMode.CCWArc)
                return false;

            Point endPoint = new Point(arcPoint.PointX, arcPoint.PointY);

            // 始点と終点が同じか
            if (startPoint.PointX != endPoint.X || startPoint.PointY != endPoint.Y)
            {
                return false;
            }

            // 半径計算
            double radius = Math.Sqrt(arcPoint.centerOffsetX * arcPoint.centerOffsetX + arcPoint.centerOffsetY * arcPoint.centerOffsetY);
            return (radius <= 0)? false : true;
        }

        /// <summary>
        /// Polygonの面積演算
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private decimal CalcPolygonArea(IReadOnlyList<PolygonPoint> points)
        {
            decimal area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                area += p1.PointX * p2.PointY - p2.PointX * p1.PointY;
            }
            return Math.Abs(area) / 2 / 1000 / 1000;
        }

        /// <summary>
        /// keyの生成
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public string BuildKey(RegionInfo region)
        {
            if (region.IsCircle)
            {
                return BuildCircleCircleKey(region.OriginalPoints);
            }
            else
            {
                return BuildCircleRegionKey(region);
            }
        }

        /// <summary>
        /// Circle key作成
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private string BuildCircleCircleKey(IReadOnlyList<PolygonPoint> points)
        {
            double radius = Math.Sqrt(
                points[1].centerOffsetX * points[1].centerOffsetX +
                points[1].centerOffsetY * points[1].centerOffsetY);

            return string.Format("RSC_R:{0:F2}:", radius / 1000);
        }

        /// <summary>
        /// Region key作成
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private string BuildCircleRegionKey(RegionInfo region)
        {
            decimal ax = (decimal)(region.MaxX - region.MinX) / 1000;

            decimal ay = (decimal)(region.MaxY - region.MinY) / 1000;

            var sb = new StringBuilder("RS_");

            sb.Append($"X:{ax:F3}:");

            sb.Append($"Y:{ay:F3}:");

            sb.Append($"Area:{region.AreaMm2:F2}:");

            sb.Append(string.Join(";", region.NormalizedPoints.Select(p => $"{p.PointX},{p.PointY}")));

            return sb.ToString();
        }      
    }
}
