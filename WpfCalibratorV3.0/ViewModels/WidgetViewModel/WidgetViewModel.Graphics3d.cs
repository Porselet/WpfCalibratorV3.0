using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.ViewModels;

/// <summary>
/// Обертка для виджета на приборной панели.
/// </summary>
public partial class WidgetViewModel : INotifyPropertyChanged
{


    // ======================================================================
    // 🌐 3D-ГЕОМЕТРИЯ HELIX TOOLKIT ДЛЯ ВИЗУАЛИЗАЦИИ КАРТ (UI СЛОЙ)
    // ======================================================================

    private System.Windows.Media.Media3D.MeshGeometry3D _surfaceMesh = new();
    public System.Windows.Media.Media3D.MeshGeometry3D SurfaceMesh
    {
        get => _surfaceMesh;
        set { _surfaceMesh = value; OnPropertyChanged(); }
    }

    private System.Windows.Media.Media3D.Point3DCollection _surfaceLines = new();
    public System.Windows.Media.Media3D.Point3DCollection SurfaceLines
    {
        get => _surfaceLines;
        set { _surfaceLines = value; OnPropertyChanged(); }
    }
    private Point3DCollection _boundingBoxLines;
    /// <summary> Линии измерительного куба-обрешетки шкал. </summary>
    public Point3DCollection BoundingBoxLines
    {
        get => _boundingBoxLines;
        set { _boundingBoxLines = value; OnPropertyChanged(); } // 🔥 ПИНОК ДЛЯ XAML!
    }
    private System.Windows.Media.Media3D.MeshGeometry3D _laserBeamMesh = new();
    public System.Windows.Media.Media3D.MeshGeometry3D LaserBeamMesh
    {
        get => _laserBeamMesh;
        set { _laserBeamMesh = value; OnPropertyChanged(); }
    }
    // Геометрия фоновых шариков решетки, синих шаров курсора и куба-обрешетки шкал
    public System.Windows.Media.Media3D.MeshGeometry3D AllSpheresMesh { get; set; } = new();
    public System.Windows.Media.Media3D.MeshGeometry3D SelectedSpheresMesh { get; set; } = new();

    private Model3DGroup _axisLabelsContainer = new();
    /// <summary>
    /// Контейнер для 3D-надписей осей шкал (будет биндиться в XAML) [1.14]
    /// </summary>
    public Model3DGroup AxisLabelsContainer
    {
        get => _axisLabelsContainer;
        set { _axisLabelsContainer = value; OnPropertyChanged(); }
    }

    // Хранители масштаба 3D-сцены от "эффекта желе"
    public double? FixedScaleZ { get; set; } = null;
    public double? FixedMinVal { get; set; } = null;
    public double? FixedMaxVal { get; set; } = null;

    /// <summary>
    /// Принудительный сброс масштаба при смене макетов
    /// </summary>
    public void Reset3DScale()
    {
        FixedScaleZ = null;
        FixedMinVal = null;
        FixedMaxVal = null;
    }

    private const double StepX = 15.0;
    private const double StepY = 15.0;
    private const double MaxHeightZ = 30.0;

