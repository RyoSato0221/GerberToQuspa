using GerberToQuspa.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GerberToQuspa
{
    public class ClusteringPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int Cluster { get; set; }

        public ClusteringPoint()
        {
        }
        public ClusteringPoint(double x, double y)
        {
            X = x;
            Y = y;
            Cluster = -1;
        }
    }

    public class RouteSearch
    {
        /// <summary>
        /// クラスタリングの実行
        /// </summary>
        /// <param name="points"></param>
        /// <param name="setting"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public List<Cluster> Clustering(List<PointVM> points, ClusterSettingModel setting, IProgress<ProgressReportMessage> progress = null)
        {
            progress?.Report(new ProgressReportMessage { Message = "クラスタリング実行中...", Percent = 0 });

            // クラスタリング距離の設定
            double epsX = setting.EpsX, epsY = setting.EpsY;
            if (setting.IsEpsAuto)
            {
                epsX = epsY = FindMinDistance(points) * 2;
            }

            // Xスケーリング
            double[][] data = new double[points.Count][];
            for (int i = 0; i < points.Count; i++)
            {
                data[i] = new double[] { points[i].X * (epsY / epsX), points[i].Y };
            }

            // クラスタリングの実行
            var db1 = new DBSCAN(epsY, setting.ClusterMinSamples);
            int[] labels1 = db1.Fit(data, progress);

            // ノイズ（-1）を抽出
            var noisePoints = new List<double[]>();
            var noiseIndices = new List<int>();
            for (int i = 0; i < data.Length; i++)
            {
                if (labels1[i] == -1)
                {
                    noisePoints.Add(data[i]);
                    noiseIndices.Add(i);
                }
            }

            // ノイズが無くなるまでクラスタリングを続ける
            int cnt = 2;
            while (noisePoints.Count > setting.ClusterMinSamples)
            {
                // クラスタIDの最大値を取得
                int maxClusterId = labels1.Max();
                // 2回目（条件を緩く）
                var db2 = new DBSCAN(epsY * cnt, setting.ClusterMinSamples);
                int[] labels2 = db2.Fit(noisePoints.ToArray(), progress, maxClusterId, new int[noisePoints.Count]);

                // ノイズ部分のラベルを元配列に反映
                for (int i = 0; i < noiseIndices.Count; i++)
                {
                    labels1[noiseIndices[i]] = labels2[i];
                }
                // ノイズ（-1）を抽出
                noisePoints = new List<double[]>();
                noiseIndices = new List<int>();
                for (int i = 0; i < data.Length; i++)
                {
                    if (labels1[i] == -1)
                    {
                        noisePoints.Add(data[i]);
                        noiseIndices.Add(i);
                    }
                }
                cnt++;
            }

            // クラスタグループごとにList化する
            var clusterGroups =
                points.Zip(labels1, (p, label) => new { Point = p, Cluster = label })
                .GroupBy(x => x.Cluster)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.Point).ToList());            

            // クラスタグループごとにList化する
            List<Cluster> clusterList = new List<Cluster>();

            int index = 1;
            foreach(var item in clusterGroups)
            {
                Cluster cluster = new Cluster
                {
                    Name = $"クラスタ_{index}",
                    ViewColor = ColorHelper.GetColorByIndex(index),
                    PointList = new ObservableCollection<PointVM>(item.Value),
                };
                clusterList.Add(cluster);
                index++;
            }
            
            progress?.Report(new ProgressReportMessage { Message = "クラスタリング完了", Percent = 50 });
            Thread.Sleep(50);
            return clusterList;
        }

        public List<Cluster> GenerateOptimizedPath(List<Cluster> cluters, eClusterPriority priority, IProgress<ProgressReportMessage> progress = null)
        {
            // クラスタ順序を決定
            var orderedClusters = OrderClusters(cluters);
            var optimizedPath = new List<Cluster>();

            // 各クラスタごとの処理
            int clusterCount = orderedClusters.Count;

            foreach(var item in orderedClusters)
            {
                int groupCountX = 0, groupCountY = 0;
                Cluster XsortedCluster = new Cluster()
                {
                    PointList = SortWithinCluster(item.PointList.ToList(), ref groupCountX, 'Y'),
                    ViewColor = item.ViewColor,
                    Name = item.Name,
                };
                Cluster YsortedCluster = new Cluster()
                {
                    PointList = SortWithinCluster(item.PointList.ToList(), ref groupCountY, 'X'),
                    ViewColor = item.ViewColor,
                    Name = item.Name,
                };

                var distanceX = CalculateTotalDistance(XsortedCluster.PointList);
                var distanceY = CalculateTotalDistance(YsortedCluster.PointList);

                if (priority == eClusterPriority.Auto_Distance)
                {
                    if (distanceX == distanceY)
                    {
                        if (groupCountX > groupCountY)
                        {
                            optimizedPath.Add(YsortedCluster);
                        }
                        else
                        {
                            optimizedPath.Add(XsortedCluster);
                        }
                    }
                    else if (distanceX > distanceY)
                    {
                        optimizedPath.Add(YsortedCluster);
                    }
                    else
                    {
                        optimizedPath.Add(XsortedCluster);
                    }
                }
                else if (priority == eClusterPriority.Auto_Turn)
                {
                    if (groupCountX == groupCountY)
                    {
                        if (CalculateTotalDistance(XsortedCluster.PointList) > CalculateTotalDistance(YsortedCluster.PointList))
                        {
                            optimizedPath.Add(YsortedCluster);
                        }
                        else
                        {
                            optimizedPath.Add(XsortedCluster);
                        }
                    }
                    else if (groupCountX > groupCountY)
                    {
                        optimizedPath.Add(YsortedCluster);
                    }
                    else
                    {
                        optimizedPath.Add(XsortedCluster);
                    }
                }
                else if (priority == eClusterPriority.X)
                {
                    optimizedPath.Add(XsortedCluster);
                }
                else
                {
                    optimizedPath.Add(YsortedCluster);
                }               
            }

            // 結合して順序最適化
            orderedClusters = OptimizeOrder(optimizedPath);

            progress?.Report(new ProgressReportMessage { Message = "経路最適化完了", Percent = 100 });
            Thread.Sleep(50);
            
            return optimizedPath;
        }

        /// <summary>
        /// クラスタ間の順序決定（最短距離法）
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public List<Cluster> OrderClusters(List<Cluster> clusters)
        {
            double minX = clusters.Min(p => p.GetCentroid.X);
            double maxX = clusters.Max(p => p.GetCentroid.X);
            double minY = clusters.Min(p => p.GetCentroid.Y);
            double maxY = clusters.Max(p => p.GetCentroid.Y);

            double stepX = (maxX - minX) / 4.0;
            double stepY = (maxY - minY) / 4.0;

            // 16個のリスト
            List<Cluster>[,] grid = new List<Cluster>[4, 4];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    grid[x, y] = new List<Cluster>();
                }
            }

            // 各点を分類
            foreach (var p in clusters)
            {
                int ix = (int)((p.GetCentroid.X - minX) / stepX);
                int iy = (int)((p.GetCentroid.Y - minY) / stepY);

                // 境界対策
                ix = Math.Min(ix, 3);
                iy = Math.Min(iy, 3);

                grid[ix, iy].Add(p);
            }

            var clusterOrder = new List<Cluster>();

            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (y % 2 == 0)
                {
                    for (int x = 0; x < grid.GetLength(0); x++)
                    {
                        Order(clusterOrder, grid[x, y]);
                    }
                }
                else
                {
                    for (int x = grid.GetLength(0) - 1; x >= 0; x--)
                    {
                        Order(clusterOrder, grid[x, y]);
                    }
                }                
            }

            // 整列したクラスタリストを返す
            return clusterOrder;
        }
        
        private void Order(List<Cluster> order, List<Cluster> current)
        {
            if (current.Count == 0) return;

            Cluster baseCluster = new Cluster();
            if (order.Count == 0)
            {
                // 原点 (0,0) に最も近いクラスタを基点にする
                baseCluster = current
                    .Where(c => c.PointList != null && c.PointList.Count > 0)
                    .OrderBy(c =>
                    {
                        var p = c.GetCentroid;
                        return (p.X * p.X) + (p.Y * p.Y);
                    })
                    .FirstOrDefault();
            }
            else
            {
                var lastCenter = order.Last();
                baseCluster = current
                    .OrderBy(c => Distance(lastCenter.GetCentroid, c.GetCentroid))
                    .First();
            }

            // 起点クラスタを登録
            order.Add(baseCluster);

            // 残りのクラスタを準備
            var remainingClusters = new List<Cluster>(current.Where(c => c != baseCluster));

            // 最近傍クラスタを順に追加
            while (remainingClusters.Any())
            {
                var lastCenter = order.Last();

                var nextCluster = remainingClusters
                    .OrderBy(c => Distance(lastCenter.GetCentroid, c.GetCentroid))
                    .First();

                order.Add(nextCluster);
                remainingClusters.Remove(nextCluster);
            }
        }
    


        /// <summary>
        /// クラスタ内のソート（ラスター走査）
        /// </summary>
        /// <param name="clusterPoints"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public ObservableCollection<PointVM> SortWithinCluster(List<PointVM> points, ref int groupCount, char priority = 'Y')
        {
            // 優先軸で全体をソート
            List<PointVM> sorted;

            if (priority == 'Y')
                sorted = points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            else
                sorted = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();

            // 優先軸ごとにグループ化
            var grouped = (priority == 'Y')
                ? sorted.GroupBy(p => p.Y).ToList()
                : sorted.GroupBy(p => p.X).ToList();

            var result = new List<PointVM>();
            int i = 0;

            // 偶数行は順方向、奇数行は逆方向に追加
            foreach (var group in grouped)
            {
                var rowPoints = group.ToList();
                if (i % 2 == 1)
                    rowPoints.Reverse();

                result.AddRange(rowPoints);
                i++;
            }
            groupCount = grouped.Count();
            return new ObservableCollection<PointVM>(result);
        }

        /// <summary>
        /// クラスタ間およびクラスタ内の順序を調整して距離を最小化する
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public List<Cluster> OptimizeOrder(List<Cluster> clusterOrder)
        {
            // --- クラスタ間の順序改善 ---
            bool improved = true;
            while (improved)
            {
                improved = false;

                for (int i = 0; i < clusterOrder.Count - 1; i++)
                {
                    var cluster1 = clusterOrder[i];
                    var cluster2 = clusterOrder[i + 1];

                    if (cluster1.PointList.Count == 0 || cluster2.PointList.Count == 0)
                        continue;

                    // 現在の距離（cluster1の終点→cluster2の始点）
                    double currentDistance = Distance(
                        cluster1.PointList.Last().X, cluster1.PointList.Last().Y,
                        cluster2.PointList.First().X, cluster2.PointList.First().Y
                    );

                    // 逆順時の距離（cluster1の終点→cluster2の終点）
                    double reversedDistance = Distance(
                        cluster1.PointList.Last().X, cluster1.PointList.Last().Y,
                        cluster2.PointList.Last().X, cluster2.PointList.Last().Y
                    );

                    if (reversedDistance < currentDistance)
                    {
                        cluster2.ReversePointList();
                        clusterOrder[i + 1] = cluster2;
                        improved = true;
                    }
                }
            }
            
            return clusterOrder;
        }

        /// <summary>
        /// 2点間の距離
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="y1"></param>
        /// <param name="x2"></param>
        /// <param name="y2"></param>
        /// <returns></returns>
        private double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        private double Distance(Point val1, Point val2)
        {
            double dx = val1.X - val2.X;
            double dy = val1.Y - val2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// クラスタ内の総距離を計算
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private double CalculateTotalDistance(ObservableCollection<PointVM> points)
        {
            double total = 0.0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                total += Distance(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y);
            }
            return total;
        }

        /// <summary>
        /// 最小距離の算出
        /// </summary>
        /// <returns></returns>
        private double FindMinDistance(List<PointVM> points)
        {           
            double minDistance = double.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double dx = points[i].X - points[j].X;
                    double dy = points[i].Y - points[j].Y;
                    double distance = dx * dx + dy * dy;

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                    }
                }
            }

            // 結果を距離に戻す
            return Math.Sqrt(minDistance);
        }
    }

    public class DBSCAN
    {
        public double Eps { get; set; }
        public int MinPts { get; set; }

        public DBSCAN(double eps, int minPts)
        {
            Eps = eps;
            MinPts = minPts;
        }

        public int[] Fit(double[][] points, IProgress<ProgressReportMessage> progress = null, int startClusterId = 0, int[] prevLabels = null)
        {
            int n = points.Length;
            int[] labels = prevLabels ?? new int[n];

            // prevLabels が null の場合は 0 で初期化
            if (prevLabels == null)
                for (int i = 0; i < n; i++) labels[i] = 0;

            int clusterId = startClusterId;

            for (int i = 0; i < n; i++)
            {
                if (i % 10 == 0)
                {
                    progress?.Report(new ProgressReportMessage { Message = $"クラスタリング実行中...", Percent = (double)i / n * 100 });
                }

                if (labels[i] != 0 && labels[i] != -1) continue; // 既にクラスタ済み

                var neighbors = RegionQuery(points, i);
                if (neighbors.Count < MinPts)
                {
                    labels[i] = -1; // ノイズ
                }
                else
                {
                    clusterId++;
                    ExpandCluster(points, labels, i, neighbors, clusterId);
                }
            }

            return labels;
        }

        private void ExpandCluster(double[][] points, int[] labels, int pointIndex, List<int> neighbors, int clusterId)
        {
            labels[pointIndex] = clusterId;
            Queue<int> queue = new Queue<int>(neighbors);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();

                if (labels[current] == -1)
                    labels[current] = clusterId; // ノイズをクラスタに変換

                if (labels[current] != 0)
                    continue; // すでにクラスタ割り当て済み

                labels[current] = clusterId;

                var currentNeighbors = RegionQuery(points, current);
                if (currentNeighbors.Count >= MinPts)
                {
                    foreach (var n in currentNeighbors)
                    {
                        if (!queue.Contains(n))
                            queue.Enqueue(n);
                    }
                }
            }
        }

        private List<int> RegionQuery(double[][] points, int index)
        {
            List<int> neighbors = new List<int>();
            for (int i = 0; i < points.Length; i++)
            {
                if (Distance(points[index], points[i]) <= Eps)
                    neighbors.Add(i);
            }
            return neighbors;
        }

        private double Distance(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
                sum += (a[i] - b[i]) * (a[i] - b[i]);
            return Math.Sqrt(sum);
        }
    }
}
