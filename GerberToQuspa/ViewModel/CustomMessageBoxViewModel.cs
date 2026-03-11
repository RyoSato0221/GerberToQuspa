using GerberToQuspa.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GerberToQuspa.ViewModel
{
    public enum enumInfoState
    {
        Info,
        Warning,
        Error
    }

    // メッセージのスタイル
    public enum eMessageStyle
    {
        OK,
        YesNo,
        YesNoCancel,
    }

    // メッセージボックスの結果戻り値
    public enum eMessageBoxResult
    {
        Yes,
        No,
        Cancel
    }

    /// -------------------------------------------------------------------------------
    /// <summary>
    /// 拡張メッセージボックス表示クラス
    /// Styleを定義し機能を拡張
    /// </summary>
    public class CustomMessageBox
    {
        private eMessageStyle _style = eMessageStyle.OK;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="style"></param>
        public CustomMessageBox(eMessageStyle style)
        {
            _style = style;
        }

        /// <summary>
        /// メッセージボックスの表示
        /// </summary>
        /// <param name="info"></param>
        /// <param name="title"></param>
        /// <param name="messsage"></param>
        /// <param name="subMessage"></param>
        /// <returns></returns>
        public eMessageBoxResult Show(enumInfoState info, string title, string messsage, string subMessage = "")
        {
            eMessageBoxResult result = eMessageBoxResult.No;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dlg = new CustomMessageBoxView(info, _style, title, messsage, subMessage);
                dlg.ShowDialog();
                result = dlg.ViewModel.Result;
            });
            return result;
        }
    }

    /// -------------------------------------------------------------------------------
    public class CustomMessageBoxViewModel : BindableBase
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string SubMessage { get; set; } = "";
        public eMessageStyle Style { get; } = eMessageStyle.OK;
        public eMessageBoxResult Result { get; set; } = eMessageBoxResult.Yes;

        private enumInfoState _state = enumInfoState.Info;
        public enumInfoState State
        {
            get { return _state; }
            set { SetProperty(ref _state, value); }
        }

        public DelegateCommand<object> SelectYesCommand { get; }
        public DelegateCommand<object> SelectNoCommand { get; }
        public DelegateCommand<object> SelectCancelCommand { get; }
        public DelegateCommand<object> SelectOkCommand { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="info"></param>
        /// <param name="style"></param>
        /// <param name="title"></param>
        /// <param name="messsage"></param>
        /// <param name="subMessage"></param>
        public CustomMessageBoxViewModel(enumInfoState info, eMessageStyle style, string title, string messsage, string subMessage)
        {
            State = info;
            Style = style;
            Title = title;
            Message = messsage;
            SubMessage = subMessage;

            SelectYesCommand = new DelegateCommand<object>(SelectYes);
            SelectNoCommand = new DelegateCommand<object>(SelectNo);
            SelectCancelCommand = new DelegateCommand<object>(SelectCancel);
            SelectOkCommand = new DelegateCommand<object>(SelectOK);
        }

        /// <summary>
        /// 「Yes」ボタン操作
        /// </summary>
        /// <param name="parameter"></param>
        private void SelectYes(object parameter)
        {
            Result = eMessageBoxResult.Yes;

            if (parameter is Window)
            {
                Window window = (Window)parameter;
                window.DialogResult = true;
                window.Close();
            }
        }

        /// <summary>
        /// 「No」ボタン操作
        /// </summary>
        /// <param name="parameter"></param>
        private void SelectNo(object parameter)
        {
            Result = eMessageBoxResult.No;

            if (parameter is Window)
            {
                Window window = (Window)parameter;
                window.DialogResult = true;
                window.Close();
            }
        }

        /// <summary>
        /// 「Cancel」ボタン操作
        /// </summary>
        /// <param name="parameter"></param>
        private void SelectCancel(object parameter)
        {
            Result = eMessageBoxResult.Cancel;

            if (parameter is Window)
            {
                Window window = (Window)parameter;
                window.DialogResult = true;
                window.Close();
            }
        }

        /// <summary>
        /// 「Yes」ボタン操作
        /// </summary>
        /// <param name="parameter"></param>
        private void SelectOK(object parameter)
        {
            Result = eMessageBoxResult.Yes;

            if (parameter is Window)
            {
                Window window = (Window)parameter;
                window.DialogResult = true;
                window.Close();
            }
        }
    }
}