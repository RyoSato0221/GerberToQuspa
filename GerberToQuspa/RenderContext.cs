using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GerberToQuspa
{
    public class TransformContext : BindableBase
    {
        public ScaleTransform ScaleTransform { get; } = new ScaleTransform();
        public TranslateTransform TranslateTransform { get; } = new TranslateTransform();
        public TransformGroup Transform { get; } = new TransformGroup();
        
        private double _originScale { get; set; }
        private Rect _originRect { get; set; }
        private double _originViewWidth { get; set; }
        private double _originViewHeight { get; set; }

        public double OriginOffsetX => _originViewWidth / 2 - _originRect.Width / 2 * ActualScaleX;
        public double OriginOffsetY => _originViewHeight / 2 - _originRect.Height / 2 * ActualScaleY;

        private double ActualScaleX => _scale * (_isMirrorX ? -1 : 1);
        private double ActualScaleY => _scale * (_isMirrorY ? -1 : 1);

        private double _mirrorOffsetX;
        private double _mirrorOffsetY;

        public Point Origin { get; set; }

        private double _scale = 1.0;
        public double Scale
        {
            get { return _scale; }
            set
            {
                _scale = value;
                UpdateTransform();
            }
        }

        private double _offsetX;
        public double OffsetX
        {
            get { return _offsetX; }
            set
            {
                _offsetX = value;
                UpdateTransform();
            }
        }

        private double _offsetY;
        public double OffsetY
        {
            get { return _offsetY; }
            set
            {
                _offsetY = value;
                UpdateTransform();
            }
        }

        private bool _isMirrorX;
        public bool IsMirrorX
        {
            get { return _isMirrorX; }
            set
            {
                _isMirrorX = value;
                if (_isMirrorX)
                {
                    _offsetX += _mirrorOffsetX * _scale / _originScale;
                }
                else
                {
                    _offsetX -= _mirrorOffsetX * _scale / _originScale;
                }
                UpdateTransform();
            }
        }

        private bool _isMirrorY;
        public bool IsMirrorY
        {
            get { return _isMirrorY; }
            set
            {
                _isMirrorY = value;
                if (_isMirrorY)
                {
                    _offsetY += _mirrorOffsetY * _scale / _originScale;
                }
                else
                {
                    _offsetY -= _mirrorOffsetY * _scale / _originScale;
                }
                UpdateTransform();
            }
        }
        
        public int Resolution { get; set; } = 1000;
        
        private Point? _panStartWorld;
        
        /// <summary>
        /// ティーチング原点
        /// </summary>
        private double _teachingOffsetX;
        public double TeachingOffsetX
        {
            get { return _teachingOffsetX; }
            set { SetProperty(ref _teachingOffsetX, value); }
        }
        private double _teachingOffsetY;
        public double TeachingOffsetY
        {
            get { return _teachingOffsetY; }
            set { SetProperty(ref _teachingOffsetY, value); }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TransformContext()
        {
            // TransformGroup: Scale → Translateに順序変更
            Transform.Children.Add(ScaleTransform);
            Transform.Children.Add(TranslateTransform);
        }        

        /// <summary>
        /// ズームアクション
        /// </summary>
        /// <param name="screenPos"></param>
        /// <param name="zoomFactor"></param>
        public void ZoomAt(Point screenPos, double zoomFactor)
        {
            double newScale = _scale * zoomFactor;

            newScale = Math.Max(0.00001, Math.Min(50, newScale));

            Point worldPos = ScreenToWorld(screenPos);
            _scale = newScale;
            _offsetX = screenPos.X - worldPos.X * ActualScaleX;
            _offsetY = screenPos.Y - worldPos.Y * ActualScaleY;

            UpdateTransform();
        }

        /// <summary>
        /// パン開始
        /// </summary>
        /// <param name="screenPos"></param>
        public void BeginPan(Point screenPos)
        {
            _panStartWorld = ScreenToWorld(screenPos);
        }

        public void PanTo(Point screenPos)
        {
            if (_panStartWorld == null)
            {
                return;
            }

            Point worldNow = ScreenToWorld(screenPos);

            Vector delta = worldNow - _panStartWorld.Value;
            _offsetX += delta.X * ActualScaleX;
            _offsetY += delta.Y * ActualScaleY;

            UpdateTransform();
        }

        public void EndPan()
        {
            _panStartWorld = null;
        }
        
        public void Fit(Rect rect, double viewWidth, double viewHeight)
        {
            double scaleX = viewWidth / rect.Width;
            double scaleY = viewHeight / rect.Height;
            Scale = Math.Min(scaleX, scaleY);

            // Reset 用に保存
            _originRect = rect;
            _originViewWidth = viewWidth;
            _originViewHeight = viewHeight;
            _originScale = Scale;
            _offsetX = OriginOffsetX;
            _offsetY = OriginOffsetY;

            _mirrorOffsetX = _originRect.Width * _scale;
            _mirrorOffsetY = _originRect.Height * _scale;

            UpdateTransform();
        }

        public void Init()
        {
            Origin = new Point();
        }

        public void Reset()
        {
            _scale = _originScale;
            _offsetX = OriginOffsetX;
            _offsetY = OriginOffsetY;
            UpdateTransform();
        }

        public Point GetVSTeachingOffset
        {
            get
            {
                return new Point()
                {
                    X = (TeachingOffsetX * Resolution) - Origin.X,
                    Y = (TeachingOffsetY * -Resolution) + Origin.Y,
                };            
            }
        }

        private int _thickness;
        public int Thickness
        {
            get { return _thickness; }
            set { SetProperty(ref _thickness, value); }
        }

        /// <summary>
        /// Screan座標からWorld座標系のRectを生成
        /// </summary>
        /// <param name="startScreen"></param>
        /// <param name="endScreen"></param>
        /// <returns></returns>
        public Rect CreateDragAreaWorld(Point startScreen, Point endScreen)
        {
            var start = ScreenToWorld(startScreen);
            var end = ScreenToWorld(endScreen);

            return new Rect(start, end);
        }

        /// <summary>
        /// Screan座標からGerber座標系のRectを生成
        /// </summary>
        /// <param name="startScreen"></param>
        /// <param name="endScreen"></param>
        /// <returns></returns>
        public Rect CreateDragAreaGerber(Point startScreen, Point endScreen)
        {
            var start = ScreenToGerber(startScreen);
            var end = ScreenToGerber(endScreen);

            return new Rect(start, end);
        }

        /// <summary>
        /// Screan → Gerberへの座標変換
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public Point ScreenToGerber(Point p)
        {
            var world = ScreenToWorld(p);

            world.Offset(Origin.X, -Origin.Y);
            world.Y = -world.Y;

            return world;
        }

        /// <summary>
        /// Screan → Worldへの座標変換
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public Point ScreenToWorld(Point p)
        {
            return new Point(
                (p.X - _offsetX) / ActualScaleX,
                (p.Y - _offsetY) / ActualScaleY
            );
        }

        /// <summary>
        /// Transformの更新
        /// </summary>
        private void UpdateTransform()
        {
            ScaleTransform.ScaleX = ActualScaleX;
            ScaleTransform.ScaleY = ActualScaleY;
           
            TranslateTransform.X = _offsetX;
            TranslateTransform.Y = _offsetY;

            int thick = (int)(2.0 / _scale);

            Thickness = (thick == 0) ? 1 : thick;

            OnPropertyChanged(nameof(Transform));
        }
    }

    public class RenderContext : BindableBase
    {
        public TransformContext TransformContext { get; set; } = new TransformContext();
        
        /// <summary>
        /// クラスタ一覧
        /// </summary>
        public ObservableCollection<Cluster> ClusterList { get; } = new ObservableCollection<Cluster>();
        
        /// <summary>
        /// 選択中のクラスタ
        /// </summary>
        private Cluster _selectedCluster;
        public Cluster SelectedCluster
        {
            get { return _selectedCluster; }
            set
            {
                if (value == null || value?.PointList.Count() != 0)
                {
                    SetProperty(ref _selectedCluster, value);
                }
            }
        }

        /// <summary>
        /// 選択塗布点リスト
        /// </summary>
        public List<PointVM> SelectPointList { get; } = new List<PointVM>();        

        public event Action RedrawRequested;

        /// <summary>
        /// コンストラクタ
        /// </summary> 
        public RenderContext()
        {
            ClusterList.CollectionChanged += OnClusterListChanged;
        }

        private void OnClusterListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Cluster c in e.NewItems)
                {
                    c.PointList.CollectionChanged += OnPointListChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (Cluster c in e.OldItems)
                {
                    c.PointList.CollectionChanged -= OnPointListChanged;
                }
            }
            OnPropertyChanged(nameof(ClusterList));
        }
        private void OnPointListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ClusterList));
        }

        public void SetSelectPointList(Rect dragArea)
        {
            SelectPointList.Clear();
            foreach (var cluster in ClusterList)
            {
                // Rect 内に含まれる ViewPointList の点を取得
                SelectPointList.AddRange(cluster.PointList
                    .Where(p => dragArea.Contains(new Point(p.ViewX + TransformContext.Origin.X, TransformContext.Origin.Y - p.ViewY)))
                    .ToList());
            }
        }
      
        public void RemoveSelectedPointsFromClusters()
        {
            ExtractSelectedPoints();
        }

        public void ReGrouping()
        {
            var extracted = ExtractSelectedPoints();

            if (extracted.Count == 0)
                return;

            var newCluster = new Cluster
            {
                Name = $"Cluster_{ClusterList.Count + 1}",
                ViewColor = ColorHelper.GetColorByIndex(ClusterList.Count + 1),
                ShowCheck = true
            };

            foreach (var p in extracted)
            {
                newCluster.PointList.Add(p);
            }

            ClusterList.Add(newCluster);
        }

        private List<PointVM> ExtractSelectedPoints()
        {
            var selected = new HashSet<PointVM>(SelectPointList);
            var extracted = new List<PointVM>();

            for (int c = ClusterList.Count - 1; c >= 0; c--)
            {
                var cluster = ClusterList[c];

                for (int i = cluster.PointList.Count - 1; i >= 0; i--)
                {
                    var point = cluster.PointList[i];

                    if (selected.Contains(point))
                    {
                        extracted.Add(point);
                        cluster.PointList.RemoveAt(i);
                    }
                }

                if (cluster.PointList.Count == 0)
                {
                    ClusterList.RemoveAt(c);
                }
            }

            SelectPointList.Clear();
            return extracted;
        }
    }
}