    /// <summary>
    /// Главный диспетчер пересчета 3D-сцены
    /// </summary>
    public void Rebuild3DSurfaceMesh()
    {
        // ПОЛУЧЕНИЕ ДАННЫХ [1.14]
        if (DataSource is not Map3DVariableViewModel map3D) return;
        if (map3D.Rows <= 1 || map3D.Cols <= 1 || map3D.MatrixData == null) return;

        double minVal;
        double maxVal;
        double delta;

        // 🚀 УМНАЯ ФИКСАЦИЯ МАСШТАБА:
        // Мы пересчитываем масштаб, если он ЕЩЕ НЕ зафиксирован, 
        // ЛИБО если прошлый расчет зафиксировался на пустых нулях (delta была равна 0)
        if (FixedScaleZ == null || FixedMinVal == null || FixedMaxVal == null || Math.Abs(FixedMaxVal.Value - FixedMinVal.Value) < 0.001)
        {
            // Сканируем живую матрицу углов зажигания в ОЗУ [1.14]
            this.FindMatrixExtremes(map3D, out minVal, out maxVal, out delta);

            // Замораживаем масштаб ТОЛЬКО если прошивка реально прислала боевые числа (delta > 0)
            if (delta > 0.001)
            {
                FixedMinVal = minVal;
                FixedMaxVal = maxVal;
                FixedScaleZ = MaxHeightZ / delta; // Вычисляем постоянный коэффициент высоты Z
            }
            else
            {
                // Если сеть еще спит и в массиве нули — подставляем временные дефолты, НЕ замораживая шкалу намертво
                minVal = 0.0;
                maxVal = 10.0;
                delta = 10.0;
            }
        }
        else
        {
            // Если масштаб уже был успешно заморожен на живых данных — держим его намертво!
            minVal = FixedMinVal.Value;
            maxVal = FixedMaxVal.Value;
            delta = maxVal - minVal;
        }

        // Твой дальнейший честный код генерации геометрии, куба и вызова Dispatcher... [1.14]
        double scaleZ = FixedScaleZ ?? 1.0;

        // Габариты измерительного куба
        double halfWidth = ((map3D.Cols - 1) * StepX) / 2.0;
        double halfLength = ((map3D.Rows - 1) * StepY) / 2.0;
        // 🚀 СБОРКА ГЕОМЕТРИИ (Вызываем подфункции генерации Helix)


        var mesh = BuildSurfaceMesh(map3D, minVal, delta, scaleZ, halfWidth, halfLength, out var positions);
        var surfaceEdges = BuildSurfaceEdges(map3D, positions, minVal, delta);
        var boundingBox = BuildBoundingBox(map3D, halfWidth, halfLength);

        // Оцифровка шкал осей
        //BuildAxisLabels(map3D, minVal, FixedMaxVal.Value, delta, halfWidth, halfLength);

        // Атомарный заброс мешей в графический конвейер WPF
        // Атомарный заброс мешей в графический конвейер WPF
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            SurfaceMesh = mesh;
            SurfaceLines = surfaceEdges;
            BoundingBoxLines = boundingBox;

            // 🔥 ФИКС: Передаем ТОЛЬКО коллекцию positions!
            // Никаких map3D в скобках быть не должно!
            UpdateCursorVerticesHighlight(positions);

            // Включаем лазерный трекер над точкой калибровки
            UpdateLaserBeamPosition(map3D.ActiveColIndex, map3D.ActiveRowIndex);
        });

    } // Конец метода Rebuild3DSurfaceMesh






    /// <summary>
    /// Шаг 2: Сборка твердотельного полигонального рельефа и расчет тепловой карты текстур [1.14]
    /// </summary>
    private System.Windows.Media.Media3D.MeshGeometry3D BuildSurfaceMesh(Map3DVariableViewModel map3D, double minVal, double delta, double scaleZ, double halfWidth, double halfLength, out System.Windows.Media.Media3D.Point3DCollection positions)
    {
        var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();
        positions = new System.Windows.Media.Media3D.Point3DCollection();
        var indices = new System.Windows.Media.Int32Collection();
        var texCoords = new System.Windows.Media.PointCollection();

        // Расчет вершин, текстурных координат и триангуляция
        for (int r = 0; r < map3D.Rows; r++)
        {
            for (int c = 0; c < map3D.Cols; c++)
            {
                // Заменяем парсинг строк на чтение прямого ОЗУ-массива ЭБУ!
                double val = map3D.GetTableValue(r, c); // Вызовет return MatrixData[r, c]; из бэкэнда

                double x = (c * StepX) - halfWidth;
                // Твоя инвертированная гоночная формула оси Y
                double y = ((map3D.Rows - 1 - r) * StepY) - halfLength;
                // Рассчитываем честную высоту вершины Z в пространстве Helix
                double z = (val - minVal) * scaleZ;

                positions.Add(new Point3D(x, y, z));

                double normZ = (delta > 0.001) ? ((val - minVal) / delta) : 0.5;
                //texCoords.Add(new System.Windows.Foundation.Point(0.5, 1.0 - normZ));
            }
        }
        mesh.Positions = positions;
        mesh.TextureCoordinates = texCoords;

        // Заполнение треугольников
        for (int r = 0; r < map3D.Rows - 1; r++)
        {
            for (int c = 0; c < map3D.Cols - 1; c++)
            {
                int i = r * map3D.Cols + c;
                int nextR = (r + 1) * map3D.Cols + c;
                indices.Add(i); indices.Add(i + 1); indices.Add(nextR);
                indices.Add(i + 1); indices.Add(nextR + 1); indices.Add(nextR);
            }
        }
        mesh.TriangleIndices = indices;
        mesh.Normals = new System.Windows.Media.Media3D.Vector3DCollection(Enumerable.Repeat(new System.Windows.Media.Media3D.Vector3D(0, 0, 1), positions.Count));
        mesh.Freeze();
        return mesh;
    }

    /// <summary>
    /// Шаг 3: Нарезка четырехугольных ребер (БЕЗ ДИАГОНАЛЕЙ) [1.14]
    /// </summary>
    private System.Windows.Media.Media3D.Point3DCollection BuildSurfaceEdges(Map3DVariableViewModel map3D, System.Windows.Media.Media3D.Point3DCollection positions, double minVal, double delta)
    {
        var lines = new System.Windows.Media.Media3D.Point3DCollection();
        // Горизонтальные ребра
        for (int r = 0; r < map3D.Rows; r++)
            for (int c = 0; c < map3D.Cols - 1; c++)
            {
                lines.Add(positions[r * map3D.Cols + c]);
                lines.Add(positions[r * map3D.Cols + (c + 1)]);
            }
        // Вертикальные ребра
        for (int c = 0; c < map3D.Cols; c++)
            for (int r = 0; r < map3D.Rows - 1; r++)
            {
                lines.Add(positions[r * map3D.Cols + c]);
                lines.Add(positions[(r + 1) * map3D.Cols + c]);
            }
        lines.Freeze();
        return lines;
    }
    /// <summary>
    /// Шаг 4: Динамическая сборка коробки-обрешетки под размер Rows и Cols (ChipTuningPRO Style) [1.14]
    /// </summary>
    private System.Windows.Media.Media3D.Point3DCollection BuildBoundingBox(Map3DVariableViewModel map3D, double halfWidth, double halfLength)
    {
        var boxLines = new System.Windows.Media.Media3D.Point3DCollection();

        // 1. СЕТКА ПОЛА (XY) — строго под рядами и колонками калибровки [1.14]
        for (int c = 0; c < map3D.Cols; c++)
        {
            double x = (c * StepX) - halfWidth;
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, -halfLength, 0));
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, halfLength, 0));
        }
        for (int r = 0; r < map3D.Rows; r++)
        {
            double y = (r * StepY) - halfLength;
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(-halfWidth, y, 0));
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(halfWidth, y, 0));
        }

        // 2. ВЕРТИКАЛЬНЫЕ СТЕНКИ (ЗАДНЯЯ Y=halfLength И БОКОВАЯ X=-halfWidth) [1.14]
        for (int c = 0; c < map3D.Cols; c++)
        {
            double x = (c * StepX) - halfWidth;
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, halfLength, 0));
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, halfLength, MaxHeightZ));
        }
        for (int r = 0; r < map3D.Rows; r++)
        {
            double y = (r * StepY) - halfLength;
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(-halfWidth, y, 0));
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(-halfWidth, y, MaxHeightZ));
        }

        // Нарезаем фиксированные 5 уровней высоты коробки [1.14]
        int heightLevels = 5;
        for (int i = 0; i <= heightLevels; i++)
        {
            double z = (MaxHeightZ / heightLevels) * i;
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(-halfWidth, halfLength, z));
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(halfWidth, halfLength, z));
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(-halfWidth, -halfLength, z));
            boxLines.Add(new System.Windows.Media.Media3D.Point3D(-halfWidth, halfLength, z));
        }

        boxLines.Freeze();
        return boxLines;
    }



    /// <summary>
    /// Шаг 5: Обновляет 3D-геометрию объемного лазерного цилиндра на основе плавных координат радара [0.1.5, 1.14]
    /// </summary>
    public void UpdateLaserBeamPosition(double exactColIndex, double exactRowIndex)
    {
        if (DataSource is not Map3DVariableViewModel map3D) return;

        // Расчет позиции центра, генерация 12-сегментной геометрии (вершины, нормали, индексы) [1.14]
        double laserX = (exactColIndex * StepX) - ((map3D.Cols - 1) * StepX) / 2.0;
        double laserY = ((map3D.Rows - 1 - exactRowIndex) * StepY) - ((map3D.Rows - 1) * StepY) / 2.0;

        var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();
        double radius = 0.6;
        int segments = 12;

        for (int i = 0; i <= segments; i++)
        {
            double theta = 2.0 * Math.PI * i / segments;
            double cos = Math.Cos(theta);
            double sin = Math.Sin(theta);
            var normal = new System.Windows.Media.Media3D.Vector3D(cos, sin, 0);

            mesh.Positions.Add(new System.Windows.Media.Media3D.Point3D(laserX + radius * cos, laserY + radius * sin, 0));
            mesh.Normals.Add(normal);
            mesh.Positions.Add(new System.Windows.Media.Media3D.Point3D(laserX + radius * cos, radius * sin + laserY, 60.0));
            mesh.Normals.Add(normal);
        }

        for (int i = 0; i < segments; i++)
        {
            int bL = i * 2, tL = bL + 1, bR = bL + 2, tR = bL + 3;
            mesh.TriangleIndices.Add(tL); mesh.TriangleIndices.Add(bL); mesh.TriangleIndices.Add(tR);
            mesh.TriangleIndices.Add(tR); mesh.TriangleIndices.Add(bL); mesh.TriangleIndices.Add(bR);
        }
        mesh.Freeze();

        System.Windows.Application.Current?.Dispatcher?.Invoke(() => LaserBeamMesh = mesh);
    }

    /// <summary>
    /// Локальная подфункция: вычисляет экстремумы 3D-матрицы для расчета стабильного масштаба [1.14]
    /// </summary>
    private void FindMatrixExtremes(Map3DVariableViewModel map3D, out double minVal, out double maxVal, out double delta)
    {
        minVal = double.MaxValue;
        maxVal = double.MinValue;

        for (int r = 0; r < map3D.Rows; r++)
        {
            for (int c = 0; c < map3D.Cols; c++)
            {
                double v = map3D.MatrixData[r, c];
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }
        }
        delta = maxVal - minVal;
    }


    /// <summary>
    /// Обновляет кристаллическую решетку и подсвечивает синим цветом вершины под курсором таблицы.
    /// </summary>
    public void UpdateCursorVerticesHighlight(System.Windows.Media.Media3D.Point3DCollection sourcePositions)
    {
        if (sourcePositions == null || sourcePositions.Count == 0 || DataSource is not Map3DVariableViewModel map3D) return;

        var allMesh = new System.Windows.Media.Media3D.MeshGeometry3D();
        var selectedMesh = new System.Windows.Media.Media3D.MeshGeometry3D();

        foreach (var pt in sourcePositions)
        {
            AddSphereToMesh(allMesh, pt, 0.4);
        }
        allMesh.Freeze();

        int minRow = Math.Max(0, Math.Min(map3D.AnchorRow, map3D.SelectedRow));
        int maxRow = Math.Min(map3D.Rows - 1, Math.Max(map3D.AnchorRow, map3D.SelectedRow));
        int minCol = Math.Max(0, Math.Min(map3D.AnchorCol, map3D.SelectedCol));
        int maxCol = Math.Min(map3D.Cols - 1, Math.Max(map3D.AnchorCol, map3D.SelectedCol));
        bool hasSelection = false;

        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                int targetIndex = r * map3D.Cols + c;
                if (targetIndex >= 0 && targetIndex < sourcePositions.Count)
                {
                    AddSphereToMesh(selectedMesh, sourcePositions[targetIndex], 1.0);
                    hasSelection = true;
                }
            }
        }

        if (hasSelection) selectedMesh.Freeze();
        else selectedMesh = new System.Windows.Media.Media3D.MeshGeometry3D();

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