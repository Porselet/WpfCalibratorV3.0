using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Windows.Media.Media3D;
using WpfCalibrator.ViewModels;

namespace WpfCalibrator.Services
{
    public class Matrix3DGeometryService : IMatrix3DGeometryService
    {
        private const double StepX = 15.0;
        private const double StepY = 15.0;
        private const double MaxHeightZ = 30.0;
        public SurfaceGeometryResult BuildGeometry( double[,] matrixData,
                                                    int rows,
                                                    int cols,
                                                    double? fixedMinVal,
                                                    double? fixedMaxVal,
                                                    double? fixedScaleZ
                                                    )
        {
            var res = new SurfaceGeometryResult();
            double minVal;
            double maxVal;
            double delta;
            // 🚀 УМНАЯ ФИКСАЦИЯ МАСШТАБА:
            // Мы пересчитываем масштаб, если он ЕЩЕ НЕ зафиксирован, 
            // ЛИБО если прошлый расчет зафиксировался на пустых нулях (delta была равна 0)
            if (fixedScaleZ == null || fixedMinVal == null || fixedMaxVal == null || Math.Abs(fixedMaxVal.Value - fixedMinVal.Value) < 0.001)
            {
                // Сканируем живую матрицу углов зажигания в ОЗУ [1.14]
                this.FindMatrixExtremes(matrixData, rows, cols, out minVal, out maxVal, out delta);

                // Замораживаем масштаб ТОЛЬКО если прошивка реально прислала боевые числа (delta > 0)
                if (delta > 0.001)
                {
                    fixedMinVal = minVal;
                    fixedMaxVal = maxVal;
                    fixedScaleZ = MaxHeightZ / delta; // Вычисляем постоянный коэффициент высоты Z
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
                minVal = fixedMinVal.Value;
                maxVal = fixedMaxVal.Value;
                delta = maxVal - minVal;
            }

            double scaleZ = fixedScaleZ ?? 1.0;
            double halfWidth = ((cols - 1) * StepX) / 2.0;
            double halfLength = ((rows - 1) * StepY) / 2.0;

            res.Mesh = BuildSurfaceMesh(matrixData, rows, cols, minVal, delta, scaleZ, halfWidth, halfLength, out var positions);
            res.Positions = positions;
            res.Edges = BuildSurfaceEdges(matrixData, rows, cols, positions, minVal, delta);
            res.BoundingBox = BuildBoundingBox(matrixData, rows, cols, halfWidth, halfLength);

            return res;
        }

        /// <summary>
        /// Локальная подфункция: вычисляет экстремумы 3D-матрицы для расчета стабильного масштаба [1.14]
        /// </summary>
        private void FindMatrixExtremes(double[,] matrixData, int rows, int cols, out double minVal, out double maxVal, out double delta)
        {
            minVal = double.MaxValue;
            maxVal = double.MinValue;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    double v = matrixData[r, c];
                    if (v < minVal) minVal = v;
                    if (v > maxVal) maxVal = v;
                }
            }
            delta = maxVal - minVal;
        }


        /// <summary>
        /// Шаг 2: Сборка твердотельного полигонального рельефа и расчет тепловой карты текстур [1.14]
        /// </summary>
        private System.Windows.Media.Media3D.MeshGeometry3D BuildSurfaceMesh(double[,] matrixData, int rows, int cols, double minVal, double delta, double scaleZ, double halfWidth, double halfLength, out System.Windows.Media.Media3D.Point3DCollection positions)
        {
            var mesh = new System.Windows.Media.Media3D.MeshGeometry3D();
            positions = new System.Windows.Media.Media3D.Point3DCollection();
            var indices = new System.Windows.Media.Int32Collection();
            var texCoords = new System.Windows.Media.PointCollection();

            // Расчет вершин, текстурных координат и триангуляция
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Заменяем парсинг строк на чтение прямого ОЗУ-массива ЭБУ!
                    double val = matrixData[r, c]; // Вызовет return MatrixData[r, c]; из бэкэнда

                    double x = (c * StepX) - halfWidth;
                    // Твоя инвертированная гоночная формула оси Y
                    double y = ((rows - 1 - r) * StepY) - halfLength;
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
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int i = r * cols + c;
                    int nextR = (r + 1) * cols + c;
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
        private System.Windows.Media.Media3D.Point3DCollection BuildSurfaceEdges(double[,] matrixData, int rows, int cols, System.Windows.Media.Media3D.Point3DCollection positions, double minVal, double delta)
        {
            var lines = new System.Windows.Media.Media3D.Point3DCollection();
            // Горизонтальные ребра
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols - 1; c++)
                {
                    lines.Add(positions[r * cols + c]);
                    lines.Add(positions[r * cols + (c + 1)]);
                }
            // Вертикальные ребра
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows - 1; r++)
                {
                    lines.Add(positions[r * cols + c]);
                    lines.Add(positions[(r + 1) * cols + c]);
                }
            lines.Freeze();
            return lines;
        }
        /// <summary>
        /// Шаг 4: Динамическая сборка коробки-обрешетки под размер Rows и Cols (ChipTuningPRO Style) [1.14]
        /// </summary>
        private System.Windows.Media.Media3D.Point3DCollection BuildBoundingBox(double[,] matrixData, int rows, int cols, double halfWidth, double halfLength)
        {
            var boxLines = new System.Windows.Media.Media3D.Point3DCollection();

            // 1. СЕТКА ПОЛА (XY) — строго под рядами и колонками калибровки [1.14]
            for (int c = 0; c < cols; c++)
            {
                double x = (c * StepX) - halfWidth;
                boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, -halfLength, 0));
                boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, halfLength, 0));
            }
            for (int r = 0; r < rows; r++)
            {
                double y = (r * StepY) - halfLength;
                boxLines.Add(new System.Windows.Media.Media3D.Point3D(-halfWidth, y, 0));
                boxLines.Add(new System.Windows.Media.Media3D.Point3D(halfWidth, y, 0));
            }

            // 2. ВЕРТИКАЛЬНЫЕ СТЕНКИ (ЗАДНЯЯ Y=halfLength И БОКОВАЯ X=-halfWidth) [1.14]
            for (int c = 0; c < cols; c++)
            {
                double x = (c * StepX) - halfWidth;
                boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, halfLength, 0));
                boxLines.Add(new System.Windows.Media.Media3D.Point3D(x, halfLength, MaxHeightZ));
            }
            for (int r = 0; r < rows; r++)
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


    }
}
