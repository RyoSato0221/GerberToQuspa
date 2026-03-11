using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace GerberToQuspa
{
    /// <summary>
    /// ドラッグ操作中の選択矩形を描画する Adorner。
    /// 対象要素（Canvasなど）の上にオーバーレイ表示される。
    /// </summary>
    public class DragSelectionAdorner : Adorner
    {
        public RenderContext RenderContext { get; set; }

        private Rect? _dragRect;
        public Rect? DragRect
        {
            get { return _dragRect; }
            set
            {
                _dragRect = value;
                InvalidateVisual();   // 再描画
            }
        }

        public DragSelectionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false; 
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (DragRect.HasValue)
            {
                Pen pen = new Pen(Brushes.LightBlue, RenderContext.TransformContext.Thickness);
                dc.DrawRectangle(Brushes.Transparent, pen, DragRect.Value);
            }
        }
    }

    /// <summary>
    /// 選択されたパッドを描画する Adorner。
    /// 対象要素（Canvasなど）の上にオーバーレイ表示される。
    /// </summary>
    public class PadSelectionAdorner : Adorner
    {
        public GerberAnalysisData GerberAnalysisData { get; set; }
        
        public PadSelectionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (GerberAnalysisData?.SelectItems.Count == null)
                return;

            dc.PushTransform(new TranslateTransform(-GerberAnalysisData.ContentBounds.BottomLeft.X, GerberAnalysisData.ContentBounds.BottomLeft.Y));
            dc.PushTransform(new ScaleTransform(1, -1));

            foreach (var item in GerberAnalysisData.SelectItems)
            {
                item.DrawSelect(dc);
            }

            dc.Pop();
            dc.Pop();
        }
    }

    /// <summary>
    /// 原点座標を描画する Adorner。
    /// 対象要素（Canvasなど）の上にオーバーレイ表示される。
    /// </summary>
    public class OriginCrossAdorner : Adorner
    {
        public RenderContext RenderContext { get; set; }
       
        private Point? _originPoint;
        public Point? OriginPoint
        {
            get { return _originPoint; }
            set
            {
                _originPoint = value;
                InvalidateVisual();   // 再描画
            }
        }

        public OriginCrossAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (OriginPoint.HasValue)
            {
                double halfLength = RenderContext.TransformContext.Thickness * 10;
                // ペンの太さと色
                Pen pen = new Pen(Brushes.Yellow, RenderContext.TransformContext.Thickness);

                // 横線
                dc.DrawLine(pen, new Point(OriginPoint.Value.X - halfLength, OriginPoint.Value.Y),
                                 new Point(OriginPoint.Value.X + halfLength, OriginPoint.Value.Y));
                // 縦線
                dc.DrawLine(pen, new Point(OriginPoint.Value.X, OriginPoint.Value.Y - halfLength),
                                 new Point(OriginPoint.Value.X, OriginPoint.Value.Y + halfLength));
            }
        }
    }

    /// <summary>
    /// 塗布経路を描画する Adorner。
    /// 対象要素（Canvasなど）の上にオーバーレイ表示される。
    /// </summary>
    public class OperateLineAdorner : Adorner
    {
        public RenderContext RenderContext { get; set; }
       
        public OperateLineAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            // 塗布点の描画
            Point? current = null;

            int i = 0;
            foreach (var cluster in RenderContext.ClusterList)
            {
                Color color = cluster.ViewColor;

                bool isFirstPoint = true;
                foreach (var point in cluster.PointList)
                {
                    double t = RenderContext.TransformContext.Thickness;
                    Pen Stroke = new Pen(new SolidColorBrush(color), t);

                    if (current != null && cluster.IsUngroupedItems == false)
                    {
                        if (i != 0 && isFirstPoint)
                        {
                            Stroke = new Pen(new SolidColorBrush(Colors.White), t);
                        }
                        LineGeometry line = new LineGeometry(current.Value, new Point(point.ViewX, point.ViewY));
                        dc.DrawGeometry(null, Stroke, line);
                    }
                    isFirstPoint = false;
                    current = new Point(point.ViewX, point.ViewY);
                }
                i++;
            }
        }
    }

    // <summary>
    /// 選択された塗布点を描画する Adorner。
    /// 対象要素（Canvasなど）の上にオーバーレイ表示される。
    /// </summary>
    public class DiapenseSelectionAdorner : Adorner
    {
        public RenderContext RenderContext { get; set; }        

        public DiapenseSelectionAdorner(UIElement adornedElement) : base(adornedElement)
        {
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            // 選択されている塗布点は色を変えて上書き
            Pen selectPen = new Pen(Brushes.White, 1);
            Brush selectBrush = new SolidColorBrush(Colors.White);
            foreach (var point in RenderContext.SelectPointList)
            {
                dc.DrawEllipse(selectBrush, selectPen, new Point(point.ViewX, point.ViewY), point.Size, point.Size);
            }
        }
    }
    
    /// <summary>
    /// ユーザーが追加した塗布点（プロセスポイント）を描画するレイヤー。
    /// ガーバー図面の上に重ねて表示される。
    /// </summary>
    public class DispensePointLayer : FrameworkElement
    {
        public RenderContext RenderContext { get; set; }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
                       
            // 塗布点の描画
            foreach (var cluster in RenderContext.ClusterList)
            {
                Color color = cluster.ViewColor;

                if(cluster == RenderContext.SelectedCluster)
                {
                    Color fillColor = Color.FromArgb(128, color.R, color.G, color.B);
                    dc.DrawRectangle(new SolidColorBrush(fillColor), new Pen(new SolidColorBrush(fillColor), RenderContext.TransformContext.Thickness), cluster.ContentBounds);                    
                }

                foreach (var point in cluster.PointList)
                {
                    Brush Fill = new SolidColorBrush(color);
                    Pen Stroke = new Pen(new SolidColorBrush(color), 1);
                    dc.DrawEllipse(Fill, Stroke, new Point(point.ViewX, point.ViewY), point.Size, point.Size);
                }                
            }            
        }               
    }

    /// <summary>
    /// ガーバー図面を描画するレイヤー。
    /// </summary>
    public class GerberRenderLayer : FrameworkElement
    {
        private Visual _child;

        public void SetVisual(Visual visual)
        {
            if (_child != null)
            {
                RemoveVisualChild(_child);
                RemoveLogicalChild(_child);
            }

            _child = visual;

            if (_child != null)
            {
                AddVisualChild(_child);
                AddLogicalChild(_child);
            }
        }

        protected override int VisualChildrenCount => _child == null ? 0 : 1;

        protected override Visual GetVisualChild(int index)
        {
            if (_child == null || index != 0)
                throw new ArgumentOutOfRangeException();

            return _child;
        }
    }

    public static class ColorHelper
    {
        public static Color GetColorByIndex(int index)
        {
            if(index == -1)
            {
                return Colors.White;
            }
            // indexに応じて徐々に色相が回転するようにする（360度をゆっくり1周）
            double hue = (index * 20) % 360;   // 1インデックスごとに10度ずつ変化
            double saturation = 0.9;           // 鮮やかさ
            double value = 1.0;                // 明るさ

            return FromHsv(hue, saturation, value);
        }

        private static Color FromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = (byte)value;
            byte p = (byte)(value * (1 - saturation));
            byte q = (byte)(value * (1 - f * saturation));
            byte t = (byte)(value * (1 - (1 - f) * saturation));

            switch (hi)
            {
                case 0: return Color.FromRgb(v, t, p);
                case 1: return Color.FromRgb(q, v, p);
                case 2: return Color.FromRgb(p, v, t);
                case 3: return Color.FromRgb(p, q, v);
                case 4: return Color.FromRgb(t, p, v);
                default: return Color.FromRgb(v, p, q);
            }
        }
    }
}
