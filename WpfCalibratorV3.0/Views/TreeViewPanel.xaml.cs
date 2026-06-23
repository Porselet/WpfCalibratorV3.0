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
                // БРОНЕБОЙНЫЙ ПОДХОД: Вместо sender берем OriginalSource (то, во что мы ТКНУЛИ мышкой вживую)
                // И поднимаемся вверх до ближайшего TreeViewItem именно этого конкретного дерева!
                if (e.OriginalSource is DependencyObject originalSource)
                {
                    var current = originalSource;
                    while (current != null && !(current is TreeViewItem))
                    {
                        current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                    }

                    if (current is TreeViewItem clickedItem && clickedItem.DataContext is VariableConfig variableConfig)
                    {
                        _isDragging = true;

                        // Намертво зажимаем e.Handled, чтобы событие не всплывало к родителям 
                        // и не перехватывалось соседними деревьями/вкладками!
                        e.Handled = true;

                        // Запускаем перетаскивание строго выделенной переменной конкретного МК
                        DragDrop.DoDragDrop(clickedItem, variableConfig, DragDropEffects.Move);

                        _isDragging = false;
                        return;
                    }
                }
            }
        }
    }

}
