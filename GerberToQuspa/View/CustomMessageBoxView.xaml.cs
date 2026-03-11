using GerberToQuspa.ViewModel;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace GerberToQuspa.View
{
    /// <summary>
    /// CustomMessageBoxView.xaml の相互作用ロジック
    /// </summary>
    public partial class CustomMessageBoxView : Window
    {
        public CustomMessageBoxViewModel ViewModel { get; private set; }

        public CustomMessageBoxView()
        {
            InitializeComponent();
        }
        public CustomMessageBoxView(enumInfoState info, eMessageStyle style, string title, string messsage, string subMessag = "")
        {
            InitializeComponent();

            ViewModel = new CustomMessageBoxViewModel(info, style, title, messsage, subMessag);
            this.DataContext = ViewModel;
        }
        // カスタムタイトルバーのドラッグ処理
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}
