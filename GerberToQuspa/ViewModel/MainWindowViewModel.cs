using GerberToQuspa.ViewModel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GerberToQuspa
{
    public static class MouseEventHelper
    {
        public static bool TryGetPosition(this MouseEventArgs e, object sender, GerberAnalysisData gerberData, RenderContext rc, out Point position)
        {
            position = default(Point);

            if (gerberData.DrawItems.Count == 0 && rc.ClusterList.Count == 0)
                return false;

            var ui = sender as UIElement;
            if (ui == null)
                return false;

            position = e.GetPosition(ui);
            return true;
        }
    }

    class MainWindowViewModel : BindableBase
    {
        /// <summary>
        /// 描画情報
        /// </summary>
        public RenderContext RenderContext { get; set; } = new RenderContext();

        /// <summary>
        /// タブ選択
        /// </summary>
        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set { SetProperty(ref _selectedTabIndex, value); }
        }

        /// <summary>
        /// 描画エリアのサイズ
        /// </summary>
        public double ViewWidth { get; set; }
        public double ViewHeight { get; set; }

        /// <summary>
        /// 現在座標
        /// </summary>
        private Point _currentMouseWorldPos = new Point(0, 0);
        public Point CurrentMouseWorldPos
        {
            get { return _currentMouseWorldPos; }
            set
            {
                SetProperty(ref _currentMouseWorldPos, value);
                OnPropertyChanged(nameof(CurrentMouseWorldPosX));
                OnPropertyChanged(nameof(CurrentMouseWorldPosY));
            }
        }
        public string CurrentMouseWorldPosX => (CurrentMouseWorldPos.X - RenderContext.TransformContext.TeachingOffsetX).ToString("F4");
        public string CurrentMouseWorldPosY => (CurrentMouseWorldPos.Y - RenderContext.TransformContext.TeachingOffsetY).ToString("F4");

        /// <summary>
        /// 経路作成ボタンの有効設定
        /// </summary>                        
        public bool IsEnableCreateRoute => (RenderContext.ClusterList.Count() >= 1);

        /// <summary>
        /// データ出力ボタンの有効設定
        /// </summary> 
        public bool IsEnableOutputFile => (RenderContext.ClusterList.Count() >= 1);

        /// <summary>
        /// 原点設定ボタンの表示設定
        /// </summary>
        public Visibility VisibilitySetOriginPosCommand => (GerberData.SelectItems.Count() == 1) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 原点座標
        /// </summary>       
        private bool _originView = true;
        public bool OriginView
        {
            get { return _originView; }
            set
            {
                SetProperty(ref _originView, value);
                UpDateViewOrigin();
            }
        }

        /// <summary>
        /// ドラッグ領域
        /// </summary>
        private Point? _dragStartWorld;

        /// <summary>
        /// ガーバーデータ管理クラス
        /// </summary>
        private GerberDataControl GerberDataControl = new GerberDataControl();

        /// <summary>
        /// 読み込んだガーバーデータ情報
        /// </summary>
        private GerberAnalysisData _GerberData = new GerberAnalysisData();
        public GerberAnalysisData GerberData
        {
            get { return _GerberData; }
            set
            {
                SetProperty(ref _GerberData, value);
                RenderContext.TransformContext.Fit(_GerberData.ContentBounds, ViewWidth, ViewHeight);
                RenderContext.TransformContext.Origin = _GerberData.ContentBounds.BottomLeft;
            }
        }

        private Cursor _currentCursor = Cursors.Arrow;
        public Cursor CurrentCursor
        {
            get { return _currentCursor; }
            set { SetProperty(ref _currentCursor, value); }
        }

        // ContentMenu
        public DelegateCommand DeleteDispenseCommand { get; }
        public DelegateCommand OffsetDispenseCommand { get; }
        public DelegateCommand ClusteringCommand { get; }

        // ボタン操作
        public DelegateCommand ResetOriginCommand { get; }
        public DelegateCommand ResetViewCommand { get; }
        public DelegateCommand ReadGervarCommand { get; }
        public DelegateCommand CreateRouteCommand { get; }
        public DelegateCommand WhileDataInPortCommand { get; }
        public DelegateCommand WhileDataExPortCommand { get; }
        public DelegateCommand SetOriginPosCommand { get; }

        // 描画
        private DispensePointLayer _dispensePointLayer { get; set; }
        private GerberRenderLayer _gerberRenderLayer { get; set; }
        
        // 選択アイテム情報
        public bool IsSelectItem { get; set; } = false;
        public string SelectItemKey { get; set; }
        public string SelectItemCenter { get; set; }

        /// <summary>
        /// アパーチャリスト
        /// </summary>
        public ObservableCollection<ApertureDropDownItem> ApertureList { get; set; } = new ObservableCollection<ApertureDropDownItem>();
        private ApertureDropDownItem _selectAperture = null;
        public ApertureDropDownItem SelectAperture
        {
            get { return _selectAperture; }
            set
            {
                SetProperty(ref _selectAperture, value);
                GerberData.SetSelectItems(GerberDataControl.GetAperture(_selectAperture.key));
                _padSelectionAdorner.InvalidateVisual();
            }
        }

        public OperationOrderViewModel OperationOrderVM { get; }
        public ClusterSettingViewModel ClusterSettingViewModel { get; set; } = new ClusterSettingViewModel();

        public OperationModel OperationModel { get; set; }
        public OperationViewModel OperationViewModel { get; set; }

        private DragSelectionAdorner _dragSelectionAdorner { get; set; }
        private OriginCrossAdorner _originCrossAdorner { get; set; }
        private PadSelectionAdorner _padSelectionAdorner { get; set; }

        private OperateLineAdorner _operateLineAdorner { get; set; }
        private DiapenseSelectionAdorner _diapenseSelectionAdorner { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="visualHost"></param>
        public MainWindowViewModel(Canvas padCanvas, Canvas dispenseCanvas, DispensePointLayer dispensePointLayer, GerberRenderLayer gerberRenderLayer)
        {
            RenderContext.PropertyChanged += (sender, e) =>
            {
                _dispensePointLayer.InvalidateVisual();
            };
            RenderContext.TransformContext.PropertyChanged += (sender, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(TransformContext.TeachingOffsetX):
                    case nameof(TransformContext.TeachingOffsetY):
                        {
                            UpDateViewOrigin();
                        }
                        break;
                }
            };
            RenderContext.ClusterList.CollectionChanged += (sender, e) =>
            {
                _operateLineAdorner.InvalidateVisual();
            };

            // PadCanvas 上の AdornerLayer を取得
            var padLayer = AdornerLayer.GetAdornerLayer(padCanvas);

            // ドラッグ選択用 Adorner（矩形選択表示）
            _dragSelectionAdorner = new DragSelectionAdorner(padCanvas)
            {
                RenderContext = RenderContext
            };
            padLayer.Add(_dragSelectionAdorner);

            // パッド選択表示用 Adorner（選択Padのハイライト）
            _padSelectionAdorner = new PadSelectionAdorner(padCanvas);
            padLayer.Add(_padSelectionAdorner);

            // 原点表示用 Adorner（十字カーソル）
            _originCrossAdorner = new OriginCrossAdorner(padCanvas)
            {
                RenderContext = RenderContext
            };
            padLayer.Add(_originCrossAdorner);

            // DispenseCanvas 上の AdornerLayer を取得
            var dispenseLayer = AdornerLayer.GetAdornerLayer(dispenseCanvas);

            // 操作ライン表示用 Adorner（塗布経路）
            _operateLineAdorner = new OperateLineAdorner(dispenseCanvas)
            {
                RenderContext = RenderContext
            };
            dispenseLayer.Add(_operateLineAdorner);

            // 塗布点選択表示用 Adorner
            _diapenseSelectionAdorner = new DiapenseSelectionAdorner(dispenseCanvas)
            {
                RenderContext = RenderContext
            };
            dispenseLayer.Add(_diapenseSelectionAdorner);

            // 塗布点描画レイヤー
            _dispensePointLayer = dispensePointLayer;
            _dispensePointLayer.RenderContext = RenderContext;
            _dispensePointLayer.IsHitTestVisible = false;

            // Gerber 描画レイヤー
            _gerberRenderLayer = gerberRenderLayer;
            _gerberRenderLayer.CacheMode = new BitmapCache()
            {
                RenderAtScale = 2.0   // 高解像度キャッシュ
            };
            _gerberRenderLayer.IsHitTestVisible = false;

            // ボタン操作
            ResetOriginCommand = new DelegateCommand(ResetOrigin);
            ResetViewCommand = new DelegateCommand(ResetView);
            ReadGervarCommand = new DelegateCommand(ReadGervar);
            CreateRouteCommand = new DelegateCommand(CreateRoute);
            WhileDataInPortCommand = new DelegateCommand(WhileDataInPort);
            WhileDataExPortCommand = new DelegateCommand(WhileDataExPort);
            SetOriginPosCommand = new DelegateCommand(SetOriginPos);

            // 子VMの変更を購読
            ClusterSettingViewModel.PropertyChanged += ClusterSettingViewModel_PropertyChanged;

            OperationOrderVM = new OperationOrderViewModel();
            OperationOrderVM.RenderContext = RenderContext;

            // 操作画面
            OperationModel = new OperationModel();
            OperationViewModel = new OperationViewModel(OperationModel);
            OperationViewModel.AddPointCommand = new DelegateCommand(AddDisoensePoint);
            OperationViewModel.CopyPointCommand = new DelegateCommand(CopyPoint);
            OperationViewModel.MovePointCommand = new DelegateCommand(MovePoint);
            OperationViewModel.DeletePointCommand = new DelegateCommand(DeletePoint);
            OperationViewModel.GroupingPointCommand = new DelegateCommand(GroupingPoint);

            ApertureList.Add(new ApertureDropDownItem());
            SelectAperture = ApertureList[0];
        }

        private void ClusterSettingViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ClusterSettingViewModel.Priority):
                    if (RenderContext.ClusterList.Count() != 0)
                    {
                        if (e.PropertyName == nameof(ClusterSettingViewModel.Priority))
                        {
                            ReCreateRoute();
                        }
                    }
                    break;
                case nameof(ClusterSettingViewModel.IsMirrorX):
                    RenderContext.TransformContext.IsMirrorX = ClusterSettingViewModel.IsMirrorX;
                    break;
                case nameof(ClusterSettingViewModel.IsMirrorY):
                    RenderContext.TransformContext.IsMirrorY = ClusterSettingViewModel.IsMirrorY;
                    break;
            }
        }

        /// <summary>
        /// Viewサイズの変更
        /// </summary>
        public void UpdataViewSize()
        {
            RenderContext.TransformContext.UpdataViewSize(ViewWidth, ViewHeight);
        }

        /// <summary>
        /// マウスホイール操作（Zoomの実行）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point pos = new Point();
            if (!e.TryGetPosition(sender, GerberData, RenderContext, out pos))
                return;

            RenderContext.TransformContext.ZoomAt(pos, e.Delta > 0 ? 1.1 : 0.9);
            _originCrossAdorner.InvalidateVisual();
            _padSelectionAdorner.InvalidateVisual();
            _operateLineAdorner.InvalidateVisual();
        }

        /// <summary>
        /// マウス移動
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnMouseMove(object sender, MouseEventArgs e)
        {
            Point pos = new Point();
            if (!e.TryGetPosition(sender, GerberData, RenderContext, out pos))
                return;

            // 現在のワールド座標
            var t = RenderContext.TransformContext;
            Point current = t.ScreenToWorld(pos);

            // 表示用にワールド座標を変換
            CurrentMouseWorldPos = new Point((current.X + t.Origin.X) / 1000, (current.Y - t.Origin.Y) / -1000);

            // パン動作
            t.PanTo(pos);

            if (_dragStartWorld != null)
            {
                // VisualHost にワールド矩形を通知（描画用）
                _dragSelectionAdorner.DragRect = t.CreateDragAreaWorld(_dragStartWorld.Value, pos);
            }
        }

        /// <summary>
        /// マウス左押しイベント（パンの開始）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = new Point();
            if (!e.TryGetPosition(sender, GerberData, RenderContext, out pos))
                return;

            RenderContext.TransformContext.BeginPan(pos);
        }

        /// <summary>
        /// マウス左離しイベント（パンの終了）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = new Point();
            if (!e.TryGetPosition(sender, GerberData, RenderContext, out pos))
                return;

            RenderContext.TransformContext.EndPan();
        }

        /// <summary>
        /// マウス右押しイベント（領域指定開始）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            Point pos = new Point();
            if (!e.TryGetPosition(sender, GerberData, RenderContext, out pos))
                return;

            _dragStartWorld = pos;
        }

        /// <summary>
        /// マウス右離しイベント（領域指定終了）
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void OnMouseRightUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = new Point();
            if (!e.TryGetPosition(sender, GerberData, RenderContext, out pos))
                return;

            _dragSelectionAdorner.DragRect = null;

            var dragArea = RenderContext.TransformContext.CreateDragAreaGerber(_dragStartWorld.Value, pos);
            _dragStartWorld = null;

            SelectAperture = ApertureList[0];

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                RenderContext.SetSelectPointList(dragArea);
            }
            else
            {
                RenderContext.SelectPointList.Clear();

                GerberData.SetSelectItems(dragArea);

                if (GerberData.SelectItems.Count == 1)
                {
                    IDrawItem item = GerberData.SelectItems[0];
                    Rect r = item.GetBounds();

                    SelectItemKey = item.Aperture.CreatShowKey();
                    SelectItemCenter = $"(X:{(r.X + r.Width / 2) / 1000:F3}, Y:{(r.Y + r.Height / 2) / 1000:F3})";
                }
                else
                {
                    SelectItemKey = "";
                    SelectItemCenter = "";
                }
                OnPropertyChanged(nameof(SelectItemKey));
                OnPropertyChanged(nameof(SelectItemCenter));
            }
            SelectedTabIndex = 2;
            _padSelectionAdorner.InvalidateVisual();
            _diapenseSelectionAdorner.InvalidateVisual();
            _dispensePointLayer.InvalidateVisual();
            OnPropertyChanged(nameof(VisibilitySetOriginPosCommand));            
        }

        /// <summary>
        /// Fit処理
        /// </summary>
        private void ResetView()
        {
            RenderContext.TransformContext.Reset();
            _dispensePointLayer.InvalidateVisual();
        }

        /// <summary>
        /// ガーバーデータの読み込み
        /// </summary>
        private async void ReadGervar()
        {
            if (RenderContext.ClusterList.Count != 0)
            {
                var dlg = new CustomMessageBox(eMessageStyle.YesNo);
                eMessageBoxResult result = dlg.Show(enumInfoState.Warning, "警告", "全ての塗布点情報は初期化されます。", "よろしいですか？");
                if (result == eMessageBoxResult.No)
                {
                    return;
                }
                RenderContext.ClusterList.Clear();
            }

            // ファイル選択
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "",
                FilterIndex = 1,
            };

            if (openFileDialog.ShowDialog() == true)
            {
                GerberDataControl = new GerberDataControl();
                // 進捗状況の表示
                var progressHelper = new ProgressHelper();

                GerberDataControl.FilePath = openFileDialog.FileName;
                GerberData = await Task.Run(() => GerberDataControl.AnalysisStarts(progressHelper.Progress));

                progressHelper.SetText("マップを生成しています...");
                await Task.Delay(50);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _gerberRenderLayer.SetVisual(GerberData.CreateGerberVisual());
                });

                // 実描画待ち
                await WaitRenderCompleteAsync();
                UpDateViewOrigin();

                // アパーチャリストの更新
                ApertureList.Clear();

                // 先頭固定
                ApertureList.Add(new ApertureDropDownItem());
                var sorted = GerberDataControl.GetApertures()
                    .Select(x => new ApertureDropDownItem()
                    {
                        key = x.key,
                        Code = x.CreatShowKey(),
                    })
                    .OrderBy(x => x.key);

                foreach (var item in sorted)
                {
                    ApertureList.Add(item);
                }                

                SelectAperture = ApertureList[0];

                // 完了時にウィンドウを閉じる
                progressHelper.Close();

                _padSelectionAdorner.GerberAnalysisData = GerberData;
            }
        }
        private Task WaitRenderCompleteAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            EventHandler handler = null;

            handler = (s, e) =>
            {
                CompositionTarget.Rendering -= handler;
                tcs.SetResult(true);
            };

            CompositionTarget.Rendering += handler;

            return tcs.Task;
        }

        /// <summary>
        /// ルート生成
        /// </summary>
        private async void CreateRoute()
        {
            var dlg = new CustomMessageBox(eMessageStyle.YesNo);
            eMessageBoxResult result = dlg.Show(enumInfoState.Warning, "警告", "設定中のクラスタが初期化され、現在のルートは破棄されます。", "ルート生成を実行しますか？");
            if (result == eMessageBoxResult.No)
            {
                return;
            }

            var progressHelper = new ProgressHelper("経路生成中");

            await Task.Run(() =>
            {
                RouteSearch route = new RouteSearch();
                // クラスタリングの実行
                List<Cluster> _clusterList = new List<Cluster>();
                _clusterList = route.Clustering(GetAllViewPointData(), ClusterSettingViewModel.Model, progressHelper.Progress);

                // 経路最適化
                _clusterList = route.GenerateOptimizedPath(_clusterList, ClusterSettingViewModel.Model.Priority, progressHelper.Progress);

                // UI更新
                App.Current.Dispatcher.Invoke(() =>
                {
                    RenderContext.ClusterList.Clear();
                    foreach (var cluster in _clusterList)
                    {
                        RenderContext.ClusterList.Add(cluster);
                    }
                    RenderContext.SelectedCluster = RenderContext.ClusterList[0];
                });
            });

            // 完了時にウィンドウを閉じる
            progressHelper.Close();
            _operateLineAdorner.InvalidateVisual();
        }

        /// <summary>
        /// ルート再生成
        /// </summary>
        private async void ReCreateRoute()
        {
            var progressHelper = new ProgressHelper("経路生成中");

            await Task.Run(() =>
            {
                RouteSearch route = new RouteSearch();

                // 経路最適化
                var _clusterList = route.GenerateOptimizedPath(RenderContext.ClusterList.ToList(), ClusterSettingViewModel.Model.Priority, progressHelper.Progress);

                // UI更新
                App.Current.Dispatcher.Invoke(() =>
                {
                    RenderContext.ClusterList.Clear();
                    foreach (var cluster in _clusterList)
                    {
                        RenderContext.ClusterList.Add(cluster);
                    }
                });
            });
            // 完了時にウィンドウを閉じる
            progressHelper.Close();
            _operateLineAdorner.InvalidateVisual();
        }

        /// <summary>
        /// ファイル入力
        /// </summary>
        private void WhileDataInPort()
        {
            // ファイル選択
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "WhileData Files (*.csv;*.gtq)|*.csv;*.gtq",
                FilterIndex = 1,
            };
            if (openFileDialog.ShowDialog() == false)
            {
                return;
            }

            // ファイルを1行ずつ読み込む
            using (StreamReader reader = new StreamReader(openFileDialog.FileName))
            {
                string line;
                bool isFirst = true;
                List<PointVM> newList = new List<PointVM>();

                Cluster currentCluster = null;
                RenderContext.ClusterList.Clear();
                int count = 1;

                while ((line = reader.ReadLine()) != null)
                {
                    string[] splitString = Regex.Split(line, ",");
                    splitString = splitString.Where(s => s != "").ToArray();
                    if (isFirst)
                    {
                        int indx = Array.IndexOf(splitString, "OrizinX");
                        if (indx != -1)
                        {
                            RenderContext.TransformContext.TeachingOffsetX = double.Parse(splitString[indx + 1]);
                        }
                        indx = Array.IndexOf(splitString, "OrizinY");
                        if (indx != -1)
                        {
                            RenderContext.TransformContext.TeachingOffsetY = double.Parse(splitString[indx + 1]);
                        }
                        isFirst = false;
                    }
                    else
                    {
                        PointVM data = new PointVM(
                                double.Parse(splitString[1]) / 10000,
                                double.Parse(splitString[2]) / 10000,
                                RenderContext.TransformContext,
                                (splitString.Count() == 7) ? int.Parse(splitString[6]) : 200);

                        if (currentCluster == null)
                        {
                            if (splitString.Count() >= 6)
                            {
                                currentCluster = new Cluster { Name = splitString[5], ViewColor = ColorHelper.GetColorByIndex(count) };
                                count++;
                            }
                            else
                            {
                                currentCluster = new Cluster { Name = $"未分類データ", ViewColor = ColorHelper.GetColorByIndex(0) };
                            }
                            RenderContext.ClusterList.Add(currentCluster);
                        }
                        else if (splitString.Count() >= 6)
                        {
                            if (currentCluster.Name != splitString[5])
                            {
                                currentCluster = new Cluster { Name = splitString[5], ViewColor = ColorHelper.GetColorByIndex(count) };
                                count++;
                                RenderContext.ClusterList.Add(currentCluster);
                            }
                        }

                        currentCluster.PointList.Add(new PointVM(
                            double.Parse(splitString[1]) / 10000,
                            double.Parse(splitString[2]) / 10000,
                            RenderContext.TransformContext,
                            (splitString.Count() == 7) ? int.Parse(splitString[6]) : 200)
                            );
                    }
                }

                _dispensePointLayer.InvalidateVisual();

                if (GerberData.ContentBounds == Rect.Empty)
                {
                    Rect rect = Rect.Empty;
                    RenderContext.TransformContext.Init();
                    foreach (var item in _dispensePointLayer.RenderContext.ClusterList)
                    {
                        rect.Union(item.ContentBounds); // 各要素のバウンディングを統合                
                    }
                    // 5％ずつ余白を設ける
                    rect.Inflate(rect.Width * 0.05, rect.Height * 0.05);

                    RenderContext.TransformContext.Fit(rect, ViewWidth, ViewHeight);
                }
            }
            OnPropertyChanged(nameof(IsEnableCreateRoute));
            OnPropertyChanged(nameof(IsEnableOutputFile));
        }

        /// <summary>
        /// ファイル出力
        /// </summary>
        private void WhileDataExPort()
        {
            // SaveFileDialog のインスタンスを作成
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Title = "WhileDataOutput",
                FileName = "default.csv",
                Filter = "WhileData Files (*.csv)|*.csv"
            };
            if (saveFileDialog.ShowDialog() == false) return;

            string csvPath = saveFileDialog.FileName;
            string gtqPath = Path.ChangeExtension(csvPath, ".gtq");

            using (var csvWriter = new StreamWriter(csvPath))
            using (var gtqWriter = new StreamWriter(gtqPath))
            {
                // ヘッダー行
                csvWriter.WriteLine(CreateHeader());
                gtqWriter.WriteLine(CreateHeader());

                // データ行
                foreach (var cluster in RenderContext.ClusterList)
                {
                    foreach (var item in cluster.PointList)
                    {
                        csvWriter.WriteLine(CreateCsvLine(item, cluster));
                        gtqWriter.WriteLine(CreateTxtLine(item, cluster));
                    }
                }
            }
        }

        private string CreateHeader()
        {
            return $"0,x,y,v,h,GaverData,,OrizinX,{RenderContext.TransformContext.TeachingOffsetX},OrizinY,{RenderContext.TransformContext.TeachingOffsetY}";
        }

        private string CreateCsvLine(PointVM item, Cluster cluster)
        {
            return $"0,{(int)(item.X * 10000)},{(int)(item.Y * 10000)},0,0";
        }

        private string CreateTxtLine(PointVM item, Cluster cluster)
        {
            return $"0,{(int)(item.X * 10000)},{(int)(item.Y * 10000)},0,0,{cluster.Name},{item.Size}";
        }

        /// <summary>
        /// 原点座標のセット
        /// </summary>
        private void SetOriginPos()
        {
            if (GerberData.SelectItems.Count == 1)
            {
                Rect r = GerberData.SelectItems[0].GetBounds();
                RenderContext.TransformContext.TeachingOffsetX = (r.X + r.Width / 2) / 1000;
                RenderContext.TransformContext.TeachingOffsetY = (r.Y + r.Height / 2) / 1000;
            }
        }

        /// <summary>
        /// 原点座標のリセット
        /// </summary>
        private void ResetOrigin()
        {
            RenderContext.TransformContext.TeachingOffsetX = 0;
            RenderContext.TransformContext.TeachingOffsetY = 0;
        }

        /// <summary>
        /// View上の原点の更新
        /// </summary>
        private void UpDateViewOrigin()
        {
            if (OriginView)
            {
                _originCrossAdorner.OriginPoint = new Point
                {
                    X = (RenderContext.TransformContext.TeachingOffsetX * 1000) - RenderContext.TransformContext.Origin.X,
                    Y = (RenderContext.TransformContext.TeachingOffsetY * -1000) + RenderContext.TransformContext.Origin.Y,
                };
            }
            else
            {
                _originCrossAdorner.OriginPoint = null;
            }            
        }

        private List<PointVM> GetAllViewPointData()
        {
            List<PointVM> _list = new List<PointVM>();
            foreach (var item in RenderContext.ClusterList)
            {
                _list.AddRange(item.PointList);
            }
            return _list;
        }

        /// <summary>
        /// 塗布点の追加
        /// </summary>
        private void AddDisoensePoint()
        {
            List<Point> list = GerberData.GetPointsInRegion(OperationModel);
            if (list.Count() == 0) return;

            var cluster = RenderContext.ClusterList.FirstOrDefault(c => c.IsUngroupedItems);
            if (cluster == null)
            {
                cluster = new Cluster();
                RenderContext.ClusterList.Add(cluster);
            }
            cluster.SetViewPointList(list, RenderContext.TransformContext, (int)(OperationViewModel.SelectedCondition.Size * 1000));

            OnPropertyChanged(nameof(IsEnableCreateRoute));
            OnPropertyChanged(nameof(IsEnableOutputFile));

            GerberData.SelectItems = new List<IDrawItem>();
            _padSelectionAdorner.InvalidateVisual();
        }

        /// <summary>
        /// 塗布点のコピー
        /// </summary>
        private void CopyPoint()
        {
            var cluster = RenderContext.ClusterList.FirstOrDefault(c => c.IsUngroupedItems);
            if (cluster == null)
            {
                cluster = new Cluster();
                RenderContext.ClusterList.Add(cluster);
            }
            cluster.SetViewPointList(RenderContext.SelectPointList, RenderContext.TransformContext, OperationViewModel);

            RenderContext.SelectPointList.Clear();
            _diapenseSelectionAdorner.InvalidateVisual();
        }

        /// <summary>
        /// 塗布点の移動
        /// </summary>
        private void MovePoint()
        {
            for (int i = 0; i < RenderContext.SelectPointList.Count; i++)
            {
                RenderContext.SelectPointList[i].X += OperationModel.OffsetX;
                RenderContext.SelectPointList[i].Y += OperationModel.OffsetY;
            }
            RenderContext.SelectPointList.Clear();
            _dispensePointLayer.InvalidateVisual();
            _diapenseSelectionAdorner.InvalidateVisual();
        }

        /// <summary>
        /// 塗布点の削除
        /// </summary>
        private void DeletePoint()
        {
            RenderContext.RemoveSelectedPointsFromClusters();            
            RenderContext.SelectPointList.Clear();
            _dispensePointLayer.InvalidateVisual();
            _diapenseSelectionAdorner.InvalidateVisual();
        }

        /// <summary>
        /// 選択された点で再グルーピング
        /// </summary>
        private void GroupingPoint()
        {
            RenderContext.SelectedCluster = null;
            RenderContext.ReGrouping();

            int count = 1;
            foreach (var item in RenderContext.ClusterList)
            {
                item.ViewColor = ColorHelper.GetColorByIndex(count);
                count++;
            }
            ReCreateRoute();
            _dispensePointLayer.InvalidateVisual();
        }
    }

    public static class TreeViewSelectedItemBehavior
    {
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItem",
                typeof(object),
                typeof(TreeViewSelectedItemBehavior),
                new PropertyMetadata(null, OnSelectedItemChanged));

        public static object GetSelectedItem(DependencyObject obj)
        {
            return (object)obj.GetValue(SelectedItemProperty);
        }

        public static void SetSelectedItem(DependencyObject obj, object value)
        {
            obj.SetValue(SelectedItemProperty, value);
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var treeView = d as TreeView;
            if (treeView == null)
                return;

            // ViewModel → View の選択処理
            if (e.NewValue != null)
            {
                SelectItemInTreeView(treeView, e.NewValue);
            }

            // View → ViewModel のSelectedItemの流し込み
            treeView.SelectedItemChanged -= TreeView_SelectedItemChanged;
            treeView.SelectedItemChanged += TreeView_SelectedItemChanged;
        }

        private static void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var treeView = sender as TreeView;
            SetSelectedItem(treeView, e.NewValue); // View → VM
        }


        // -------------------------------
        // TreeViewItem を検索して選択する
        // -------------------------------
        private static void SelectItemInTreeView(TreeView treeView, object target)
        {
            treeView.UpdateLayout();
            var tvi = GetTreeViewItem(treeView, target);
            if (tvi != null)
            {
                tvi.IsSelected = true;
                tvi.Focus();
            }
        }

        // TreeViewItem を VisualTree から探す（再帰）
        private static TreeViewItem GetTreeViewItem(ItemsControl container, object item)
        {
            if (container == null) return null;

            for (int i = 0; i < container.Items.Count; i++)
            {
                var child = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (child == null) continue;

                if (child.DataContext == item)
                    return child;

                var childNode = GetTreeViewItem(child, item);
                if (childNode != null)
                    return childNode;
            }
            return null;
        }
    }

    public class ApertureDropDownItem
    {
        public string key { get; set; } = "";
        public string Code { get; set; } = "<未選択>";

        public override string ToString()
        {
            return Code;
        }
    }
}