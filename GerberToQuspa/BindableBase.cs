using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GerberToQuspa
{
    public class BindableBase : INotifyPropertyChanged
    {
        /// <summary>
        /// INotifyPropertyChanged を実装するためのイベントハンドラ
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// fieldを変更してPropertyChangedイベントを発生させる
        /// true: PropertyChangedイベントを発生させた, false: 値は変更なし。イベントなし
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <returns>true: PropertyChangedイベントを発生させた, false: 値は変更なし。イベントなし</returns>
        protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName]string propertyName = null)
        {
            if (Equals(field, value)) { return false; }
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        /// <summary>
        /// プロパティを変更してPropertyChangedイベントを発生させる
        /// 使用例
        /// SetProperty(() => PropatyName, v => PropatyName = v, value);
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        /// <param name="value"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        protected bool SetProperty<T>(Func<T> getter, Action<T> setter, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(getter(), value)) { return false; }
            setter(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        /// <summary>
        /// PropertyChangedの発火
        /// </summary>
        /// <param name="propertyName"></param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (Application.Current == null)
            {
                // MS Test時はApplication.Currentがnullなのでこちらの処理にしておく
                return;
            }
            // WPF では、UI の更新は UI スレッドで実行する必要がある ため、
            // Dispatcher を使って UI スレッドで PropertyChanged を発火 させます。
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // UIスレッドならそのまま実行
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                // 別スレッドなら UI スレッドに切り替えて実行
                Application.Current.Dispatcher.Invoke(() =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))
                );
            }
        }
    }
}
