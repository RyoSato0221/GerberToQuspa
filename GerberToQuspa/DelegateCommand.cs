using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GerberToQuspa
{
    /// <summary>
    /// 引数を受け付けない（引数なし）DelegateCommand
    /// </summary>
    public class DelegateCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Action action;
        private readonly Func<bool> canExecute;
        public DelegateCommand(Action action)
        {
            this.action = action;
            this.canExecute = null;
        }
        public DelegateCommand(Action action, Func<bool> canExecute)
        {
            this.action = action;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            action?.Invoke();
        }

        public void DelegateCanExecute()
        {
            // WPF では、UI の更新は UI スレッドで実行する必要がある ため、
            // Dispatcher を使って UI スレッドで PropertyChanged を発火 させます。
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // UIスレッドならそのまま実行
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // 別スレッドなら UI スレッドに切り替えて実行
                Application.Current.Dispatcher.Invoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
            }
        }
    }
    /// <summary>
    /// 任意の型の引数を1つ受け付けるDelegateCommand
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DelegateCommand<T> : ICommand
    {
        public event EventHandler CanExecuteChanged;

        private readonly Action<T> _action;
        private readonly Func<bool> _canExecute;
        public DelegateCommand(Action<T> action)
        {
            _action = action;
            _canExecute = null;
        }
        public DelegateCommand(Action<T> action, Func<bool> canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _action?.Invoke((T)parameter);
        }

        public void DelegateCanExecute()
        {
            // WPF では、UI の更新は UI スレッドで実行する必要がある ため、
            // Dispatcher を使って UI スレッドで PropertyChanged を発火 させます。
            if (Application.Current.Dispatcher.CheckAccess())
            {
                // UIスレッドならそのまま実行
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // 別スレッドなら UI スレッドに切り替えて実行
                Application.Current.Dispatcher.Invoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
