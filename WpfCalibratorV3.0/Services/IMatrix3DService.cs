using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Media3D;

namespace WpfCalibrator.Services
{
    public struct SurfaceGeometryResult
    {
        public MeshGeometry3D Mesh { get; set; }
        public Point3DCollection Edges { get; set; }
        public Point3DCollection BoundingBox { get; set; }
        public Model3DGroup SpheresGroup { get; set; }
        public Point3DCollection Positions { get; set; } // Кэш для курсора

        public double CalculatedMin { get; set; }
        public double CalculatedMax { get; set; }
        public double CalculatedScaleZ { get; set; }
    }
    public interface IMatrix3DGeometryService
    {
        SurfaceGeometryResult BuildGeometry(
            double[,] matrixData,
            int rows,
            int cols,
            double? fixedMinVal,
            double? fixedMaxVal,
            double? fixedScaleZ
        );
    }

}
