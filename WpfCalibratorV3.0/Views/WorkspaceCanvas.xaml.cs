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
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Views
{
    /// <summary>
    /// Логика взаимодействия для WorkspaceCanvas.xaml
    /// </summary>
    public partial class WorkspaceCanvas : UserControl
    {
        public WorkspaceCanvas()
        {
            InitializeComponent();
        }
        private void Canvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(VariableConfig)) && sender is Canvas canvas)
            {
                var variable = (VariableConfig)e.Data.GetData(typeof(VariableConfig));
                if (variable == null || DataContext is not MainViewModel vm) return;

                Point dropPosition = e.GetPosition(canvas);

                // Инженерная магнитная сетка с шагом 10 пикселей
                const double gridStep = 10.0;
                double snappedX = Math.Round(dropPosition.X / gridStep) * gridStep;
                double snappedY = Math.Round(dropPosition.Y / gridStep) * gridStep;

                if (variable.IsParam)
                {
                    CreateWidgetOnWorkspace(vm, variable, snappedX, snappedY, "Default");
                }
                else
                {
                    ShowWidgetSelectorMenu(canvas, vm, variable, snappedX, snappedY);
                }

                e.Handled = true;
            }
        }

        private void ShowWidgetSelectorMenu(Canvas canvas, MainViewModel vm, VariableConfig variable, double x, double y)
        {
            var menu = new ContextMenu();

            var itemDisplay = new MenuItem { Header = "🔢 Крупные цифры" };
            itemDisplay.Click += (s, e) => CreateWidgetOnWorkspace(vm, variable, x, y, "Digital");

            var itemSlider = new MenuItem { Header = "📊 Линейный индикатор (Слайдер)" };
            itemSlider.Click += (s, e) => CreateWidgetOnWorkspace(vm, variable, x, y, "Slider");

            var itemGauge = new MenuItem { Header = "🧭 Стрелочный прибор (Gauge)" };
            itemGauge.Click += (s, e) => CreateWidgetOnWorkspace(vm, variable, x, y, "Gauge");

            menu.Items.Add(itemDisplay);
            menu.Items.Add(itemSlider);
            menu.Items.Add(itemGauge);

            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void CreateWidgetOnWorkspace(MainViewModel vm, VariableConfig variable, double x, double y, string viewType)
        {
            var variableVm = new VariableViewModel(variable, variable.ModelId);

            var widget = new WidgetViewModel
            {
                Left = x,
                Top = y,
                ControlView = viewType,
                DataSource = variableVm
            };

            if (variable.IsParam && variable.TotalElements > 1)
            {
                widget.Width = 450;
                widget.Height = 250;
            }
            else
            {
                widget.Width = 220;
                widget.Height = 70;
            }

            // РАСКОММЕНТИРОВАНО: 
            // Так как у вас в MainViewModel пока используются списки ParameterVariables/TelemetryVariables,
            // для отображения на гибком холсте мы временно закидываем созданную переменную в ParameterVariables.
            // Чуть позже мы заведем для холста отдельную чистую коллекцию WorkspaceWidgets.
            if (variable.IsParam)
            {
                vm.ParameterVariables.Add(variableVm);
            }
            else
            {
                vm.TelemetryVariables.Add(variableVm);
            }
        }


    }
}
