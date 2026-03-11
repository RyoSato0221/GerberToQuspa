using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GerberToQuspa
{
    /// <summary>
    /// MainWindowView.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindowView : Window
    {
        MainWindowViewModel vm;

        public MainWindowView()
        {
            InitializeComponent();

            vm = new MainWindowViewModel(gerberCanvas, dispenseCanvas, dispensePointLayer, gerberRenderLayer);
            this.DataContext = vm;

            mainGrid.PreviewMouseWheel += vm.OnMouseWheel;
            mainGrid.PreviewMouseMove += vm.OnMouseMove;
            mainGrid.PreviewMouseLeftButtonDown += vm.OnMouseDown;
            mainGrid.PreviewMouseLeftButtonUp += vm.OnMouseUp;
            mainGrid.PreviewMouseRightButtonDown += vm.OnMouseRightDown;
            mainGrid.PreviewMouseRightButtonUp += vm.OnMouseRightUp;
        }

        private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            vm.ViewWidth = mainGrid.ActualWidth;
            vm.ViewHeight = mainGrid.ActualHeight;
        }        
    }
}
