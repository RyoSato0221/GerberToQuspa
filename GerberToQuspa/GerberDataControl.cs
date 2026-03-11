using GerberToQuspa.View;
using GerberToQuspa.ViewModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GerberToQuspa
{
    public enum MoveMode { Move, Draw, Flash }
    public enum InterpMode { None, Linear, CWArc, CCWArc }

    /// <summary>
    /// 座標変換クラス
    /// </summary>
    public class CoordinateTransformer
    {
        public enum eParseType { X, Y, Size }
        public int UnitScale { get; set; }

        private bool LeadingZeros { get; set; } = true;         // 先頭ゼロ省略
        private bool AbsoluteCoordinates { get; set; } = true;  // 絶対座標
        private int IntX { get; set; } = 6;                     // X座標整数桁数
        private int DecX { get; set; } = 6;                     // X座標少数桁数
        private int IntY { get; set; } = 6;                     // Y座標整数桁数
        private int DecY { get; set; } = 6;                     // Y座標少数桁数
        private int BaseDec { get; set; }                       // 基準小数点桁数

        private int _errCount = 0;                              // 桁数不一致の個数

        /// <summary>
        /// 単位設定
        /// </summary>
        /// <param name="line"></param>
        public void SetScale(string line)
        {
            //decimal scale = (line.Contains("MM")) ? 1.0m : 25.4m;
            //for (int i = 0; i < BaseDec; i++)
            //{
            //    scale *= 10;
            //}
            //UnitScale = (int)scale;
            UnitScale = (line.Contains("MM")) ? 1000 : 25400;
        }

        /// <summary>
        /// フォーマット設定
        /// </summary>
        /// <param name="line"></param>
        public void SetFormat(string line)
        {
            // L/T判定
            LeadingZeros = line.Contains("L");
            // A/I判定
            AbsoluteCoordinates = line.Contains("A");
            // XとYの整数・小数桁数
            var match = Regex.Match(line, @"X(\d)(\d)Y(\d)(\d)");
            if (match.Success)
            {
                IntX = int.Parse(match.Groups[1].Value);
                DecX = int.Parse(match.Groups[2].Value);
                IntY = int.Parse(match.Groups[3].Value);
                DecY = int.Parse(match.Groups[4].Value);
                BaseDec = Math.Max(DecX, DecY);
            }
        }

        /// <summary>
        /// X座標の変換
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public long ToInternalX(string str)
        {
            return ToInternal(str, IntX, DecX);
        }

        /// <summary>
        /// Y座標の変換
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public long ToInternalY(string str)
        {
            return ToInternal(str, IntY, DecY);
        }

        /// <summary>
        /// 実数値の変換
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public long ParseRealToInternal(decimal d)
        {
            decimal buf = d * UnitScale;
            return (long)(buf);
        }

        /// <summary>
        /// 座標変換
        /// </summary>
        /// <param name="coordStr"></param>
        /// <param name="intDigits"></param>
        /// <param name="decDigits"></param>
        /// <returns></returns>
        private long ToInternal(string coordStr, int intDigits, int decDigits)
        {
            string sign = "";

            // ゼロ省略対応
            if (LeadingZeros)
            {
                // 符号を判定                
                if (coordStr.StartsWith("-"))
                {
                    sign = "-";
                    coordStr = coordStr.Substring(1);
                }
                // 必要桁に満たない場合は前ゼロ補完
                coordStr = coordStr.PadLeft(intDigits + decDigits, '0');
            }
            else
            {
                // T=末尾ゼロ省略の場合は右にゼロ補完
                coordStr = coordStr.PadRight(intDigits + decDigits, '0');
            }

            // フォーマットエラーは自動で修正する
            int total = intDigits + decDigits;
            if (coordStr.Length > total)
            {
                intDigits = coordStr.Length - decDigits;
                _errCount++;
            }

            string intPart = coordStr.Substring(0, intDigits);
            string decPart = coordStr.Substring(intDigits, decDigits);
            // 符号を戻す
            intPart = sign + intPart;

            decimal value = decimal.Parse(intPart + "." + decPart, CultureInfo.InvariantCulture);
            long internalValue = ParseRealToInternal(value);
            return internalValue;
        }

        /// <summary>
        /// 桁数不一致があればエラーメッセージを出す
        /// </summary>
        public void ShowErrMsg()
        {
            if(_errCount != 0)
            {
                // UIスレッドで実行
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var msg = new CustomMessageBoxView(enumInfoState.Warning, eMessageStyle.OK, "Getber警告", "座標データの桁数不一致を検出しました。", $"{_errCount}個の座標を自動修正して読み込みました。");
                    msg.ShowDialog();
                });
            }
        }
    }

    /// <summary>
    /// ガーバーデータ解析クラス
    /// </summary>
    class GerberDataControl
    {
        public class GerberFile
        {
            public Dictionary<string, IAperture> Apertures { get; } = new Dictionary<string, IAperture>();
            public List<Macro> Macros { get; } = new List<Macro>();
        }

        public class ProgressUnit
        {
            public StreamReader _reader;
            public IProgress<ProgressReportMessage> Progress;
            private ProgressReportMessage reportMessage;
            private StringBuilder _buffer = new StringBuilder();
            private long _totalLength;     // ファイル全体サイズ
            private long _readLength;      // 現在の読み込み量

            public event Action<double> ProgressChanged;

            public ProgressUnit(FileStream fs, StreamReader reader, IProgress<ProgressReportMessage> progress)
            {
                _totalLength = fs.Length;                
                _reader = reader;
                Progress = progress;
                reportMessage = new ProgressReportMessage();
                reportMessage.Message = "ガーバーデータ解析中";
            }

            public string ReadStatement()
            {
                _buffer.Clear();

                int firstChar = -1;

                while (true)
                {
                    int ch = _reader.Read();

                    if (ch == -1) // EOF
                    {
                        if (_buffer.Length == 0)
                            return null;

                        return _buffer.ToString().Trim();
                    }

                    _readLength++;   // ← ここでカウント

                    // 進捗報告
                    if (_readLength % 100 == 0)
                    {
                        reportMessage.Percent = (double)_readLength / _totalLength * 100.0;
                        Progress.Report(reportMessage);
                    }

                    char c = (char)ch;

                    if (c == '\r' || c == '\n')
                        continue;

                    if (firstChar == -1)
                        firstChar = c;

                    _buffer.Append(c);

                    if (firstChar != '%')
                    {
                        if (c == '*')
                        {
                            _buffer.Length--;
                            return _buffer.ToString().Trim();
                        }
                    }
                    else
                    {
                        int len = _buffer.Length;
                        if (len >= 2 &&
                            _buffer[len - 2] == '*' &&
                            _buffer[len - 1] == '%')
                        {
                            _buffer.Length -= 2;
                            return _buffer.ToString().Trim();
                        }
                    }
                }
            }            
        }

        private GerberFile file = new GerberFile();
        private int currentDCode;
        private CoordinateTransformer CoordinateTransformer = new CoordinateTransformer();

        public string FilePath { get; set; }

        /// <summary>
        /// ガーバーデータの解析開始
        /// </summary>
        /// <param name="progress"></param>
        /// <param name="token"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public async Task<GerberAnalysisData> AnalysisStarts(IProgress<ProgressReportMessage> progress)
        {
            // ファイル読み込みの実行
            GerberAnalysisData AnalysisData = await Task.Run(() => ReadFile(progress));
            CoordinateTransformer.ShowErrMsg();
            return AnalysisData;
        }

        /// <summary>
        /// ファイル読み込み
        /// </summary>       
        public async Task<GerberAnalysisData> ReadFile(IProgress<ProgressReportMessage> progress)
        {
            GerberAnalysisData AnalysisData = new GerberAnalysisData();
           
            long currentPosX = 0;
            long currentPosY = 0;

            InterpMode currentInterp = InterpMode.Linear;
            MoveMode currentMode = MoveMode.Draw;
            bool isLPD = true;

            file = new GerberFile();

            using (var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fs))
            {
                var unit = new ProgressUnit(fs, reader, progress);

                string line;
                while ((line = unit.ReadStatement()) != null)
                {
                    // 単位設定
                    if (line.StartsWith("%MO"))
                    {
                        CoordinateTransformer.SetScale(line);
                        continue;
                    }

                    // フォーマット定義
                    if (line.StartsWith("%FS"))
                    {
                        CoordinateTransformer.SetFormat(line);
                        continue;
                    }

                    // アパーチャ定義
                    if (line.StartsWith("%AD"))
                    {
                        ParseAperture(line);
                        continue;
                    }

                    // アパーチャマクロ定義
                    if (line.StartsWith("%AM"))
                    {
                        var apertureMacro = ParseApertureMacro(line);
                        if (apertureMacro != null) file.Macros.Add(apertureMacro);
                        continue;
                    }

                    // Dコードの切替
                    if (line.StartsWith("D") || line.StartsWith("G54"))
                    {
                        var dMatch = Regex.Match(line, @"D(\d+)");
                        currentDCode = int.Parse(dMatch.Groups[1].Value);
                        continue;
                    }

                    // リージョンステートメント
                    if (line.StartsWith("G36"))
                    {
                        ParseRegionStatement(ref currentPosX, ref currentPosY, ref AnalysisData, unit);
                        continue;
                    }

                    // Dコードの切替
                    if (line.StartsWith("%LP"))
                    {
                        isLPD = (line.Contains("D"));
                        continue;
                    }

                    // Dコード
                    if (line.Contains("D01") || line.Contains("D1")) currentMode = MoveMode.Draw;
                    if (line.Contains("D02") || line.Contains("D2")) currentMode = MoveMode.Move;
                    if (line.Contains("D03") || line.Contains("D3")) currentMode = MoveMode.Flash;

                    // Gコード
                    if (line.Contains("G01") || line.Contains("G1")) currentInterp = InterpMode.Linear;
                    if (line.Contains("G02") || line.Contains("G2")) currentInterp = InterpMode.CWArc;
                    if (line.Contains("G03") || line.Contains("G3")) currentInterp = InterpMode.CCWArc;

                    // 座標抽出                                    
                    if (line.StartsWith("X") || line.StartsWith("Y") || line.StartsWith("G01") || line.StartsWith("G02") || line.StartsWith("G03") || line.StartsWith("G1") || line.StartsWith("G2") || line.StartsWith("G3"))
                    {
                        var xMatch = Regex.Match(line, @"X(-?\d+)");
                        var yMatch = Regex.Match(line, @"Y(-?\d+)");
                        var iMatch = Regex.Match(line, @"I(-?\d+)");
                        var jMatch = Regex.Match(line, @"J(-?\d+)");

                        if (!xMatch.Success && !yMatch.Success && !iMatch.Success && !jMatch.Success)
                        {
                            continue;
                        }

                        long x = xMatch.Success ? CoordinateTransformer.ToInternalX(xMatch.Groups[1].Value) : currentPosX;
                        long y = yMatch.Success ? CoordinateTransformer.ToInternalY(yMatch.Groups[1].Value) : currentPosY;
                        long i = iMatch.Success ? CoordinateTransformer.ToInternalX(iMatch.Groups[1].Value) : 0;
                        long j = jMatch.Success ? CoordinateTransformer.ToInternalY(jMatch.Groups[1].Value) : 0;

                        if (currentDCode != null)
                        {
                            IAperture aperture;
                            if (file.Apertures.TryGetValue($"D:{currentDCode}", out aperture))
                            {
                            }
                            else
                            {
                            }

                            if (currentMode == MoveMode.Draw)
                            {
                                if (currentInterp == InterpMode.Linear)
                                {
                                    AnalysisData.DrawItems.Add(new DrawLine(new Point(currentPosX, currentPosY), new Point(x, y), aperture.GetWidth(), isLPD));
                                }
                                else if (currentInterp == InterpMode.CWArc)
                                {
                                    AnalysisData.DrawItems.Add(new DrawArcLine(new Point(currentPosX, currentPosY), new Point(x, y), i, j, aperture.GetWidth(), true, isLPD));
                                }
                                else if (currentInterp == InterpMode.CCWArc)
                                {
                                    AnalysisData.DrawItems.Add(new DrawArcLine(new Point(currentPosX, currentPosY), new Point(x, y), i, j, aperture.GetWidth(), false, isLPD));
                                }
                            }
                            else if (currentMode == MoveMode.Flash)
                            {
                                IDrawItem item;
                                item = new DrawPad(aperture, new Point(x, y), isLPD);
                                AnalysisData.DrawItems.Add(item);
                            }
                        }
                        currentPosX = x;
                        currentPosY = y;
                    }
                }
            }
            AnalysisData.CreareContentBounds();
            return AnalysisData;
        }

        /// <summary>
        /// アパーチャの解析
        /// </summary>
        /// <param name="line"></param>
        private void ParseAperture(string item)
        {
            var match = Regex.Match(item, @"^%ADD(?<dcode>\d+)(?<name>[^,]+)(?:,(?<param>.*))?$");
            if (!match.Success) return;

            IAperture aperture;
            switch (match.Groups[2].Value)
            {
                case "C":   // 円アパーチャ
                    aperture = new ApertureCircle();
                    break;
                case "R":   // 長方形アパーチャ
                    aperture = new ApertureRect();
                    break;
                case "O":   // 楕円形アパーチャ
                    aperture = new ApertureObround();
                    break;
                case "P":   // 多角形アパーチャ
                    aperture = new AperturePolygon();
                    break;
                default:    // アパーチャマクロ
                    var macro = file.Macros.FirstOrDefault(p => p.Name == match.Groups[2].Value);
                    aperture = new ApertureMacro(macro);
                    break;
            }
            List<decimal> paramList = match.Groups[3].Value
                .Split(new char[] { 'X', 'x', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => decimal.Parse(s, CultureInfo.InvariantCulture))
                .ToList();
            aperture.SetParameter(paramList, CoordinateTransformer);
            aperture.key = $"D:{int.Parse(match.Groups[1].Value)}";

            file.Apertures.Add(aperture.key, aperture);
        }

        /// <summary>
        /// アパーチャマクロの解析
        /// </summary>
        /// <param name="item"></param>
        /// <param name="parts"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private Macro ParseApertureMacro(string item)
        {
            // マクロを分割
            string[] parts = item.Split('*');

            var match = Regex.Match(parts[0], @"%AM(.+)");
            if (!match.Success) return null;

            Macro macro = new Macro();
            macro.Name = match.Groups[1].Value;

            IPrimitive primitive = null;
            foreach (string part in parts)
            {
                if (part.StartsWith("G04"))
                {
                    continue;   // コメント行はスキップ
                }

                List<string> buf = part.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();


                if (part.StartsWith("0"))           // コメント
                {
                    continue;
                }
                else if (part.StartsWith("1"))           // 円プリミティブ
                {
                    primitive = new CirclePrimitive();
                }
                else if (part.StartsWith("20"))         // ベクターラインプリミティブ
                {
                    primitive = new VectorLinePrimitive();
                }
                else if (part.StartsWith("21"))         // センターラインプリミティブ
                {
                    primitive = new CenterLinePrimitive();
                }
                else if (part.StartsWith("4"))          // アウトラインプリミティブ
                {
                    primitive = new OutLinePrimitive();
                }
                else if (part.StartsWith("5"))      
                {
                }
                else if (part.StartsWith("7"))          // サーマルパッド（ドーナッツ状パッド）
                {
                    primitive = new ThermalPrimitive();
                }
                else if(part.StartsWith("$"))           // 逐次変数評価
                {

                }

                if (primitive != null)
                {
                    primitive.SetParameter(buf, CoordinateTransformer);
                    macro.Primitives.Add(primitive);
                }

                //if (buf.Count() > 0)
                //{
                //    switch (buf[0])
                //    {
                //        case "0":   // コメント
                //            break;
                //        case "1":   // 円プリミティブ
                //            primitive = new CirclePrimitive();
                //            break;
                //        case "20":  // ベクターラインプリミティブ
                //            primitive = new VectorLinePrimitive();
                //            break;
                //        case "21":  // センターラインプリミティブ
                //            primitive = new CenterLinePrimitive();
                //            break;
                //        case "4":   // アウトラインプリミティブ
                //            primitive = new OutLinePrimitive();
                //            break;
                //        case "5":
                //            break;
                //        case "7":   // サーマルパッド（ドーナッツ状パッド）
                //            primitive = new ThermalPrimitive();
                //            break;
                //    }
                //    if (primitive != null)
                //    {
                //        primitive.SetParameter(buf, CoordinateTransformer);
                //        macro.Primitives.Add(primitive);
                //    }
                //}
            }
            return macro;
        }

        /// <summary>
        /// リージョンステートメントの解析
        /// </summary>        
        /// <param name="parts"></param>
        /// <param name="index"></param>
        private void ParseRegionStatement(ref long currentPosX, ref long currentPosY, ref GerberAnalysisData data, ProgressUnit unit)
        {
            InterpMode mode = InterpMode.Linear;
            string str;
            List<PolygonPoint> points = new List<PolygonPoint>();
            do
            {
                str = unit.ReadStatement();
                if (str == null || str.StartsWith("G37"))
                {
                    break;
                }
                if (str.Contains("G01") || str.Contains("G1")) mode = InterpMode.Linear;
                if (str.Contains("G02") || str.Contains("G2")) mode = InterpMode.CWArc;
                if (str.Contains("G03") || str.Contains("G3")) mode = InterpMode.CCWArc;

                var xMatch = Regex.Match(str, @"X(-?\d+)");
                var yMatch = Regex.Match(str, @"Y(-?\d+)");
                var iMatch = Regex.Match(str, @"I(-?\d+)");
                var jMatch = Regex.Match(str, @"J(-?\d+)");

                long x = xMatch.Success ? CoordinateTransformer.ToInternalX(xMatch.Groups[1].Value) : currentPosX;
                long y = yMatch.Success ? CoordinateTransformer.ToInternalY(yMatch.Groups[1].Value) : currentPosY;
                long i = iMatch.Success ? CoordinateTransformer.ToInternalX(iMatch.Groups[1].Value) : 0;
                long j = jMatch.Success ? CoordinateTransformer.ToInternalY(jMatch.Groups[1].Value) : 0;

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

            // 中心（重心）
            long minX = points.Min(p => p.PointX);
            long maxX = points.Max(p => p.PointX);
            long minY = points.Min(p => p.PointY);
            long maxY = points.Max(p => p.PointY);

            long sumX = minX + maxX;
            long cx = (sumX >= 0) ? (sumX + 1) / 2 : (sumX - 1) / 2;
            long sumY = minY + maxY;
            long cy = (sumY >= 0) ? (sumY + 1) / 2 : (sumY - 1) / 2;

            // 全点を移動
            for (int i = 0; i < points.Count; i++)
            {
                points[i].PointX -= cx;
                points[i].PointY -= cy;
            }

            // keyの作成
            string key = "";
            if(points.Count < 2)
            {                
                return; // 無効なケース
            }
            else if (points.Count == 2)
            {
                if(IsFullCircle(points[1], points[0]))
                {
                    double radius = Math.Sqrt(points[1].centerOffsetX * points[1].centerOffsetX + points[1].centerOffsetY * points[1].centerOffsetY);
                    var sb = new StringBuilder("RSC_");
                    sb.Append($"R:{radius / 1000:F2}:");
                    key = sb.ToString();
                }
                else
                {
                    return; // 無効なケース
                }
            }
            else
            {
                // key用に面積を算出
                decimal area = 0;
                for (int i = 0; i < points.Count; i++)
                {
                    var p1 = points[i];
                    var p2 = points[(i + 1) % points.Count];
                    area += p1.PointX * p2.PointY - p2.PointX * p1.PointY;
                }
                decimal area_mm2 = Math.Abs(area) / 2 / 1000 / 1000;

                // key生成
                decimal ax = (decimal)(maxX - minX) / 1000;
                decimal ay = (decimal)(maxY - minY) / 1000;
                var sb = new StringBuilder("RS_");
                sb.Append($"X:{ax:F3}:");
                sb.Append($"Y:{ay:F3}:");
                sb.Append($"Area:{area_mm2:F2}:");
                key = sb.ToString();
            }

            IAperture aperture;
            if (!file.Apertures.TryGetValue(key, out aperture))
            {
                // 新規アパーチャとして保存しておく
                aperture = new ApertureRS();
                aperture.key = key;
                ((ApertureRS)aperture).points = points;
                file.Apertures.Add(key, aperture);
            }
            data.DrawItems.Add(new DrawPad(aperture, new Point(cx, cy), true));
        }

        /// <summary>
        /// リージョンステートメントで円を形成している？
        /// </summary>
        /// <param name="arcPoint"></param>
        /// <param name="startPoint"></param>
        /// <returns></returns>
        private bool IsFullCircle(PolygonPoint arcPoint, PolygonPoint startPoint)
        {
            if (arcPoint.mode != InterpMode.CWArc && arcPoint.mode != InterpMode.CCWArc)
                return false;

            Point endPoint = new Point(arcPoint.PointX, arcPoint.PointY);

            // 始点と終点が同じか
            if (startPoint.PointX != endPoint.X || startPoint.PointY != endPoint.Y)
                return false;

            // 半径計算
            double radius = Math.Sqrt(arcPoint.centerOffsetX * arcPoint.centerOffsetX + arcPoint.centerOffsetY * arcPoint.centerOffsetY);

            if (radius <= 0)
                return false;

            return true;
        }

        public List<IAperture> GetApertures()
        {
            return file.Apertures.Values.ToList();
        }

        public IAperture GetAperture(string key)
        {
            IAperture aperture;
            file.Apertures.TryGetValue(key, out aperture);
            return aperture;
        }
    }

    /// <summary>
    /// 解析済みガーバーデータ
    /// </summary>
    public class GerberAnalysisData
    {        
        public List<IDrawItem> DrawItems = new List<IDrawItem>();
        public List<IDrawItem> SelectItems = new List<IDrawItem>();
                   
        private Rect _contentBounds = Rect.Empty;
        public Rect ContentBounds => _contentBounds;

        public DrawingVisual CreateGerberVisual()
        {            
            var dv = new DrawingVisual();

            using (var dc = dv.RenderOpen())
            {
                //var sg = new StreamGeometry();

                //dc.PushTransform(new TranslateTransform(-ContentBounds.BottomLeft.X, ContentBounds.BottomLeft.Y));
                //dc.PushTransform(new ScaleTransform(1, -1));
                //dc.PushGuidelineSet(new GuidelineSet());
                //using (var ctx = sg.Open())
                //{
                //    foreach (var item in DrawItems)
                //    {
                //        item.Draw(ctx);
                //    }
                //}

                //sg.Freeze();

                //Pen pen = new Pen(Brushes.Green, 1);
                //pen.Freeze();

                //dc.DrawGeometry(null, pen, sg);   // ← 1回だけ
                //dc.Pop();
                //dc.Pop();
                //dc.Pop();

                dc.PushTransform(new TranslateTransform(-ContentBounds.BottomLeft.X, ContentBounds.BottomLeft.Y));
                dc.PushTransform(new ScaleTransform(1, -1));

                foreach (var item in DrawItems)
                {
                    item.Draw(dc);
                }

                dc.Pop();
                dc.Pop();
            }

            return dv;
        }
                
        /// <summary>
        /// 描画エリアの生成
        /// </summary>
        public void CreareContentBounds()
        {
            _contentBounds = Rect.Empty;
            foreach (var item in DrawItems)
            {
                _contentBounds.Union(item.GetBounds()); // 各要素のバウンディングを統合                
            }
            // 5％ずつ余白を設ける
            _contentBounds.Inflate(_contentBounds.Width * 0.05, _contentBounds.Height * 0.05);
        }
        
        public List<Point> GetPointsInRegion(OperationModel operationModel)
        {
            List<Point> list = new List<Point>();
            foreach (var item in SelectItems)
            {
                List<Point> buf = item.CreateDispensePoints(operationModel);
                if (buf != null)
                {
                    Point offset = ContentBounds.BottomLeft;
                    foreach (var pos in buf)
                    {
                        pos.Offset(-offset.X, offset.Y);
                        list.Add(new Point { X = pos.X, Y = pos.Y });
                    }
                }
            }
            return list;
        }

        public void SetSelectItems(Rect area)
        {
            SelectItems = new List<IDrawItem>();
            foreach (var item in DrawItems)
            {
                if (item is DrawLine || item is DrawArcLine)
                {
                    continue;
                }

                Rect rect = item.GetBounds();
                if (rect.IntersectsWith(area))
                {
                    SelectItems.Add(item);
                }
            }
        }

        public void SetSelectItems(IAperture aperture)
        {
            SelectItems = new List<IDrawItem>();
            foreach (var item in DrawItems)
            {                
                if(item.Aperture == aperture)
                {
                    SelectItems.Add(item);
                }
            }
        }
    }

    
}