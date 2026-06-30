using System;

namespace WpfCalibrator.ViewModels
{
    /// <summary>
    /// Промежуточный базовый класс для всех интерактивных калибровочных таблиц (1D и 3D)
    /// </summary>
    public abstract class TableVariableViewModelBase : VariableViewModelBase
    {
        private int _selectedRow;
        private int _selectedCol;
        private int _anchorRow;
        private int _anchorCol;
        private bool _isEditing;

        public int SelectedRow { get => _selectedRow; set { _selectedRow = value; OnPropertyChanged(); } }
        public int SelectedCol { get => _selectedCol; set { _selectedCol = value; OnPropertyChanged(); } }
        public int AnchorRow { get => _anchorRow; set { _anchorRow = value; OnPropertyChanged(); } }
        public int AnchorCol { get => _anchorCol; set { _anchorCol = value; OnPropertyChanged(); } }
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }

        /// <summary>
        /// Локальное обновление подсветки для одномерной шкалы
        /// </summary>
        public abstract void UpdateSelectionHighlight();
    }
}
