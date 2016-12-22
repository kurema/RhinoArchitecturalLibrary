using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using Rhino.Collections;

using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Collections;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace kurema.RhinoTools
{
    /// <summary>
    /// Rhino Commonに標準では含まれない追加的関数を含みます。
    /// </summary>
    public static class GeneralHelper
    {
        /// <summary>
        /// Brepを二点に合わせて配置します。
        /// </summary>
        /// <param name="Origin">Brep</param>
        /// <param name="Point1">点1</param>
        /// <param name="Point2">点2</param>
        /// <returns>結果</returns>
        public static Brep FitTwoPoint(Brep Origin, Point3d Point1, Point3d Point2)
        {
            Vector3d Direction = Point2 - Point1;
            Brep Result = (Brep)Origin.Duplicate();
            double Angle;
            if (Direction.X == 0) { Angle = Direction.Y > 0 ? Math.PI / 2.0 : -Math.PI / 2.0; }
            else
            {
                Angle = Math.Atan(Direction.Y / Direction.X);
                if (Direction.X < 0) { Angle += Math.PI; }
            }

            Result.Rotate(Angle, new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
            Result.Translate((Vector3d)Point1);
            return Result;
        }

        /// <summary>
        /// 二点に合わせた移動を示すTransfromを取得します。
        /// </summary>
        /// <param name="Point1">点1</param>
        /// <param name="Point2">点2</param>
        /// <returns>Transform</returns>
        public static Transform FitTwoPoint(Point3d Point1, Point3d Point2)
        {
            Vector3d Direction = Point2 - Point1;
            Transform tf = Transform.Identity;
            double Angle;
            if (Direction.X == 0) { Angle = Direction.Y > 0 ? Math.PI / 2.0 : -Math.PI / 2.0; }
            else
            {
                Angle = Math.Atan(Direction.Y / Direction.X);
                if (Direction.X < 0) { Angle += Math.PI; }
            }
            tf *= Transform.Translation((Vector3d)Point1);
            tf *= Transform.Rotation(Angle, new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
            return tf;
        }

        /// <summary>
        /// Extrusionを行った結果にCapを行います。
        /// </summary>
        /// <param name="Section">断面</param>
        /// <param name="Direction">方向</param>
        /// <returns>結果</returns>
        public static Brep[] CreateExtrusionCaped(Curve[] Section, Vector3d Direction)
        {
            List<Brep> Result = new List<Brep>();

            Brep[] BaseBrep = Brep.CreatePlanarBreps(Section);
            Result.AddRange(BaseBrep);
            foreach (Brep bp in BaseBrep)
            {
                Brep TempBrep = (Brep)bp.Duplicate();
                TempBrep.Translate(Direction);
                Result.Add(TempBrep);
            }
            foreach (Curve cv in Section)
            {
                Result.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(cv, Direction)));
            }
            //return Brep.CreateSolid(Result, Direction.Length/100.0);
            return Result.ToArray();
        }

        /// <summary>
        /// 複数のBrepを複製します。手軽に使えます。
        /// </summary>
        /// <param name="Origin">複製元のBrep</param>
        /// <returns>複製後のBrep</returns>
        public static Brep[] DuplicateBreps(Brep[] Origin)
        {
            Brep[] Result = new Brep[Origin.GetLength(0)];
            for (int i = 0; i < Origin.GetLength(0); i++)
            {
                if (Origin[i] == null) continue;
                Result[i] = (Brep)Origin[i].Duplicate();
            }
            return Result;
        }

        /// <summary>
        /// Curveに合わせて蓋つきパイプを作ります。(円でSweep)
        /// </summary>
        /// <param name="RailCurve">Curve</param>
        /// <param name="Radius">半径</param>
        /// <returns>結果</returns>
        public static Brep CreateSweepCircle(Curve RailCurve, double Radius)
        {
            return CreateSweep(RailCurve, NurbsCurve.CreateFromCircle(new Circle(Radius)));
        }

        /// <summary>
        /// Sweepし、可能ならCapします。
        /// </summary>
        /// <param name="RailCurve">Curve</param>
        /// <param name="BaseShape">断面</param>
        /// <returns>結果</returns>
        public static Brep CreateSweep(Curve RailCurve, Curve BaseShape)
        {
            Brep result = Brep.CreateFromSweep(RailCurve, GetCurveForSweep(BaseShape, RailCurve), true, 1e-2)[0];
            if (!RailCurve.IsClosed)
            {
                result.CapPlanarHoles(0);
            }
            return result;
        }
        /// <summary>
        /// Sweepを実行する為に断面の向きを変えます。
        /// これはBrep.CreateFromSweepの特性に対する補助です。
        /// </summary>
        /// <param name="Shape">基本形状</param>
        /// <param name="Rail">SweepするCurve(レール)</param>
        /// <returns>結果</returns>
        public static Curve GetCurveForSweep(Curve Shape, Curve Rail)
        {
            Rail.Domain = new Interval(0, 1);
            Shape.Rotate(Math.PI / 2.0, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
            Shape.Rotate(GeneralHelper.GetCurvatureAsAngle(Rail, 0), new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
            Shape.Translate((Vector3d)Rail.PointAtStart);
            return Shape;
        }

        /// <summary>
        /// Curveのある点での曲率を角度として取得します。
        /// </summary>
        /// <param name="cv">Curve</param>
        /// <param name="t">t</param>
        /// <returns>角度</returns>
        public static double GetCurvatureAsAngle(Curve cv, double t)
        {
            Vector3d Curvature = cv.TangentAt(t);

            double angle = 0;
            if (Curvature.Y == 0) { angle = Math.PI / 2.0; }
            else
            {
                angle = -Math.Atan(Curvature.X / Curvature.Y);
                if (Curvature.X < 0) { angle += Math.PI; }
            }
            return angle;
        }
        /// <summary>
        /// 複数のBrepを移動します。
        /// </summary>
        /// <param name="Origin">移動するBrep</param>
        /// <param name="To">移動方向</param>
        /// <returns>移動結果</returns>
        public static Brep[] TranslateBreps(Brep[] Origin, Point3d To)
        {
            return TranslateBreps(Origin, (Vector3d)To);
        }
        /// <summary>
        /// 複数のBrepを移動します。
        /// </summary>
        /// <param name="Origin">移動するBrep</param>
        /// <param name="To">移動方向</param>
        /// <returns>移動結果</returns>
        public static Brep[] TranslateBreps(Brep[] Origin, Vector3d To)
        {
            if (Origin == null) { return null; }
            Brep[] Result = new Brep[Origin.GetLength(0)];
            for (int i = 0; i < Origin.GetLength(0); i++)
            {
                Brep TempBrep = (Brep)Origin[i].Duplicate();
                TempBrep.Translate(To);
                Result[i] = TempBrep;
            }
            return Result;
        }
        /// <summary>
        /// 複数のBrepを回転します。
        /// </summary>
        /// <param name="Origin">回転元のBrep</param>
        /// <param name="AngleRadians">回転角度</param>
        /// <param name="rotationAxis">回転軸</param>
        /// <returns></returns>
        public static Brep[] RotateBreps(Brep[] Origin, double AngleRadians, Vector3d rotationAxis)
        {
            return RotateBreps(Origin, AngleRadians, rotationAxis, new Point3d(0, 0, 0));
        }
        /// <summary>
        /// 複数のBrepを回転します。
        /// </summary>
        /// <param name="Origin">回転元のBrep</param>
        /// <param name="AngleRadians">回転角度</param>
        /// <param name="rotationAxis">回転軸</param>
        /// <param name="rotationCenter">回転中心</param>
        /// <returns>結果</returns>
        public static Brep[] RotateBreps(Brep[] Origin, double AngleRadians, Vector3d rotationAxis, Point3d rotationCenter)
        {
            Brep[] Result = new Brep[Origin.GetLength(0)];
            for (int i = 0; i < Origin.GetLength(0); i++)
            {
                Brep TempBrep = (Brep)Origin[i].Duplicate();
                TempBrep.Rotate(AngleRadians, rotationAxis, rotationCenter);
                Result[i] = TempBrep;
            }
            return Result;
        }

        /// <summary>
        /// 複数のBrepを回転します。RotateBrepと同じ内容ですが互換性の為に残されています。
        /// </summary>
        /// <param name="Origin">回転元のBrep</param>
        /// <param name="AngleRadians">回転角度</param>
        /// <param name="rotationAxis">回転軸</param>
        /// <param name="rotationCenter">回転中心</param>
        /// <returns>結果</returns>
        public static Brep[] RotateBrep(Brep[] origin, double angleRadians, Vector3d rotationAxis, Point3d rotationCenter)
        {
            Brep[] Result = new Brep[origin.GetLength(0)];
            for (int i = 0; i < origin.GetLength(0); i++)
            {
                Brep bp = (Brep)origin[i].Duplicate();
                bp.Rotate(angleRadians, rotationAxis, rotationCenter);
                Result[i] = bp;
            }
            return Result;
        }

        /// <summary>
        /// Brepを一定間隔で配置します。
        /// </summary>
        /// <param name="Origin">配置するBrep</param>
        /// <param name="Vector">移動方向</param>
        /// <param name="Count">個数</param>
        /// <returns>結果</returns>
        public static Brep[] RepeatBrep(Brep Origin, Vector3d Vector, int Count)
        {
            return RepeatBrep(Origin, Vector, new Vector3d(0, 0, 0), Count, 1);
        }
        /// <summary>
        /// Brepを一定間隔で二方向に配置します。
        /// </summary>
        /// <param name="Origin">配置するBrep</param>
        /// <param name="Vector1">移動方向1</param>
        /// <param name="Vector2">移動方向2</param>
        /// <param name="CountX">方向1の配置個数</param>
        /// <param name="CountY">方向2の配置個数</param>
        /// <returns>結果</returns>
        public static Brep[] RepeatBrep(Brep Origin, Vector3d Vector1, Vector3d Vector2, int CountX, int CountY)
        {
            return RepeatBrep(Origin, Vector1, Vector2, new Vector3d(0, 0, 0), CountX, CountY, 1);
        }
        /// <summary>
        /// 複数のBrepを一定間隔で配置します。
        /// </summary>
        /// <param name="Origin">配置するBrep</param>
        /// <param name="Vector">移動方向</param>
        /// <param name="Count">個数</param>
        /// <returns>結果</returns>
        public static Brep[] RepeatBrep(Brep[] Origin, Vector3d Vector, int Count)
        {
            return RepeatBrep(Origin, Vector, new Vector3d(0, 0, 0), Count, 1);
        }
        /// <summary>
        /// 複数のBrepを一定間隔で二方向に配置します。
        /// </summary>
        /// <param name="Origin">配置するBrep</param>
        /// <param name="Vector1">移動方向1</param>
        /// <param name="Vector2">移動方向2</param>
        /// <param name="CountX">方向1の配置個数</param>
        /// <param name="CountY">方向2の配置個数</param>
        /// <returns>結果</returns>
        public static Brep[] RepeatBrep(Brep[] Origin, Vector3d Vector1, Vector3d Vector2, int CountX, int CountY)
        {
            return RepeatBrep(Origin, Vector1, Vector2, new Vector3d(0, 0, 0), CountX, CountY, 1);
        }
        /// <summary>
        /// Brepを一定間隔で三方向に配置します。
        /// </summary>
        /// <param name="Origin">配置するBrep</param>
        /// <param name="Vector1">移動方向1</param>
        /// <param name="Vector2">移動方向2</param>
        /// <param name="Vector3">移動方向3</param>
        /// <param name="CountX">方向1の配置個数</param>
        /// <param name="CountY">方向2の配置個数</param>
        /// <param name="CountZ">方向3の配置個数</param>
        /// <returns>結果</returns>
        public static Brep[] RepeatBrep(Brep Origin, Vector3d Vector1, Vector3d Vector2, Vector3d Vector3, int CountX, int CountY, int CountZ)
        {
            return RepeatBrep(new Brep[] { Origin }, Vector1, Vector2, Vector3, CountX, CountY, CountZ);
        }
        /// <summary>
        /// 複数のBrepを一定間隔で三方向に配置します。
        /// </summary>
        /// <param name="Origin">配置するBrep</param>
        /// <param name="Vector1">移動方向1</param>
        /// <param name="Vector2">移動方向2</param>
        /// <param name="Vector3">移動方向3</param>
        /// <param name="CountX">方向1の配置個数</param>
        /// <param name="CountY">方向2の配置個数</param>
        /// <param name="CountZ">方向3の配置個数</param>
        /// <returns>結果</returns>
        public static Brep[] RepeatBrep(Brep[] Origin, Vector3d Vector1, Vector3d Vector2, Vector3d Vector3, int CountX, int CountY, int CountZ)
        {
            Brep[] Result = new Brep[CountX * CountY * CountZ * Origin.GetLength(0)];
            for (int i = 0; i < CountZ; i++)
            {
                for (int j = 0; j < CountY; j++)
                {
                    for (int k = 0; k < CountX; k++)
                    {
                        for (int l = 0; l < Origin.GetLength(0); l++)
                        {
                            Brep tempBrep = (Brep)Origin[l].Duplicate();
                            tempBrep.Translate(Vector1 * k + Vector2 * j + Vector3 * i);
                            Result[i * CountX * CountY * Origin.GetLength(0) + j * CountX * Origin.GetLength(0) + k * Origin.GetLength(0) + l] = tempBrep;
                        }
                    }
                }
            }
            return Result;
        }

        /// <summary>
        /// 複数のBrepを複製します。
        /// </summary>
        /// <param name="origin">複製元Brep</param>
        /// <returns>複製結果</returns>
        public static Brep[] DuplicateBrep(Brep[] origin)
        {
            Brep[] result = new Brep[origin.GetLength(0)];
            for (int i = 0; i < origin.GetLength(0); i++)
            {
                result[i] = (Brep)origin[i].Duplicate();
            }
            return result;
        }
    }
}