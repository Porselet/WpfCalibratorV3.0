using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Collections.Generic;


// ... внутри partial класса VariableViewModel ...

// Константы шага и масштаба сетки на экране для жесткой синхронизации всех подметодов
namespace WpfCalibrator.ViewModels
{
    public partial class VariableViewModel
    {

        private System.Windows.Media.Media3D.Point3DCollection _surfaceLines = new System.Windows.Media.Media3D.Point3DCollection();
        public System.Windows.Media.Media3D.Point3DCollection SurfaceLines
        {
            get => _surfaceLines;
            set
            {
                _surfaceLines = value;
                OnPropertyChanged(nameof(SurfaceLines));
            }
        }


        private MeshGeometry3D _surfaceMesh = new MeshGeometry3D();
        public MeshGeometry3D SurfaceMesh
        {
            get => _surfaceMesh;
            set
            {
                _surfaceMesh = value;
                OnPropertyChanged(nameof(SurfaceMesh));
            }
        }
        // Хранители стабильного масштаба 3D-сцены, защищающие от "эффекта желе"
        public double? FixedScaleZ { get; set; } = null;
        public double? FixedMinVal { get; set; } = null;
        public double? FixedMaxVal { get; set; } = null;

        // Метод для принудительного сброса масштаба (вызовем при смене Layout или первом UART пакете)
        public void Reset3DScale()
        {
            FixedScaleZ = null;
            FixedMinVal = null;
            FixedMaxVal = null;
        }

        private const double StepX = 15.0;
        private const double StepY = 15.0;
        private const double MaxHeightZ = 30.0;


        // Коллекция линий измерительного куба-обрешетки
        private Point3DCollection _boundingBoxLines = new Point3DCollection();
        public Point3DCollection BoundingBoxLines
        {
            get => _boundingBoxLines;
            set { _boundingBoxLines = value; OnPropertyChanged(nameof(BoundingBoxLines)); }
        }

        // Новые свойства для хранения 3D-подписей шкал (будут биндиться в XAML)
        public class AxisTextLabel
        {
            public Point3D Position { get; set; }
            public string Text { get; set; }
        }



        // ... внутри класса VariableViewModel ...


        // ... внутри класса VariableViewModel ...

        // Изменили тип с ModelVisual3D на Model3DGroup
        private Model3DGroup _axisLabelsContainer = new Model3DGroup();
        public Model3DGroup AxisLabelsContainer
        {
            get => _axisLabelsContainer;
            set { _axisLabelsContainer = value; OnPropertyChanged(nameof(AxisLabelsContainer)); }
        }




