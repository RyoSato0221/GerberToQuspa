using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GerberToQuspa
{
    public class ProgressReportMessage
    {
        public string Message;
        public double Percent;
    }

    class ProgressDisplayViewModel : BindableBase
    {
        private int _progressValue = 0;
        public int ProgressValue
        {
            get { return _progressValue; }
            set
            {
                SetProperty(ref _progressValue, value);
                ProgressText = $"進捗: {value}%";
            }
        }
        private string _progressText = "進捗: 0%";
        public string ProgressText
        {
            get { return _progressText; }
            set { SetProperty(ref _progressText, value); }
        }
    }

    public class ProgressHelper
    {
        private ProgressDisplayViewModel _vm;
        private ProgressDisplayView _view;

        public IProgress<ProgressReportMessage> Progress { get; private set; }

        public ProgressHelper(string title = "進捗状況")
        {
            _vm = new ProgressDisplayViewModel();
            _view = new ProgressDisplayView { DataContext = _vm, Title = title };
            _view.Show();

            Progress = new Progress<ProgressReportMessage>(value =>
            {
                _vm.ProgressValue = (int)value.Percent;
                _vm.ProgressText = $"{value.Message}... 進捗: {(int)value.Percent}%";
            });
        }

        public void SetText(string text)
        {
            _vm.ProgressText = text;
        }

        public void Close()
        {
            _view.Close();
        }
    }
}
