using GerberToQuspa.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace GerberToQuspa
{
    class PointComparer<T> : IEqualityComparer<T>
    {
        private const double Epsilon = 1e-3;

        private readonly Func<T, double> getX;
        private readonly Func<T, double> getY;

        public PointComparer(Func<T, double> x, Func<T, double> y)
        {
            getX = x;
            getY = y;
        }

        public bool Equals(T a, T b)
        {
            return Math.Abs(getX(a) - getX(b)) < Epsilon &&
                   Math.Abs(getY(a) - getY(b)) < Epsilon;
        }

        public int GetHashCode(T obj)
        {
            long x = (long)Math.Round(getX(obj) / Epsilon);
            long y = (long)Math.Round(getY(obj) / Epsilon);

            int hash = 17;
            hash = hash * 23 + x.GetHashCode();
            hash = hash * 23 + y.GetHashCode();

            return hash;
        }
    }

    //class PointComparer : IEqualityComparer<Point>
    //{
    //    private const double Epsilon = 1e-3; // 許容誤差

    //    public bool Equals(Point a, Point b)
    //    {
    //        return Math.Abs(a.X - b.X) < Epsilon
    //            && Math.Abs(a.Y - b.Y) < Epsilon;
    //    }

    //    public int GetHashCode(Point obj)
    //    {
    //        // Epsilon単位で整数化してハッシュ値を作る
    //        long xHash = (long)(obj.X / Epsilon);
    //        long yHash = (long)(obj.Y / Epsilon);

    //        int hash = 17;
    //        hash = hash * 23 + xHash.GetHashCode();
    //        hash = hash * 23 + yHash.GetHashCode();
    //        return hash;
    //    }
    //}

    public class PointVM : BindableBase
    {
        private double _x;
        private double _y;
        private TransformContext _transform;

        public int Size;

        public PointVM(double x, double y, TransformContext transform, int size)
        {
            _x = x;
            _y = y;
            _transform = transform;
            Size = size;
            // TransformContext の変更を監視
            _transform.PropertyChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(DisplayX));
                OnPropertyChanged(nameof(DisplayY));
            };
        }

        // 実データ
        public double X
        {
            get { return _x; }
            set
            {
                SetProperty(ref _x, value);
                OnPropertyChanged(nameof(DisplayX));
            }
        }
        public double Y
        {
            get { return _y; }
            set
            {
                SetProperty(ref _y, value);
                OnPropertyChanged(nameof(DisplayY));
            }
        }

        // 表示用
        public double DisplayX
        {
            get { return X - _transform.TeachingOffsetX; }
            set { X = value + _transform.TeachingOffsetX; }
        }
        public double DisplayY
        {
            get { return Y - _transform.TeachingOffsetY; }
            set { Y = value + _transform.TeachingOffsetY; }
        }

        // 描画用
        public double ViewX => X * _transform.Resolution - _transform.Origin.X;
        public double ViewY => Y * -_transform.Resolution + _transform.Origin.Y;

        public TransformContext TransformContext
        {
            get { return _transform; }
            set { _transform = value; }
        }

        public PointVM Clone()
        {
            return new PointVM(this.X, this.Y, this._transform, this.Size);
        }
    }

    public class Cluster : BindableBase
    {
        /// <summary>
        /// 名称
        /// </summary>
        private string _name = "未分類データ";
        public string Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        /// <summary>
        /// 座標データリスト
        /// </summary>
        private ObservableCollection<PointVM> _pointList = new ObservableCollection<PointVM>();
        public ObservableCollection<PointVM> PointList
        {
            get { return _pointList; }
            set { SetProperty(ref _pointList, value); }
        }

        /// <summary>
        /// 描画ブラシ
        /// </summary>
        private Color _viewColor = ColorHelper.GetColorByIndex(0);
        public Color ViewColor
        {
            get { return _viewColor; }
            set { SetProperty(ref _viewColor, value); }
        }

        /// <summary>
        /// 色情報の表示/非表示設定
        /// </summary>
        public bool ShowCheck { get; set; }

        /// <summary>
        /// 編集中
        /// </summary>
        private bool _isEditing = false;
        public bool IsEditing
        {
            get { return _isEditing; }
            set { SetProperty(ref _isEditing, value); }
        }

        /// <summary>
        /// クラスタ領域
        /// </summary>
        public Rect ContentBounds
        {
            get
            {
                Rect rect = Rect.Empty;
                foreach (var data in PointList)
                {
                    rect.Union(new Point(data.ViewX, data.ViewY)); // 各要素のバウンディングを統合     
                }
                rect.Inflate(10, 10);
                return rect;
            }
        }

        /// <summary>
        /// 座標中心位置の取得
        /// </summary>
        public Point GetCentroid => new Point(PointList.Average(p => p.X), PointList.Average(p => p.Y));

        /// <summary>
        /// 未分類データ？
        /// </summary>
        public bool IsUngroupedItems => Name == "未分類データ";

        public DelegateCommand ReverseCommand { get; }

        public Cluster()
        {
            ReverseCommand = new DelegateCommand(ReversePointList);
        }

        /// <summary>
        /// 座標データをセット
        /// </summary>
        /// <param name="points"></param>
        public void SetPointList(List<PointVM> points)
        {
            foreach (var data in points)
            {
                _pointList.Add(data);
            }
        }

        // <summary>
        /// 座標データをセット
        /// </summary>
        /// <param name="points"></param>
        public void SetViewPointList(List<Point> points, TransformContext transform, int size)
        {
            var comparer = new PointComparer<Point>(p => p.X, p => p.Y);
            var set = new HashSet<Point>(PointList
                .Select(p => new Point(
                    p.X * transform.Resolution - transform.Origin.X,
                    p.Y * -transform.Resolution + transform.Origin.Y)),
                    comparer);
            List<Point> addList = points.Where(p => set.Add(p)).ToList();

            // 重複していない点だけ PointVM に変換して PointList に追加
            foreach (var item in addList)
            {
                PointList.Add(new PointVM
                (
                    (item.X + transform.Origin.X) / transform.Resolution,
                    (item.Y - transform.Origin.Y) / -transform.Resolution,
                    transform,
                    size
                ));
            }
        }

        public void SetViewPointList(List<PointVM> points, TransformContext transform, OperationViewModel vm)
        {
            var comparer = new PointComparer<PointVM>(p => p.ViewX, p => p.ViewY);
            var set = new HashSet<PointVM>(PointList, comparer);

            foreach (var item in points)
            {
                PointVM point = item.Clone();
                point.X += vm.OffsetX;
                point.Y += vm.OffsetY;

                if (set.Add(point))
                {
                    PointList.Add(point);
                }
            }
        }

        /// <summary>
        /// 座標データの順序を反転
        /// </summary>
        public void ReversePointList()
        {
            for (int i = 0, j = PointList.Count - 1; i < j; i++, j--)
            {
                var tmp = PointList[i];
                PointList[i] = PointList[j];
                PointList[j] = tmp;
            }
        }
    }
}