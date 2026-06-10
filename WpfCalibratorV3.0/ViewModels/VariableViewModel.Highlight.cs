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

        // 1. Нахождение ближайшей точки по оси X
        int colIdx = 0;
        if (currentInputX <= axisXData[0])
        {
            colIdx = 0;
        }
        else if (currentInputX >= axisXData[Cols - 1])
        {
            colIdx = Cols - 1;
        }
        else
        {
            // Ищем, между какими двумя точками находится сигнал
            for (int i = 0; i < Cols - 1; i++)
            {
                if (currentInputX >= axisXData[i] && currentInputX <= axisXData[i + 1])
                {
                    // Округляем к ближайшей точке
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
        }
        else if (currentInputY >= axisYData[Rows - 1])
        {
            rowIdx = Rows - 1;
        }
        else
        {
            for (int i = 0; i < Rows - 1; i++)
            {
                if (currentInputY >= axisYData[i] && currentInputY <= axisYData[i + 1])
                {
                    float distToLeft = Math.Abs(currentInputY - axisYData[i]);
                    float distToRight = Math.Abs(axisYData[i + 1] - currentInputY);
                    rowIdx = distToLeft < distToRight ? i : i + 1;
                    break;
                }
            }
        }

        // 3. Обновляем индексы подсветки
        ActiveRowIndex = rowIdx;
        ActiveColIndex = colIdx;

        // 4. Перегенерируем коллекцию ячеек с новой подсветкой
        RebuildMatrixCells(); 
    }
}