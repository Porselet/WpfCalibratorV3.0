using System;
using System.Collections.Generic;
using System.Text;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public class MatrixTableWidgetViewModel : EditableWidgetViewModel
    {
        public MatrixTableWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {
            
        }




        private bool _showRadarTracker = true;
        /// <summary>
        /// Настройка UI: разрешает или запрещает отображение зелёного неонового маркера-прицела
        /// текущей рабочей точки поверх сетки калибровочной таблицы.
        /// </summary>

        public bool ShowRadarTracker
        {
            get => _showRadarTracker;
            set { if (_showRadarTracker != value) { _showRadarTracker = value; OnPropertyChanged(); } }
        }


        private bool _show3DSurface;
        /// <summary>
        /// Настройка UI: переключает графический виджет таблицы в режим отрисовки 
        /// трехмерной полигональной горы рельефа Helix Toolkit.
        /// </summary>
        public bool Show3DSurface
        {
            get => _show3DSurface;
            set { if (_show3DSurface != value) { _show3DSurface = value; OnPropertyChanged(); } }
        }

    }
}