        /// <summary>
        /// Главный диспетчер пересчета 3D-сцены калибровочной карты
        /// </summary>
        public void Rebuild3DSurfaceMesh()
        {
            if (Rows <= 1 || Cols <= 1 || MatrixData == null) return;

            double minVal, maxVal, delta, scaleZ;

            // 🔥 ПРОВЕРКА ЗАЩИТЫ: Если масштаб еще НЕ зафиксирован в этой сессии экрана — считаем его один раз
            if (FixedScaleZ == null || FixedMinVal == null || FixedMaxVal == null)
            {
                FindMatrixExtremes(out minVal, out maxVal, out delta);
                scaleZ = (delta > 0.001) ? (MaxHeightZ / delta) : 1.0;

                // Замораживаем масштаб в памяти вьюмодели
                FixedMinVal = minVal;
                FixedMaxVal = maxVal;
                FixedScaleZ = scaleZ;
            }
            else
            {
                // Прилетел UART-пакет подтверждения записи? Игнорируем пересчет масштаба!
                // Берем жестко зафиксированные константы. Карта больше не "дышит".
                minVal = FixedMinVal.Value;
                maxVal = FixedMaxVal.Value;
                scaleZ = FixedScaleZ.Value;
                delta = maxVal - minVal;
            }

            // --- Оставшаяся часть метода (BuildSurfaceMesh, BuildSurfaceEdges, BuildBoundingBox) без изменений ---
            double halfWidth = ((Cols - 1) * StepX) / 2.0;
            double halfLength = ((Rows - 1) * StepY) / 2.0;

            var mesh = BuildSurfaceMesh(minVal, delta, scaleZ, halfWidth, halfLength, out Point3DCollection positions);
            var surfaceEdges = BuildSurfaceEdges(positions, minVal, delta);
            var boundingBox = BuildBoundingBox(halfWidth, halfLength);

            BuildAxisLabels(minVal, maxVal, delta, halfWidth, halfLength);

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                SurfaceMesh = mesh;
                SurfaceLines = surfaceEdges;
                BoundingBoxLines = boundingBox;

                OnPropertyChanged(nameof(SurfaceMesh));
                OnPropertyChanged(nameof(SurfaceLines));
                OnPropertyChanged(nameof(BoundingBoxLines));
                OnPropertyChanged(nameof(AxisLabelsContainer));
            });
        }

        /// <summary>
        /// Шаг 1: Поиск минимального, максимального значений и дельты диапазона
        /// </summary>
        private void FindMatrixExtremes(out double minVal, out double maxVal, out double delta)
        {
            minVal = double.MaxValue;
            maxVal = double.MinValue;

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    double v = MatrixData[r, c];
                    if (v < minVal) minVal = v;
                    if (v > maxVal) maxVal = v;
                }
            }
            delta = maxVal - minVal;
        }

        /// <summary>
        /// Шаг 2: Сборка твердотельного полигонального рельефа и расчет тепловой карты текстур
        /// </summary>
        private MeshGeometry3D BuildSurfaceMesh(double minVal, double delta, double scaleZ, double halfWidth, double halfLength, out Point3DCollection positions)
        {


            var mesh = new MeshGeometry3D();
            positions = new Point3DCollection();
            var indices = new Int32Collection();
            var texCoords = new PointCollection();

            // 1. Генерация вершин (Строго Rows x Cols)
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    double val = MatrixData[r, c];
                    double x = (c * StepX) - halfWidth;

                    // 🔥 Твоя новая инвертированная формула Y (Синхронизация 3D с таблицей)
                    double y = ((Rows - 1 - r) * StepY) - halfLength;
                    double z = (val - minVal) * scaleZ;

                    positions.Add(new Point3D(x, y, z));

                    double normZ = (delta > 0.001) ? ((val - minVal) / delta) : 0.5;
                    texCoords.Add(new Point(0.5, 1.0 - normZ));
                }
            }
            mesh.Positions = positions;
            mesh.TextureCoordinates = texCoords;

            // 2. Сборка треугольников (Исправленный обход: нормали смотрят строго вверх)
            for (int r = 0; r < Rows - 1; r++)
            {
                for (int c = 0; c < Cols - 1; c++)
                {
                    int topLeft = r * Cols + c;
                    int topRight = r * Cols + (c + 1);
                    int bottomLeft = (r + 1) * Cols + c;
                    int bottomRight = (r + 1) * Cols + (c + 1);

                    // Первый треугольник (TL -> TR -> BL)
                    indices.Add(topLeft);
                    indices.Add(topRight);
                    indices.Add(bottomLeft);

                    // Второй треугольник (TR -> BR -> BL)
                    indices.Add(topRight);
                    indices.Add(bottomRight);
                    indices.Add(bottomLeft);
                }
            }
            mesh.TriangleIndices = indices;

            // Расчет базовых нормалей для буфера глубины
            var normals = new Vector3DCollection();
            for (int i = 0; i < positions.Count; i++) normals.Add(new Vector3D(0, 0, 1));
            mesh.Normals = normals;

            mesh.Freeze();
            return mesh;
        }

        /// <summary>
        /// Шаг 3: Нарезка четырехугольных ребер (БЕЗ ДИАГОНАЛЕЙ, С ЗАЩИТОЙ ОТ ВЫЛЕТА ИНДЕКСА)
        /// </summary>
        private Point3DCollection BuildSurfaceEdges(Point3DCollection positions, double minVal, double delta)
        {
            var lines = new Point3DCollection();

            // Предохранитель: если вершины не сгенерировались, возвращаем пустую коллекцию
            if (positions == null || positions.Count != Rows * Cols) return lines;

            // Горизонтальные линии ячеек (Идем строго в границах сгенерированного массива positions)
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols - 1; c++)
                {
                    int idx1 = r * Cols + c;
                    int idx2 = r * Cols + (c + 1);

                    if (idx1 < positions.Count && idx2 < positions.Count)
                    {
                        lines.Add(positions[idx1]);
                        lines.Add(positions[idx2]);
                    }
                }
            }

            // Вертикальные линии ячеек
            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows - 1; r++)
                {
                    int idx1 = r * Cols + c;
                    int idx2 = (r + 1) * Cols + c;

                    if (idx1 < positions.Count && idx2 < positions.Count)
                    {
                        lines.Add(positions[idx1]);
                        lines.Add(positions[idx2]);
                    }
                }
            }

            lines.Freeze();
            return lines;
        }

        /// <summary>
        /// Шаг 4: Динамическая сборка коробки-обрешетки под размер Rows и Cols (ChipTuningPRO Style)
        /// </summary>
        private Point3DCollection BuildBoundingBox(double halfWidth, double halfLength)
        {
            var boxLines = new Point3DCollection();

            // 1. СЕТКА ПОЛА (XY) — строго под рядами и колонками калибровки
            for (int c = 0; c < Cols; c++)
            {
                double x = (c * StepX) - halfWidth;
                boxLines.Add(new Point3D(x, -halfLength, 0));
                boxLines.Add(new Point3D(x, halfLength, 0));
            }
            for (int r = 0; r < Rows; r++)
            {
                double y = (r * StepY) - halfLength;
                boxLines.Add(new Point3D(-halfWidth, y, 0));
                boxLines.Add(new Point3D(halfWidth, y, 0));
            }

            // 2. ВЕРТИКАЛЬНЫЕ СТЕНКИ (ЗАДНЯЯ Y=halfLength И БОКОВАЯ X=-halfWidth)
            // Линии, уходящие вверх от каждой точки контура пола до MaxHeightZ
            for (int c = 0; c < Cols; c++)
            {
                double x = (c * StepX) - halfWidth;
                boxLines.Add(new Point3D(x, halfLength, 0));
                boxLines.Add(new Point3D(x, halfLength, MaxHeightZ));
            }
            for (int r = 0; r < Rows; r++)
            {
                double y = (r * StepY) - halfLength;
                boxLines.Add(new Point3D(-halfWidth, y, 0));
                boxLines.Add(new Point3D(-halfWidth, y, MaxHeightZ));
            }

            // Горизонтальные уровни на задних стенках (нарезаем фиксированные 5 уровней высоты)
            int heightLevels = 5;
            for (int i = 0; i <= heightLevels; i++)
            {
                double z = (MaxHeightZ / heightLevels) * i;

                // Линия по задней стене XZ
                boxLines.Add(new Point3D(-halfWidth, halfLength, z));
                boxLines.Add(new Point3D(halfWidth, halfLength, z));

                // Линия по боковой стене YZ
                boxLines.Add(new Point3D(-halfWidth, -halfLength, z));
                boxLines.Add(new Point3D(-halfWidth, halfLength, z));
            }

            boxLines.Freeze();
            return boxLines;
        }

        /// <summary>
        /// Шаг 5: Калькулятор 3D-координат для подписей шкал и осей
        /// </summary>
        /// <summary>
        /// Шаг 5: Калькулятор 3D-координат для подписей шкал и осей (ChipTuningPRO Style)
        /// </summary>
        /// <summary>
        /// Шаг 5: Калькулятор и генератор 3D-надписей шкал и осей (Чистый нативный Helix способ)
        /// </summary>
        /// <summary>
        /// Шаг 5: Калькулятор и генератор 3D-надписей шкал и осей (Стабильный легковесный способ)
        /// </summary>
        /// <summary>
        /// Шаг 5: Калькулятор и генератор 3D-надписей шкал и осей (Стабильный легковесный способ)
        /// </summary>
        /// <summary>
        /// Шаг 5: Калькулятор и генератор 3D-надписей шкал и осей (Безопасный к размерам массивов способ)
        /// </summary>
        private void BuildAxisLabels(double minVal, double maxVal, double delta, double halfWidth, double halfLength)
        {
            var group = new Model3DGroup();

            // 1. ОЦИФРОВКА ОСИ X (Колонки - например, Обороты / RPM)
            for (int c = 0; c < Cols; c++)
            {
                double x = (c * StepX) - halfWidth;
                string txt = (c + 1).ToString(); // Дефолтное значение

                // Безопасное извлечение данных из оси X
                if (BoundAxisX != null && BoundAxisX.MatrixData != null)
                {
                    int axisRows = BoundAxisX.MatrixData.GetLength(0);
                    int axisCols = BoundAxisX.MatrixData.GetLength(1);

                    // Если ось лежит как строка [1, N]
                    if (axisRows == 1 && c < axisCols)
                        txt = BoundAxisX.MatrixData[0, c].ToString("F0");
                    // Если ось лежит как столбец [N, 1]
                    else if (axisCols == 1 && c < axisRows)
                        txt = BoundAxisX.MatrixData[c, 0].ToString("F0");
                    // На крайний случай плоского индекса
                    else if (c < BoundAxisX.MatrixData.Length)
                        txt = BoundAxisX.MatrixData[c % axisRows, c / axisRows].ToString("F0");
                }

                var textModel = HelixToolkit.Wpf.TextCreator.CreateTextLabelModel3D(
                    txt, Brushes.DarkGray, false, 10,
                    new Point3D(x, -halfLength - 6.0, -1.5), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)
                );
                if (textModel != null) group.Children.Add(textModel);
            }

            // НАЗВАНИЕ ОСИ X
            string labelX = !string.IsNullOrEmpty(BoundAxisX?.Name) ? BoundAxisX.Name : "Ось X";
            var labelXModel = HelixToolkit.Wpf.TextCreator.CreateTextLabelModel3D(
                labelX.ToUpper(), new SolidColorBrush(Color.FromRgb(255, 255, 0)), false, 12,
                new Point3D(0, -halfLength - 14.0, -3.0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)
            );
            if (labelXModel != null) group.Children.Add(labelXModel);


            // 2. ОЦИФРОВКА ОСИ Y (Строки - например, Дроссель % / Нагрузка)
            for (int r = 0; r < Rows; r++)
            {
                double y = (r * StepY) - halfLength;
                string txt = (r + 1).ToString(); // Дефолтное значение

                // Безопасное извлечение данных из оси Y
                if (BoundAxisY != null && BoundAxisY.MatrixData != null)
                {
                    int axisRows = BoundAxisY.MatrixData.GetLength(0);
                    int axisCols = BoundAxisY.MatrixData.GetLength(1);

                    // Если ось лежит как столбец [N, 1]
                    if (axisCols == 1 && r < axisRows)
                        txt = BoundAxisY.MatrixData[r, 0].ToString("F1");
                    // Если ось лежит как строка [1, N]
                    else if (axisRows == 1 && r < axisCols)
                        txt = BoundAxisY.MatrixData[0, r].ToString("F1");
                    // На крайний случай
                    else if (r < BoundAxisY.MatrixData.Length)
                        txt = BoundAxisY.MatrixData[r % axisRows, r / axisRows].ToString("F1");
                }

                var textModelY = HelixToolkit.Wpf.TextCreator.CreateTextLabelModel3D(
                    txt, Brushes.DarkGray, false, 10,
                    new Point3D(-halfWidth - 9.0, y, -1.5), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0)
                );
                if (textModelY != null) group.Children.Add(textModelY);
            }

            // НАЗВАНИЕ ОСИ Y
            string labelY = !string.IsNullOrEmpty(BoundAxisY?.Name) ? BoundAxisY.Name : "Ось Y";
            var labelYModel = HelixToolkit.Wpf.TextCreator.CreateTextLabelModel3D(
                labelY.ToUpper(), new SolidColorBrush(Color.FromRgb(255, 255, 0)), false, 12,
                new Point3D(-halfWidth - 19.0, 0, -3.0), new Vector3D(0, 1, 0), new Vector3D(-1, 0, 0)
            );
            if (labelYModel != null) group.Children.Add(labelYModel);


            // 3. ОЦИФРОВКА ОСИ Z (Высота калибровок) - тут зависимости от осей нет, код безопасен
            int heightLevels = 5;
            for (int i = 0; i <= heightLevels; i++)
            {
                double z = (MaxHeightZ / heightLevels) * i;
                double realVal = minVal + (delta / heightLevels) * i;

                var textModelZ = HelixToolkit.Wpf.TextCreator.CreateTextLabelModel3D(
                    realVal.ToString("F1"), Brushes.LightGray, false, 10,
                    new Point3D(-halfWidth - 6.0, -halfLength - 6.0, z), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1)
                );
                if (textModelZ != null) group.Children.Add(textModelZ);
            }

            // НАЗВАНИЕ КАРТЫ НА ВЕРШИНЕ ОСИ Z
            var labelZModel = HelixToolkit.Wpf.TextCreator.CreateTextLabelModel3D(
                Name.ToUpper(), new SolidColorBrush(Color.FromRgb(0, 255, 0)), false, 13,
                new Point3D(-halfWidth - 8.0, -halfLength - 8.0, MaxHeightZ + 4.0), new Vector3D(1, 0, 0), new Vector3D(0, 0, 1)
            );
            if (labelZModel != null) group.Children.Add(labelZModel);

            //sgroup.Freeze();
            AxisLabelsContainer = group;
        }
    }
}