using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.ViewModels.WidgetViewModel;

// Часть BaseWidgetViewModel, отвечающая за обработку ввода с клавиатуры/потенциометров
public partial class BaseWidgetViewModel : INotifyPropertyChanged
{

    /// <summary>
    /// Метод атомарной фиксации ввода: парсит накопленный буфер, швыряет число в AdjustValue() переменной,
    /// гасит флаг редактирования IsEditing и полностью очищает InputBuffer
    /// </summary>
    public void CommitInputBuffer()
    {
        if (string.IsNullOrEmpty(InputBuffer)) return;

        // Пытаемся распарсить накопленный текст в физическое число double
        if (double.TryParse(InputBuffer, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedValue))
        {
            // Если привязана интерактивная таблица (1D или 3D)
            if (DataSource is TableVariableViewModelBase tableSource)
            {
                // Бежим по ячейкам и жестко фиксируем число в памяти
                foreach (var cell in tableSource.MatrixCells)
                {
                    if (cell.IsSelected)
                    {
                        // В будущем здесь вызовется цепочка OnTableDataChanged() для пересчета 3D и UART!
                        cell.ValueText = parsedValue.ToString("F2");
                    }
                }
            }
            // Если привязана одиночная константа-параметр
            else if (DataSource is ScalarVariableViewModel scalarSource && scalarSource.IsParam)
            {
                scalarSource.CurrentValue = parsedValue;
            }
        }

        // Полностью очищаем черновики виджета, гася флаг IsEditing у DataSource
        InputBuffer = string.Empty;
        OnPropertyChanged(nameof(CurrentValueText));
    }


    /// <summary>
    /// Сброс текстового буфера ввода инженера и очистка черновика набора [1.14]
    /// </summary>
    public void ClearGraphBuffer()
    {
        // Обнуляем буфер ввода, чтобы сбросить черновик набора по нажатию Escape [1.14]
        InputBuffer = string.Empty;

        // Если у тебя на виджете привязан ползущий график логов, 
        // здесь можно вызвать очистку его точек (например: GraphPoints?.Clear();)

        OnPropertyChanged(nameof(CurrentValueText));
    }


    /// <summary>
    /// Накопление строки ввода. Вызывается драйвером клавиатуры на каждый нажатый символ.
    /// Синхронно размножает вводимый текст по всей выделенной области в реальном времени.
    /// </summary>
    public void AppendToBuffer(string text)
    {
        // Накапливаем символ в локальный буфер виджета
        InputBuffer += text;

        // Если наш источник данных — интерактивная таблица (1D или 3D)
        if (DataSource is TableVariableViewModelBase tableSource)
        {
            // Размножаем черновой текст по всем выделенным синей рамкой ячейкам на экране!
            foreach (var cell in tableSource.MatrixCells)
            {
                if (cell.IsSelected)
                {
                    cell.ValueText = InputBuffer;
                }
            }
        }
        // Если это одиночная константа-параметр
        else if (DataSource is ScalarVariableViewModel scalarSource && scalarSource.IsParam)
        {
            OnPropertyChanged(nameof(CurrentValueText));
        }
    }

    private string _inputBuffer = string.Empty;
    private string _currentValueText = "0";
    /// <summary>
    /// Текстовый буфер для бесфокусного набора цифр с клавиатуры.
    /// </summary>
    public string InputBuffer
    {
        get => _inputBuffer;
        set
        {
            if (_inputBuffer == value) return;
            _inputBuffer = value;
            OnPropertyChanged();

            // Автоматически взводим твой существующий флаг IsEditing:
            // Если в буфере есть текст — значит, идет редактирование и UART заблокирован!
            IsEditing = !string.IsNullOrEmpty(_inputBuffer);

            // Уведомляем интерфейс, что текст на экране обновился
            OnPropertyChanged(nameof(CurrentValueText));
        }
    }

    /// <summary>
    /// Универсальное свойство отображения для TextBox скаляров и логов.
    /// Заменяет собой дёрганую привязку к float.
    /// </summary>
    public string CurrentValueText
    {
        get
        {
            // Если инженер сейчас набирает цифры руками — жестко выводим буфер ввода
            if (IsEditing && !string.IsNullOrEmpty(_inputBuffer))
            {
                return _inputBuffer;
            }

            // В режиме покоя — выводим наше стандартное число из UART с красивым гоночным форматом

            // Безопасно приводим к скаляру. Если это таблица — вернем пустую строку или прочерк.
            if (DataSource is ScalarVariableViewModel scalar)
            {
                return scalar.CurrentValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            }

            return "---";
        }
        set
        {
            // Этот сеттер будет вызываться только при инициализации, его не трогаем
            _currentValueText = value;
            OnPropertyChanged();
        }
    }
    /// <summary>
    /// Отмена ввода (Нажатие ESC).
    /// </summary>
    public void CancelEditing()
    {
        // 1. Полностью очищаем черновик набора и гасим флаг редактирования
        InputBuffer = string.Empty;
        IsEditing = false;

        // 2. Возвращаем на экран честные числа из памяти ОЗУ
        if (DataSource is TableVariableViewModelBase tableVar)
        {
            // Бежим по ячейкам UniformGrid и сбрасываем их текст обратно на актуальные данные из МК
            int cellIndex = 0;
            foreach (var cell in tableVar.MatrixCells)
            {
                // Вытягиваем живые числа через наш универсальный геттер таблиц
                double ramValue = tableVar.GetTableValue(cell.Row, cell.Col);
                cell.ValueText = ramValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        // 3. Уведомляем интерфейс, чтобы обновился текст TextBox для скаляров
        OnPropertyChanged(nameof(CurrentValueText));
    }


    /// <summary>
    /// Изменение числа внутри буфера на заданный шаг (Для PageUp/PageDown в режиме ввода).
    /// </summary>
    public void ChangeBufferByStep(float step)
    {
        if (DataSource == null) return;

        // Быстрое переключение через AdjustValue
        if (!IsEditing || string.IsNullOrEmpty(InputBuffer))
        {
            DataSource.AdjustValue(step);
            return;
        }

        // Ручной ввод с ограничением по лимитам
        if (float.TryParse(InputBuffer, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float currentValue))
        {
            float newValue = Math.Clamp(currentValue + step, (float)DataSource.ScaleMin, (float)DataSource.ScaleMax);
            InputBuffer = newValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Синхронизация ячейки через TableVariableViewModelBase
            if (DataSource is TableVariableViewModelBase tableVar)
            {
                var anchorCell = tableVar.MatrixCells.FirstOrDefault(c => c.Row == tableVar.SelectedRow && c.Col == tableVar.SelectedCol);
                if (anchorCell != null) anchorCell.ValueText = InputBuffer;
            }
        }
    }
}