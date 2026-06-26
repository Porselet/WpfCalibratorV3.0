using System;

namespace WpfCalibrator.ViewModels;

public partial class VariableViewModel
{
    // Логика подсветки для таблиц

    // ...
    public void CalculateWorkingPoint(double currentInputX, double currentInputY, double[] axisXData, double[] axisYData)
    {
        // 1. Железный щит валидации размеров
        if (!ValidateAxes(axisXData, axisYData)) return;

        // 2. Поиск базовых индексов и узлов квадранта
        FindBaseIndices(currentInputX, currentInputY, axisXData, axisYData,
            out int colIdx, out int rowIdx, out int baseColIdx, out int baseRowIdx);

        // 3. Расчет дельт для плоского авиаприцела-радара
        CalculateRadarOffsets(currentInputX, currentInputY, axisXData, axisYData, colIdx, rowIdx);

        // 4. Расчет точной плавной интерполяции для 3D лазера Маклауда
        Calculate3DLaserPosition(currentInputX, currentInputY, axisXData, axisYData, baseColIdx, baseRowIdx);

        // 5. Синхронизация неона двумерной таблицы ячеек
        ActiveRowIndex = rowIdx;
        ActiveColIndex = colIdx;
        RebuildMatrixCells();
    }
    /// <summary>
    /// Шаг 1: Проверка наличия и валидности размеров осей X и Y
    /// </summary>
    private bool ValidateAxes(double[] axisXData, double[] axisYData)
    {
        bool hasValidAxisX = BoundAxisX != null && axisXData != null && axisXData.Length == Cols;
        bool hasValidAxisY = Rows > 1 ? (BoundAxisY != null && axisYData != null && axisYData.Length == Rows) : true;

        return hasValidAxisX && hasValidAxisY;
    }
    /// <summary>
    /// Шаг 2: Поиск базовых индексов ячеек и узлов квадранта
    /// </summary>
    private void FindBaseIndices(double currentInputX, double currentInputY, double[] axisXData, double[] axisYData,
        out int colIdx, out int rowIdx, out int baseColIdx, out int baseRowIdx)
    {
        colIdx = 0;
        baseColIdx = 0;
        if (currentInputX <= axisXData[0]) { /* уже 0 */ }
        else if (currentInputX >= axisXData[Cols - 1])
        {
            colIdx = Cols - 1;
            baseColIdx = Cols - 2;
        }
        else
        {
            for (int i = 0; i < Cols - 1; i++)
            {
                if (currentInputX >= axisXData[i] && currentInputX <= axisXData[i + 1])
                {
                    baseColIdx = i;
                    colIdx = Math.Abs(currentInputX - axisXData[i]) < Math.Abs(axisXData[i + 1] - currentInputX) ? i : i + 1;
                    break;
                }
            }
        }
        rowIdx = 0;
        baseRowIdx = 0;
        if (Rows > 1 && axisYData != null)
        {
            if (currentInputY <= axisYData[0]) { /* уже 0 */ }
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
                        baseRowIdx = i;
                        rowIdx = Math.Abs(currentInputY - axisYData[i]) < Math.Abs(axisYData[i + 1] - currentInputY) ? i : i + 1;
                        break;
                    }
                }
            }
        }

    }

    private void CalculateRadarOffsets(double inputX, double inputY, double[] axX, double[] axY, int cIdx, int rIdx)
    {
        // Логика расчета смещения (sX/sY) внутри ячейки сетки (maxDev = 100px)
        double sX = 0.0, sY = 0.0;
        const double maxDev = 100.0;

        // Расчет для X
        if (cIdx >= 0 && cIdx < Cols - 1)
            sX = ((inputX - axX[cIdx]) / (axX[cIdx + 1] - axX[cIdx])) * maxDev;

        // Расчет для Y
        if (rIdx >= 0 && rIdx < Rows - 1 && axY != null)
            sY = ((inputY - axY[rIdx]) / (axY[rIdx + 1] - axY[rIdx])) * maxDev;

        // Обновление ViewModel (UI) через Dispatcher
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var widget = mainVm.ActiveWidgets.FirstOrDefault(w => w.ControlView == "RadarTracker" && w.DataSource?.Name == this.Name);
                if (widget != null)
                {
                    widget.RadarGridOffsetX = sX;
                    widget.RadarGridOffsetY = -sY; // Инверсия Y для экрана
                }
            });
        }
    }
    private void Calculate3DLaserPosition(double inputX, double inputY, double[] axX, double[] axY, int bCol, int bRow)
    {
        double exactCol = bCol;
        double exactRow = bRow;

        // Считаем дробную долю по оси X (Обороты)
        if (bCol >= 0 && bCol < Cols - 1 && axX[bCol + 1] > axX[bCol])
        {
            double factorX = (inputX - axX[bCol]) / (axX[bCol + 1] - axX[bCol]);
            exactCol += Math.Max(0.0, Math.Min(1.0, factorX)); // Зажимаем в границы [0..1]
        }

        // Считаем дробную долю по оси Y (Нагрузка)
        if (bRow >= 0 && bRow < Rows - 1 && axY != null && axY[bRow + 1] > axY[bRow])
        {
            double factorY = (inputY - axY[bRow]) / (axY[bRow + 1] - axY[bRow]);
            exactRow += Math.Max(0.0, Math.Min(1.0, factorY)); // Зажимаем в границы [0..1]
        }

        // 🔥 Пинаем наш 3D-движок в VariableViewModel.3d.cs для отрисовки лазера!
        UpdateLaserBeamPosition(exactCol, exactRow);
    }


}