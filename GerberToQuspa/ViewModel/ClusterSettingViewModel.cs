using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GerberToQuspa.ViewModel
{
    public enum eClusterPriority { Auto_Turn, Auto_Distance, X, Y }

    public class ClusterSettingModel : BindableBase
    {
        /// <summary>
        /// クラスタリング範囲
        /// </summary>
        private bool _isEpsAuto = true;
        public bool IsEpsAuto
        {
            get { return _isEpsAuto; }
            set { SetProperty(ref _isEpsAuto, value); }
        }
        private double _epsX = 4.000;
        public double EpsX
        {
            get { return _epsX; }
            set { SetProperty(ref _epsX, value); }
        }
        private double _epsY = 4.000;
        public double EpsY
        {
            get { return _epsY; }
            set { SetProperty(ref _epsY, value); }
        }

        /// <summary>
        /// クラスタリング最小ポイント数
        /// </summary>
        private int _clusterMinSamples = 3;
        public int ClusterMinSamples
        {
            get { return _clusterMinSamples; }
            set { SetProperty(ref _clusterMinSamples, value); }
        }

        /// <summary>
        /// 優先度
        /// </summary>
        private eClusterPriority _priority = eClusterPriority.Auto_Turn;
        public eClusterPriority Priority
        {
            get { return _priority; }
            set { SetProperty(ref _priority, value); }
        }

        /// <summary>
        /// 軸の反転
        /// </summary>
        public bool IsMirrorX { get; set; }
        public bool IsMirrorY { get; set; }
    }

    public class ClusterSettingViewModel : BindableBase
    {
        public ClusterSettingModel Model = new ClusterSettingModel();

        /// <summary>
        /// クラスタリング範囲
        /// </summary>
        public bool IsEpsAuto
        {
            get { return Model.IsEpsAuto; }
            set { SetProperty(() => Model.IsEpsAuto, v => Model.IsEpsAuto = v, value); }
        }
        public double EpsX
        {
            get { return Model.EpsX; }
            set { SetProperty(() => Model.EpsX, v => Model.EpsX = v, value); }
        }
        public double EpsY
        {
            get { return Model.EpsY; }
            set { SetProperty(() => Model.EpsY, v => Model.EpsY = v, value); }
        }

        /// <summary>
        /// クラスタリング最小ポイント数
        /// </summary>
        public int ClusterMinSamples
        {
            get { return Model.ClusterMinSamples; }
            set { SetProperty(() => Model.ClusterMinSamples, v => Model.ClusterMinSamples = v, value); }
        }

        /// <summary>
        /// 優先度
        /// </summary>
        public eClusterPriority Priority
        {
            get { return Model.Priority; }
            set { SetProperty(() => Model.Priority, v => Model.Priority = v, value); }
        }

        /// <summary>
        /// 軸の反転
        /// </summary>
        public bool IsMirrorX
        {
            get { return Model.IsMirrorX; }
            set { SetProperty(() => Model.IsMirrorX, v => Model.IsMirrorX = v, value); }
        }
        public bool IsMirrorY
        {
            get { return Model.IsMirrorY; }
            set { SetProperty(() => Model.IsMirrorY, v => Model.IsMirrorY = v, value); }
        }

        public DelegateCommand ClusterMinSamplesUpCommand { get; }
        public DelegateCommand ClusterMinSamplesDownCommand { get; }

        public ClusterSettingViewModel()
        {
            ClusterMinSamplesUpCommand = new DelegateCommand(ClusterMinSamplesUp);
            ClusterMinSamplesDownCommand = new DelegateCommand(ClusterMinSamplesDown);
        }

        /// <summary>
        /// クラスタリング最小ポイント数の変更
        /// </summary>
        private void ClusterMinSamplesUp()
        {
            ClusterMinSamples = ClusterMinSamples + 1;
        }
        private void ClusterMinSamplesDown()
        {
            if (ClusterMinSamples != 1)
            {
                ClusterMinSamples = ClusterMinSamples - 1;
            }
        }
    }
}
