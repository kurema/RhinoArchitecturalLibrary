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

namespace kurema
{
    namespace RhinoTools
    {
        public static class GeneralHelper
        {
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

            public static Brep CreateSweepCircle(Curve RailCurve, double Radius)
            {
                return CreateSweep(RailCurve, NurbsCurve.CreateFromCircle(new Circle(Radius)));
            }

            public static Brep CreateSweep(Curve RailCurve, Curve BaseShape)
            {
                Brep result = Brep.CreateFromSweep(RailCurve, GetCurveForSweep(BaseShape, RailCurve), true, 1e-2)[0];
                if (!RailCurve.IsClosed)
                {
                    result.CapPlanarHoles(0);
                }
                return result;
            }
            public static Curve GetCurveForSweep(Curve Shape, Curve Rail)
            {
                Rail.Domain = new Interval(0, 1);
                Shape.Rotate(Math.PI / 2.0, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
                Shape.Rotate(GeneralHelper.GetCurvatureAsAngle(Rail, 0), new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                Shape.Translate((Vector3d)Rail.PointAtStart);
                return Shape;
            }

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
            public static Brep[] TranslateBreps(Brep[] Origin, Point3d To)
            {
                return TranslateBreps(Origin, (Vector3d)To);
            }
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

            public static Brep[] RotateBreps(Brep[] Origin, double AngleRadians, Vector3d rotationAxis)
            {
                return RotateBreps(Origin, AngleRadians, rotationAxis, new Point3d(0, 0, 0));
            }

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

            public static Brep[] RepeatBrep(Brep Origin, Vector3d Vector, int Count)
            {
                return RepeatBrep(Origin, Vector, new Vector3d(0, 0, 0), Count, 1);
            }

            public static Brep[] RepeatBrep(Brep Origin, Vector3d Vector1, Vector3d Vector2, int CountX, int CountY)
            {
                return RepeatBrep(Origin, Vector1, Vector2, new Vector3d(0, 0, 0), CountX, CountY, 1);
            }

            public static Brep[] RepeatBrep(Brep[] Origin, Vector3d Vector, int Count)
            {
                return RepeatBrep(Origin, Vector, new Vector3d(0, 0, 0), Count, 1);
            }

            public static Brep[] RepeatBrep(Brep[] Origin, Vector3d Vector1, Vector3d Vector2, int CountX, int CountY)
            {
                return RepeatBrep(Origin, Vector1, Vector2, new Vector3d(0, 0, 0), CountX, CountY, 1);
            }

            public static Brep[] RepeatBrep(Brep Origin, Vector3d Vector1, Vector3d Vector2, Vector3d Vector3, int CountX, int CountY, int CountZ)
            {
                return RepeatBrep(new Brep[] { Origin }, Vector1, Vector2, Vector3, CountX, CountY, CountZ);
            }

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
}