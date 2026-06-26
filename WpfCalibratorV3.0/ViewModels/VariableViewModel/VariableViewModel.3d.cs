using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;


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
        private MeshGeometry3D _laserBeamMesh = new MeshGeometry3D();
        /// <summary>
        /// Единая 3D-геометрия объемного лазерного цилиндра Маклауда
        /// </summary>
        public MeshGeometry3D LaserBeamMesh
        {
            get => _laserBeamMesh;
            set { _laserBeamMesh = value; OnPropertyChanged(nameof(LaserBeamMesh)); }
        }

        private Point3DCollection _laserBeamPoints = new Point3DCollection();
        /// <summary>
        /// Две 3D-точки вертикального лазерного луча: [0] - Старт на полу (Z=0), [1] - Финиш в небе (Z=60)
        /// </summary>
        public Point3DCollection LaserBeamPoints
        {
            get => _laserBeamPoints;
            set
            {
                _laserBeamPoints = value;
                OnPropertyChanged(nameof(LaserBeamPoints));
            }
        }


        // ... внутри класса VariableViewModel ...

        public class ScreenTextLabel
        {
            public double ScreenX { get; set; }
            public double ScreenY { get; set; }
            public string Text { get; set; }
        }



        // ... внутри класса VariableViewModel ...

        private MeshGeometry3D _allSpheresMesh = new MeshGeometry3D();
        /// <summary>
        /// Единая 3D-геометрия всех фоновых шариков кристаллической решетки
        /// </summary>
        public MeshGeometry3D AllSpheresMesh
        {
            get => _allSpheresMesh;
            set { _allSpheresMesh = value; OnPropertyChanged(nameof(AllSpheresMesh)); }
        }

        private MeshGeometry3D _selectedSpheresMesh = new MeshGeometry3D();
        /// <summary>
        /// Единая 3D-геометрия ярких синих шаров выделенного курсора
        /// </summary>
        public MeshGeometry3D SelectedSpheresMesh
        {
            get => _selectedSpheresMesh;
            set { _selectedSpheresMesh = value; OnPropertyChanged(nameof(SelectedSpheresMesh)); }
        }




        // ... внутри класса VariableViewModel ...

        private List<BillboardTextItem> _numericLabels = new List<BillboardTextItem>();
        public List<BillboardTextItem> NumericLabels
        {
            get => _numericLabels;
            set { _numericLabels = value; OnPropertyChanged(nameof(NumericLabels)); }
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
                UpdateCursorVerticesHighlight(positions);


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

            // Генерация вершин и маппинг градиента
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    double val = MatrixData[r, c];
                    double x = (c * StepX) - halfWidth;
                    //double y = (r * StepY) - halfLength;

                    // Строка r=0 таблицы получит максимальный Y (улетит на дальний край сцены, наверх)
                    double y = ((Rows - 1 - r) * StepY) - halfLength;
                    double z = (val - minVal) * scaleZ;

                    positions.Add(new Point3D(x, y, z));

                    double normZ = (delta > 0.001) ? ((val - minVal) / delta) : 0.5;
                    texCoords.Add(new Point(0.5, 1.0 - normZ));
                }
            }
            mesh.Positions = positions;
            mesh.TextureCoordinates = texCoords;

            // Сборка треугольников с правильным направлением нормалей (лицом вверх)
            for (int r = 0; r < Rows - 1; r++)
            {
                for (int c = 0; c < Cols - 1; c++)
                {
                    int topLeft = r * Cols + c;
                    int topRight = r * Cols + (c + 1);
                    int bottomLeft = (r + 1) * Cols + c;
                    int bottomRight = (r + 1) * Cols + (c + 1);

                    indices.Add(topLeft); indices.Add(topRight); indices.Add(bottomLeft);
                    indices.Add(topRight); indices.Add(bottomRight); indices.Add(bottomLeft);
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
        /// Шаг 3: Нарезка четырехугольных ребер (БЕЗ ДИАГОНАЛЕЙ)
        /// </summary>
        private Point3DCollection BuildSurfaceEdges(Point3DCollection positions, double minVal, double delta)
        {
            var lines = new Point3DCollection();

            // Горизонтальные линии ячеек
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols - 1; c++)
                {
                    lines.Add(positions[r * Cols + c]);
                    lines.Add(positions[r * Cols + (c + 1)]);
                }
            }

            // Вертикальные линии ячеек
            for (int c = 0; c < Cols; c++)
            {
                for (int r = 0; r < Rows - 1; r++)
                {
                    lines.Add(positions[r * Cols + c]);
                    lines.Add(positions[(r + 1) * Cols + c]);
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
            // Создаем чистый список для визуальных 3D объектов
            var list = new List<Visual3D>();

            // 1. ОЦИФРОВКА ОСИ X (Шаги по колонкам - например, Обороты RPM)
            for (int c = 0; c < Cols; c++)
            {
                double x = (c * StepX) - halfWidth;
                string txt = (c + 1).ToString(); // Дефолт

                if (BoundAxisX != null && BoundAxisX.MatrixData != null)
                {
                    int axisRows = BoundAxisX.MatrixData.GetLength(0);
                    int axisCols = BoundAxisX.MatrixData.GetLength(1);

                    if (axisRows == 1 && c < axisCols)
                        txt = BoundAxisX.MatrixData[0, c].ToString("F0");
                    else if (axisCols == 1 && c < axisRows)
                        txt = BoundAxisX.MatrixData[c, 0].ToString("F0");
                }

                // Создаем легкий текстовый билборд
                var billboard = new HelixToolkit.Wpf.BillboardTextVisual3D
                {
                    Position = new Point3D(x, -halfLength - 6.0, -1.0), // Перед кубом на полу
                    Text = txt,
                    Foreground = System.Windows.Media.Brushes.DarkGray,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10
                };
                list.Add(billboard);
            }

            // 2. ОЦИФРОВКА ОСИ Y (Шаги по строкам - например, Дроссель % / Давление)
            for (int r = 0; r < Rows; r++)
            {
                // Твоя утренняя инвертированная формула Y, чтобы цифры совпали с рельефом!
                double y = ((Rows - 1 - r) * StepY) - halfLength;
                string txt = (r + 1).ToString();

                if (BoundAxisY != null && BoundAxisY.MatrixData != null)
                {
                    int axisRows = BoundAxisY.MatrixData.GetLength(0);
                    int axisCols = BoundAxisY.MatrixData.GetLength(1);

                    if (axisCols == 1 && r < axisRows)
                        txt = BoundAxisY.MatrixData[r, 0].ToString("F1");
                    else if (axisRows == 1 && r < axisCols)
                        txt = BoundAxisY.MatrixData[0, r].ToString("F1");
                }

                var billboard = new HelixToolkit.Wpf.BillboardTextVisual3D
                {
                    Position = new Point3D(-halfWidth - 8.0, y, -1.0), // Слева от куба
                    Text = txt,
                    Foreground = System.Windows.Media.Brushes.DarkGray,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 10
                };
                list.Add(billboard);
            }

            // Пушим готовый список объектов в свойство
            //Numeric3DLabels = list;
        }

        /// <summary>
        /// Обновляет 3D-координаты сквозного лазерного луча на основе плавных (дробных) координат радара
        /// </summary>
        /// <summary>
        /// Обновляет 3D-геометрию объемного лазерного цилиндра на основе плавных координат радара
        /// </summary>
        public void UpdateLaserBeamPosition(double exactColIndex, double exactRowIndex)
        {
            double halfWidth = ((Cols - 1) * StepX) / 2.0;
            double halfLength = ((Rows - 1) * StepY) / 2.0;

            // 1. Находим точные плавно скользящие координаты центра луча
            double laserX = (exactColIndex * StepX) - halfWidth;
            double laserY = ((Rows - 1 - exactRowIndex) * StepY) - halfLength;

            // 2. Строим объемную трубу (Цилиндр)
            var mesh = new MeshGeometry3D();

            // Параметры цилиндра: радиус трубы (0.6) и количество граней круга (12 для идеальной окружности)
            double radius = 0.6;
            int segments = 12;
            double heightStart = 0.0;  // Пол куба
            double heightEnd = 60.0;  // Небо над картой

            // Генерируем вершины нижнего и верхнего оснований + нормали
            for (int i = 0; i <= segments; i++)
            {
                double theta = 2.0 * Math.PI * i / segments;
                double cos = Math.Cos(theta);
                double sin = Math.Sin(theta);

                // Вектор нормали смотрит строго вбок от центральной оси цилиндра
                var normal = new Vector3D(cos, sin, 0);

                // Нижняя точка окружности
                mesh.Positions.Add(new Point3D(laserX + radius * cos, laserY + radius * sin, heightStart));
                mesh.Normals.Add(normal);

                // Верхняя точка окружности
                mesh.Positions.Add(new Point3D(laserX + radius * cos, laserY + radius * sin, heightEnd));
                mesh.Normals.Add(normal);
            }

            // Собираем боковые стенки цилиндра из треугольников
            for (int i = 0; i < segments; i++)
            {
                int bL = i * 2;       // Bottom Left
                int tL = bL + 1;      // Top Left
                int bR = bL + 2;      // Bottom Right
                int tR = bL + 3;      // Top Right

                // Первый треугольник грани (TL -> BL -> TR)
                mesh.TriangleIndices.Add(tL);
                mesh.TriangleIndices.Add(bL);
                mesh.TriangleIndices.Add(tR);

                // Второй треугольник грани (TR -> BL -> BR)
                mesh.TriangleIndices.Add(tR);
                mesh.TriangleIndices.Add(bL);
                mesh.TriangleIndices.Add(bR);
            }

            mesh.Freeze();

            // 3. Безопасно пушаем готовый меш лазера в UI-поток
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                LaserBeamMesh = mesh;
            });
        }



        /// <summary>
        /// Обновляет кристаллическую решетку и подсвечивает синим цветом вершины под курсором таблицы
        /// </summary>
        public void UpdateCursorVerticesHighlight(Point3DCollection sourcePositions)
        {
            if (sourcePositions == null || sourcePositions.Count == 0) return;

            var allMesh = new MeshGeometry3D();
            var selectedMesh = new MeshGeometry3D();

            // 1. ГЕНЕРИРУЕМ ФОНОВУЮ РЕШЕТКУ ШАРОВ (Радиус 0.4 единицы)
            foreach (var pt in sourcePositions)
            {
                AddSphereToMesh(allMesh, pt, 0.4);
            }
            allMesh.Freeze();

            // 2. ГЕНЕРИРУЕМ ЖИРНЫЕ ШАРЫ ДЛЯ КУРСОРНОГО ВЫДЕЛЕНИЯ (Радиус 1.0)
            int minRow = Math.Max(0, Math.Min(AnchorRow, SelectedRow));
            int maxRow = Math.Min(Rows - 1, Math.Max(AnchorRow, SelectedRow));
            int minCol = Math.Max(0, Math.Min(AnchorCol, SelectedCol));
            int maxCol = Math.Min(Cols - 1, Math.Max(AnchorCol, SelectedCol));

            bool hasSelection = false;
            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    int targetIndex = r * Cols + c;
                    if (targetIndex >= 0 && targetIndex < sourcePositions.Count)
                    {
                        AddSphereToMesh(selectedMesh, sourcePositions[targetIndex], 1.0);
                        hasSelection = true;
                    }
                }
            }

            if (hasSelection) selectedMesh.Freeze();
            else selectedMesh = new MeshGeometry3D();

            // 3. Безопасно пушим готовые меши геометрии в UI-поток
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                AllSpheresMesh = allMesh;
                SelectedSpheresMesh = selectedMesh;
            });
        }

        /// <summary>
        /// Нативный математический генератор 3D-сферы (WPF 3D Core)
        /// </summary>
        private void AddSphereToMesh(MeshGeometry3D mesh, Point3D center, double radius)
        {
            int slices = 10;
            int stacks = 10;
            int baseIndex = mesh.Positions.Count;

            // Генерируем вершины И нормали сферы (синусы и косинусы)
            for (int stack = 0; stack <= stacks; stack++)
            {
                double phi = Math.PI * stack / stacks;
                double y = radius * Math.Cos(phi);
                double rStrata = radius * Math.Sin(phi);

                for (int slice = 0; slice <= slices; slice++)
                {
                    double theta = 2.0 * Math.PI * slice / slices;
                    double x = rStrata * Math.Cos(theta);
                    double z = rStrata * Math.Sin(theta);

                    // 1. Физическая точка на экране
                    mesh.Positions.Add(new Point3D(center.X + x, center.Y + y, center.Z + z));

                    // 2. 🔥 ХАК НЕПРОЗРАЧНОСТИ: Рассчитываем вектор нормали (направление взгляда из центра сферы наружу)
                    // Это заставит видеокарту включить жесткую Z-отсечку буфера глубины
                    double normalX = x / radius;
                    double normalY = y / radius;
                    double normalZ = z / radius;
                    mesh.Normals.Add(new Vector3D(normalX, normalY, normalZ));
                }
            }

            // Собираем треугольники сферы (Исправленный обход: нормали смотрят СТРОГО наружу!)
            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int nextStack = stack + 1;
                    int nextSlice = slice + 1;
                    int stride = slices + 1;

                    int tL = baseIndex + stack * stride + slice;
                    int tR = baseIndex + stack * stride + nextSlice;
                    int bL = baseIndex + nextStack * stride + slice;
                    int bR = baseIndex + nextStack * stride + nextSlice;

                    // Первый треугольник ячейки сферы (TL -> TR -> BL)
                    mesh.TriangleIndices.Add(tL);
                    mesh.TriangleIndices.Add(tR);
                    mesh.TriangleIndices.Add(bL);

                    // Второй треугольник ячейки сферы (TR -> BR -> BL)
                    mesh.TriangleIndices.Add(tR);
                    mesh.TriangleIndices.Add(bR);
                    mesh.TriangleIndices.Add(bL);
                }
            }
        }


    }
}