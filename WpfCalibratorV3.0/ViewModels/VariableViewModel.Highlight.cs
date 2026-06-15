using System;

namespace WpfCalibrator.ViewModels;

public partial class VariableViewModel
{
    // Логика подсветки для таблиц
    public void CalculateWorkingPoint(float currentInputX, float currentInputY, float[] axisXData, float[] axisYData)
    {
        // Проверка: если это не таблица, или нет привязок, или массивы осей не совпадают по размерам
        if (!IsLutLinked || axisXData.Length != Cols || axisYData.Length != Rows)
        {
            ActiveRowIndex = -1;
            ActiveColIndex = -1;
            return;
        }

        // Переменные для субсеточного расчета радара (индексы левого/верхнего узла квадранта)
        int baseColIdx = 0;
        int baseRowIdx = 0;

        // 1. Нахождение ближайшей точки по оси X
        int colIdx = 0;
        if (currentInputX <= axisXData[0])
        {
            colIdx = 0;
            baseColIdx = 0;
        }
        else if (currentInputX >= axisXData[Cols - 1])
        {
            colIdx = Cols - 1;
            baseColIdx = Cols - 2; // Фиксируем на предклипповом узле для дельты
        }
        else
        {
            // Ищем, между какими двумя точками находится сигнал
            for (int i = 0; i < Cols - 1; i++)
            {
                if (currentInputX >= axisXData[i] && currentInputX <= axisXData[i + 1])
                {
                    baseColIdx = i; // Нашли базовый левый узел квадранта для радара!

                    // Ваша эталонная логика: округляем к ближайшей точке для неона таблицы
                    float distToLeft = Math.Abs(currentInputX - axisXData[i]);
                    float distToRight = Math.Abs(axisXData[i + 1] - currentInputX);
                    colIdx = distToLeft < distToRight ? i : i + 1;
                    break;
                }
            }
        }

        // 2. Аналогично для оси Y
        int rowIdx = 0;
        if (currentInputY <= axisYData[0])
        {
            rowIdx = 0;
            baseRowIdx = 0;
        }
        else if (currentInputY >= axisYData[Rows - 1])
        {
            rowIdx = Rows - 1;
            baseRowIdx = Rows - 2;
        }
        else
        {
            for (int i = 0; i < Rows - 1; i++)
            {
                if (currentInputY >= axisYData[i] && currentInputY <= axisYData[i + 1])
                {
                    baseRowIdx = i; // Нашли базовый верхний узел квадранта для радара!

                    float distToLeft = Math.Abs(currentInputY - axisYData[i]);
                    float distToRight = Math.Abs(axisYData[i + 1] - currentInputY);
                    rowIdx = distToLeft < distToRight ? i : i + 1;
                    break;
                }
            }
        }

        // ======================================================================
        // 3. МАТЕМАТИКА АВИАЦИОННОГО ПРИЦЕЛА-РАДАРА (Перенос дельт в виджет)
        // ======================================================================
        // ======================================================================
        // 3. МАТЕМАТИКА СУБСЕТОЧНОЙ ЛУПЫ ЯЧЕЙКИ (Прямое позиционирование прицела)
        // ======================================================================
        double shiftX = 0.0;
        double shiftY = 0.0;
        const double maxPixelDev = 100.0; // Максимальное отклонение от центра до края окна в пикселях

        // Расчет отклонения по горизонтали (Обороты) относительно выбранного узла colIdx
        if (colIdx >= 0 && colIdx < Cols)
        {
            float currentXNode = axisXData[colIdx];

            if (currentInputX > currentXNode && colIdx < Cols - 1)
            {
                // Сигнал ушел вправо, к следующему узлу
                float nextXNode = axisXData[colIdx + 1];
                if (nextXNode > currentXNode)
                    shiftX = ((currentInputX - currentXNode) / (nextXNode - currentXNode)) * maxPixelDev;
            }
            else if (currentInputX < currentXNode && colIdx > 0)
            {
                // Сигнал ушел влево, к предыдущему узлу (дельта со знаком минус)
                float prevXNode = axisXData[colIdx - 1];
                if (currentXNode > prevXNode)
                    shiftX = ((currentInputX - currentXNode) / (currentXNode - prevXNode)) * maxPixelDev;
            }
        }

        // Расчет отклонения по вертикали (Давление) относительно выбранного узла rowIdx
        if (rowIdx >= 0 && rowIdx < Rows)
        {
            float currentYNode = axisYData[rowIdx];

            if (currentInputY > currentYNode && rowIdx < Rows - 1)
            {
                // Сигнал ушел вниз, к следующей строке
                float nextYNode = axisYData[rowIdx + 1];
                if (nextYNode > currentYNode)
                    shiftY = ((currentInputY - currentYNode) / (nextYNode - currentYNode)) * maxPixelDev;
            }
            else if (currentInputY < currentYNode && rowIdx > 0)
            {
                // Сигнал ушел вверх, к предыдущей строке (дельта со знаком минус)
                float prevYNode = axisYData[rowIdx - 1];
                if (currentYNode > prevYNode)
                    shiftY = ((currentInputY - currentYNode) / (currentYNode - prevYNode)) * maxPixelDev;
            }
        }

        // Переносим рассчитанные сдвиги напрямую во вьюмодель виджета радара
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var radarWidget = mainVm.ActiveWidgets.FirstOrDefault(w =>
                    w.ControlView == "RadarTracker" && w.DataSource != null && w.DataSource.Name == this.Name);

                if (radarWidget != null)
                {
                    // Записываем чистые смещения от центра. Линза полетит в правильную сторону!
                    radarWidget.RadarGridOffsetX = shiftX;

                    // Инвертируем Y, так как в WPF координата Y растет сверху вниз, 
                    // а для инженера рост давления должен двигать прицел вверх!
                    radarWidget.RadarGridOffsetY = -shiftY;
                }
            });
        }
        // ======================================================================

        // 4. Обновляем индексы подсветки неона таблицы
        ActiveRowIndex = rowIdx;
        ActiveColIndex = colIdx;

        // 5. Перегенерируем коллекцию ячеек с новой подсветкой
        RebuildMatrixCells();
        // ======================================================================

        // 4. Обновляем индексы подсветки неона таблицы
        ActiveRowIndex = rowIdx;
        ActiveColIndex = colIdx;

        // 5. Перегенерируем коллекцию ячеек с новой подсветкой
        RebuildMatrixCells();
    }
}