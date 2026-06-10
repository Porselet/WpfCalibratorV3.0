using System;

namespace WpfCalibrator.ViewModels;

public partial class VariableViewModel
{

    private float _currentValue = 0.0f;

    /// <summary>
    /// Текущее значение переменной (для сигналов) или активное значение в ячейке (для таблиц).
    /// </summary>
    public float CurrentValue
    {
        get => _currentValue;
        set
        {
            if (Math.Abs(_currentValue - value) > 0.0001f)
            {
                _currentValue = value;
                OnPropertyChanged();
            }
        }
    }
    // Сериализация данных в байтовый массив (для отправки на устройство)
    public byte[] SerializeToBytesColumnMajor()
    {
        if (TotalElements == 1)
        {
            // Для скаляров
            return BitConverter.GetBytes(CurrentValue);
        }
        else
        {
            // Для матриц: упаковка в Column-Major (как в MATLAB)
            var rawBytes = new List<byte>();
            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    float val = MatrixData[r, c];
                    rawBytes.AddRange(BitConverter.GetBytes(val));
                }
            }
            return rawBytes.ToArray();
        }
    }

    // Десериализация байтового массива в значение (для приема с устройства)
    public void DeserializeFromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length < TotalBytes)
            return;

        if (TotalElements == 1)
        {
            // Для скаляров
            CurrentValue = BitConverter.ToSingle(bytes, 0);
        }
        else
        {
            // Для матриц: раскладываем байты в MatrixData (Column-Major)
            int index = 0;
            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows; r++)
                {
                    MatrixData[r, c] = BitConverter.ToSingle(bytes, index * ElementSize);
                    index++;
                }
            }
        }
    }
}