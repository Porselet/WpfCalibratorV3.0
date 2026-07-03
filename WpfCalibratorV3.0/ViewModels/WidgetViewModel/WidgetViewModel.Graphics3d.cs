// Если проект на версии v3.0+, MeshBuilder лежит в корне HelixToolkit:
using HelixToolkit;
using HelixToolkit.Geometry;
using HelixToolkit.Wpf;          // Базовое пространство для MeshGeometryVisual3D
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
    private System.Windows.Media.Media3D.Model3DGroup _allSpheresModel = new();
    /// <summary> Готовая группа 3D-моделей фоновых объемных шариков решетки. </summary>
    public System.Windows.Media.Media3D.Model3DGroup AllSpheresModel
    {
        get => _allSpheresModel;
        set { _allSpheresModel = value; OnPropertyChanged(); } // 🔥 Пинок реактивности для XAML!
    }

    private System.Windows.Media.Media3D.Model3DGroup _selectedSpheresModel = new();
    /// <summary> Готовая группа 3D-моделей больших синих шаров выделения курсора. </summary>
    public System.Windows.Media.Media3D.Model3DGroup SelectedSpheresModel
    {
        get => _selectedSpheresModel;
        set { _selectedSpheresModel = value; OnPropertyChanged(); }
    }


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


    private System.Windows.Media.Media3D.Point3DCollection _cachedPositions;


    private Point3DCollection _surfacePoints = new();
    /// <summary> Координаты всех вершин калибровочной сетки для XAML-шаров. </summary>
    public Point3DCollection SurfacePoints
    {
        get => _surfacePoints;
        set { _surfacePoints = value; OnPropertyChanged(); }
    }


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

        double scaleZ = FixedScaleZ ?? 1.0;
        double halfWidth = ((map3D.Cols - 1) * StepX) / 2.0;
        double halfLength = ((map3D.Rows - 1) * StepY) / 2.0;

        var mesh = BuildSurfaceMesh(map3D, minVal, delta, scaleZ, halfWidth, halfLength, out var positions);
        var surfaceEdges = BuildSurfaceEdges(map3D, positions, minVal, delta);
        var boundingBox = BuildBoundingBox(map3D, halfWidth, halfLength);

        // 1. Создаем контейнер для группы 3D-моделей
        var spheresGroup = new System.Windows.Media.Media3D.Model3DGroup();

        // 2. Генерируем шаблон фонового шарика радиусом 0.15 через наш чистый метод
        var sphereTemplate = GenerateWpfSphere(1);

        // 3. Создаем материал для фоновых шариков (матовый серо-синий)
        var sphereMaterial = new System.Windows.Media.Media3D.DiffuseMaterial(
            new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A3D54")));

        // 4. Размножаем готовую сферу по всем координатам вершин
        foreach (var pt in positions)
        {
            var model = new System.Windows.Media.Media3D.GeometryModel3D(sphereTemplate, sphereMaterial);
            model.Transform = new System.Windows.Media.Media3D.TranslateTransform3D(pt.X, pt.Y, pt.Z);
            model.Freeze();
            spheresGroup.Children.Add(model);
        }
        spheresGroup.Freeze();
        // Кэшируем точки рельефа в памяти вьюмодели
        _cachedPositions = positions;
        // Атомарно закидываем меши в графический конвейер WPF
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            SurfaceMesh = mesh;
            SurfaceLines = surfaceEdges;
            BoundingBoxLines = boundingBox;

            // Привязываем готовую группу моделей к свойству для XAML
            AllSpheresModel = spheresGroup;

            // Запускаем пересчет больших синих шаров курсора мыши
            UpdateCursorVerticesHighlight(positions);

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
                texCoords.Add(new System.Windows.Point(0, normZ));
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
    /// Генерирует честную полигональную 3D-сферу штатными средствами WPF Media3D без сторонних библиотек.
    /// </summary>
    private System.Windows.Media.Media3D.MeshGeometry3D GenerateWpfSphere(double radius)
    {
        var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();
        int slices = 10;
        int stacks = 10;

        for (int stack = 0; stack <= stacks; stack++)
        {
            double phi = Math.PI * stack / stacks;
            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);

            for (int slice = 0; slice <= slices; slice++)
            {
                double theta = 2 * Math.PI * slice / slices;
                double sinTheta = Math.Sin(theta);
                double cosTheta = Math.Cos(theta);

                // Вычисляем координаты вершины сферы
                double x = radius * sinPhi * cosTheta;
                double y = radius * sinPhi * sinTheta;
                double z = radius * cosPhi;

                mesh.Positions.Add(new System.Windows.Media.Media3D.Point3D(x, y, z));
            }
        }

        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                int p0 = stack * (slices + 1) + slice;
                int p1 = p0 + 1;
                int p2 = p0 + slices + 1;
                int p3 = p2 + 1;

                // Триангуляция четырехугольника сферы (два треугольника для GPU)
                mesh.TriangleIndices.Add(p0);
                mesh.TriangleIndices.Add(p2);
                mesh.TriangleIndices.Add(p1);

                mesh.TriangleIndices.Add(p1);
                mesh.TriangleIndices.Add(p2);
                mesh.TriangleIndices.Add(p3);
            }
        }

        mesh.Freeze(); // Замораживаем в ОЗУ для ультра-быстрого FPS видеокарты
        return mesh;
    }

    /// <summary>
    /// Быстрое перемещение синего 3D-курсора без тяжелого пересчета рельефа горы.
    /// Вызывается при кликах мыши по DataGrid и перемещении стрелок клавиатуры.
    /// </summary>
    public void Refresh3DSelectionOnly()
    {
        // Если гора еще ни разу не строилась — бежать некуда
        if (_cachedPositions == null || _cachedPositions.Count == 0) return;

        // Вызываем только легкий метод обновления синих шаров, используя сохраненный кэш!
        UpdateCursorVerticesHighlight(_cachedPositions);
    }


    private void UpdateCursorVerticesHighlight(System.Windows.Media.Media3D.Point3DCollection sourcePositions)
    {
        if (sourcePositions == null || sourcePositions.Count == 0 || DataSource is not Map3DVariableViewModel map3D) return;

        var selectedGroup = new System.Windows.Media.Media3D.Model3DGroup();

        // Генерируем шаблон БОЛЬШОГО шара для курсора (радиус 0.4)
        var cursorTemplate = GenerateWpfSphere(1.2);

        // Глянцевый неоновый материал MoTeC-Style с зеркальным бликом
        var materialGroup = new System.Windows.Media.Media3D.MaterialGroup();
        materialGroup.Children.Add(new System.Windows.Media.Media3D.DiffuseMaterial(
            new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0080FF"))));
        materialGroup.Children.Add(new System.Windows.Media.Media3D.SpecularMaterial(
            System.Windows.Media.Brushes.White, 40));

        int minRow = Math.Max(0, Math.Min(map3D.AnchorRow, map3D.SelectedRow));
        int maxRow = Math.Min(map3D.Rows - 1, Math.Max(map3D.AnchorRow, map3D.SelectedRow));
        int minCol = Math.Max(0, Math.Min(map3D.AnchorCol, map3D.SelectedCol));
        int maxCol = Math.Min(map3D.Cols - 1, Math.Max(map3D.AnchorCol, map3D.SelectedCol));

        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                int index = r * map3D.Cols + c;
                if (index >= 0 && index < sourcePositions.Count)
                {
                    var model = new System.Windows.Media.Media3D.GeometryModel3D(cursorTemplate, materialGroup);
                    var pt = sourcePositions[index];
                    model.Transform = new System.Windows.Media.Media3D.TranslateTransform3D(pt.X, pt.Y, pt.Z);
                    model.Freeze();
                    selectedGroup.Children.Add(model);
                }
            }
        }
        selectedGroup.Freeze();

        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            SelectedSpheresModel = selectedGroup;
        });
    }


}