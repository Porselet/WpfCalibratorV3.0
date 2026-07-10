using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Media.Media3D;
using WpfCalibrator.Services;

namespace WpfCalibrator.ViewModels.WidgetViewModel
{
    public class Matrix3DWidgetViewModel: BaseWidgetViewModel 
    {
        public Matrix3DWidgetViewModel(VariableViewModelBase dataSource) : base(dataSource)
        {

            if (ControlView == "Matrix3DSurface")
            {
                Rebuild3DSurfaceMesh();
            }
        }


        protected override void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // ПОТОК 2: Логика обработки таблиц (2D Радар + 3D Поверхность Helix)
            if (DataSource is TableVariableViewModelBase tableVar)
            {
                // А) Если обновились координаты смещения радара в ОЗУ — двигаем мишень
                if (e.PropertyName == "RadarGridOffsetX") OnPropertyChanged(nameof(RadarGridOffsetX));
                if (e.PropertyName == "RadarGridOffsetY") OnPropertyChanged(nameof(RadarGridOffsetY));

                // Б) 🔥 ВОТ ОН — СЕТЕВОЙ ЗАПУСК 3D-ГОР:
                // Если бэкэнд сообщает, что изменился массив калибровок, 
                // и перед инженером сейчас открыта именно 3D-поверхность...
                if (e.PropertyName == "SelectedRow" || e.PropertyName == "SelectedCol" || e.PropertyName == "AnchorRow" || e.PropertyName == "AnchorCol")
                {
                    if (ControlView == "Matrix3DSurface")
                    {
                        // Мгновенно двигаем синие шары со скоростью 60 FPS, вообще не трогая саму гору!
                        this.Refresh3DSelectionOnly();
                    }
                }
                if (e.PropertyName == "MatrixData" || e.PropertyName == "CurrentValue")
                {
                    if (ControlView == "Matrix3DSurface")
                    {
                        //if (IsEditing || DataSource.IsUpdatingFromNetwork) return; // Защита Helix [1.14]
                        // Вызываем наш тяжелый метод пересчета мешей и триангуляции!
                        this.Rebuild3DSurfaceMesh();
                    }
                }
            }

        }

        public void Refresh()
        {
            Rebuild3DSurfaceMesh();
        }
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

        // Объявляем ленивое поле. Объект НЕ создается прямо сейчас!
        private readonly Lazy<System.Windows.Media.Media3D.MeshGeometry3D> _sphereTemplateLoader = new Lazy<System.Windows.Media.Media3D.MeshGeometry3D>(() =>
        {
            // Этот код выполнится РОВНО ОДИН РАЗ за всё время работы программы
            // и только тогда, когда кто-то вызовет свойство Value.
            return GenerateWpfSphere(1.0);
        });

        private IMatrix3DGeometryService matrix3DGeometryService = new Matrix3DGeometryService();
        /// <summary>
        /// Главный диспетчер пересчета 3D-сцены
        /// </summary>
        private void Rebuild3DSurfaceMesh()
        {
            // ПОЛУЧЕНИЕ ДАННЫХ [1.14]
            if (DataSource is not Map3DVariableViewModel map3D) return;
            if (map3D.Rows <= 1 || map3D.Cols <= 1 || map3D.MatrixData == null) return;

            var res = matrix3DGeometryService.BuildGeometry(map3D.MatrixData, map3D.Rows, map3D.Cols, FixedMinVal, FixedMaxVal, FixedScaleZ);


            // 1. Создаем контейнер для группы 3D-моделей
            var spheresGroup = new System.Windows.Media.Media3D.Model3DGroup();

            // 2. Генерируем шаблон фонового шарика радиусом 0.15 через наш чистый метод

            // 3. Создаем материал для фоновых шариков (матовый серо-синий)
            var sphereMaterial = new System.Windows.Media.Media3D.DiffuseMaterial(
                new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A3D54")));
            var sphereTemplate = _sphereTemplateLoader.Value;
            // 4. Размножаем готовую сферу по всем координатам вершин
            foreach (var pt in res.Positions)
            {
                var model = new System.Windows.Media.Media3D.GeometryModel3D(sphereTemplate, sphereMaterial);
                model.Transform = new System.Windows.Media.Media3D.TranslateTransform3D(pt.X, pt.Y, pt.Z);
                model.Freeze();
                spheresGroup.Children.Add(model);
            }
            spheresGroup.Freeze();
            // Кэшируем точки рельефа в памяти вьюмодели
            _cachedPositions = res.Positions;
            // Атомарно закидываем меши в графический конвейер WPF
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                SurfaceMesh = res.Mesh;
                SurfaceLines = res.Edges;
                BoundingBoxLines = res.BoundingBox;

                // Привязываем готовую группу моделей к свойству для XAML
                AllSpheresModel = spheresGroup;

                // Запускаем пересчет больших синих шаров курсора мыши
                UpdateCursorVerticesHighlight(res.Positions);

                UpdateLaserBeamPosition(map3D.ActiveColIndex, map3D.ActiveRowIndex);
            });




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
        /// Генерирует честную полигональную 3D-сферу штатными средствами WPF Media3D без сторонних библиотек.
        /// </summary>
        static private System.Windows.Media.Media3D.MeshGeometry3D GenerateWpfSphere(double radius)
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
}
