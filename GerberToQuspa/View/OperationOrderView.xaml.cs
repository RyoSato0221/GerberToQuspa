using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Interactivity;
using GerberToQuspa.ViewModel;

namespace GerberToQuspa.View
{
    /// <summary>
    /// OperationOrderView.xaml の相互作用ロジック
    /// </summary>
    public partial class OperationOrderView : UserControl
    {
        private Cluster _selectItem;

        public OperationOrderView()
        {
            InitializeComponent();
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // TextBox をクリックしたら編集目的なのでドラッグにしない
            if (VisualUpwardSearch<TextBox>((DependencyObject)e.OriginalSource) != null)
                return;

            var item = VisualUpwardSearch<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                _selectItem = item.DataContext as Cluster;
                if (_selectItem != null)
                {
                    DragDrop.DoDragDrop(item, _selectItem, DragDropEffects.Move);
                }
            }
        }

        private void TreeView_Drop(object sender, DragEventArgs e)
        {
            var targetItem = GetNearestContainer(e.OriginalSource as UIElement);
            if (targetItem == null) return;

            var targetNode = targetItem.DataContext as Cluster;
            var vm = this.DataContext as OperationOrderViewModel;

            var source = _selectItem;

            vm?.MoveRootNode(source, targetNode);
        }

        private TreeViewItem GetNearestContainer(UIElement element)
        {
            var container = element;
            while (container != null && !(container is TreeViewItem))
            {
                container = VisualTreeHelper.GetParent(container) as UIElement;
            }
            return container as TreeViewItem;
        }

        static T VisualUpwardSearch<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null && !(source is T))
                source = VisualTreeHelper.GetParent(source);
            return source as T;
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!_selectItem.IsUngroupedItems)
            {
                _selectItem.IsEditing = true;
            }
        }

        private void EditName_LostFocus(object sender, RoutedEventArgs e)
        {
            _selectItem.IsEditing = false;
        }

        private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindParent<TreeViewItem>(e.OriginalSource as DependencyObject);

            if (item != null)
            {
                item.IsSelected = true;
                e.Handled = true;
            }
        }

        private T FindParent<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            return current as T;
        }
    }
}
