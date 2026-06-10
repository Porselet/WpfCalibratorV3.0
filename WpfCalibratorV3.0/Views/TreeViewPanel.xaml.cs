using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfCalibrator.Models;

namespace WpfCalibrator.Views
{
    /// <summary>
    /// Логика взаимодействия для TreeViewPanel.xaml
    /// </summary>
    public partial class TreeViewPanel : UserControl
    {
        private Point _startPoint;
        private bool _isDragging = false;
        public TreeViewPanel()
        {
            InitializeComponent();
        }
        private void VariableNode_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }
        // ==================== ЛОГИКА DRAG (ЗАХВАТ ИЗ ДЕРЕВА) ====================


        private void VariableNode_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumHorizontalDragDistance)
            {
                // ИСПРАВЛЕНО: sender теперь это TreeViewItem
                if (sender is TreeViewItem treeViewItem && treeViewItem.DataContext is VariableConfig variableConfig)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(treeViewItem, variableConfig, DragDropEffects.Move);
                    _isDragging = false;
                    e.Handled = true;
                }
            }
        }
    }

}
