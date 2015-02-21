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
        public static class Providers
        {
            public static Curve GetRectangle3d(Point3d p, double Width, double Height)
            {
                Polyline pl = new Polyline();
                pl.Add(p);
                pl.Add(p + Vector3d.XAxis * Width);
                pl.Add(p + Vector3d.XAxis * Width + Vector3d.YAxis * Height);
                pl.Add(p + Vector3d.YAxis * Height);
                pl.Add(p);
                return pl.ToNurbsCurve();
            }

            public static Brep GetPaperSizeA(int i, bool Landscape)
            {
                double width = 1189 * Math.Pow(2.0, -i / 2.0);
                double height = 841 * Math.Pow(2.0, -i / 2.0);
                if (!Landscape) { double temp; temp = height; height = width; width = height; }
                return Brep.CreatePlanarBreps(new Rectangle3d(Plane.WorldZX, height, width).ToNurbsCurve())[0];
            }

            public static RealObject.Building GetThumbtack()
            {
                RealObject.Building result = new RealObject.Building("Thumback");
                Polyline pl = new Polyline(3);
                pl.Add(5, 0, 0.5);
                pl.Add(-7.5, 0, 0.5);
                pl.Add(-10.0, 0, 0);
                result.Content.Add(new RealObject.Member("Needle")
                {
                    Content = new Brep[]{
                Brep.CreateFromRevSurface(RevSurface.Create(pl.ToNurbsCurve(), new Line(0, 0, 0, 1, 0, 0)), true, true)},
                    Material = new RealObject.Material("Silver"),
                    DeatailLevel = -1
                });

                Curve[] cv = Curve.JoinCurves(new Curve[]{
                    new Arc(new Point3d(0,0,4),new Point3d(1,0,2+Math.Sqrt(2)),new Point3d(2,0,2)).ToNurbsCurve(),
                    new Line(2,0,2,7,0,1.5).ToNurbsCurve(),
                    new Line(7,0,1.5,7,0,2.5).ToNurbsCurve(),
                    new Arc(new Point3d(7,0,2.5),new Point3d(7.3,0,2.2),new Point3d(7.5,0,0)).ToNurbsCurve()
                });

                result.Content.Add(new RealObject.Member("Body"){
                    Content=new Brep[]{Brep.CreateFromRevSurface(RevSurface.Create(cv[0],new Line(0,0,0,1,0,0)),true,true)},
                    Material=new RealObject.Material("Plastic"),
                    DeatailLevel=0
                });
                return result;
            }

            public static Polyline GetKochCurve(double Angle,int Generation)
            {
                Polyline result = new Polyline();

                double crtangle = 0.0;
                Point3d crtpt = new Point3d(0, 0, 0);
                LSystem lsys= GetKochCurveLSystem();
                for(int i=0;i<Generation;i++){
                    lsys.ApplyRules();
                }
                foreach (LSystem.Body bd in lsys.Tree.Content)
                {
                    if (bd.BodyType == lsys.BodyTypes["F"])
                    {
                        crtpt += new Vector3d(Math.Cos(crtangle), Math.Sin(crtangle), 0);
                        result.Add(crtpt);
                    }
                    else if (bd.BodyType == lsys.BodyTypes["+"])
                    {
                        crtangle += Angle;
                    }
                    else if (bd.BodyType == lsys.BodyTypes["-"])
                    {
                        crtangle -= Angle;
                    }
                }
                return result;
            }

            public static LSystem GetKochCurveLSystem()
            {
                LSystem.BodyType F = new LSystem.BodyType("F");
                LSystem.BodyType P = new LSystem.BodyType("+");
                LSystem.BodyType M = new LSystem.BodyType("-");

                LSystem.Sequence initseq = new LSystem.Sequence(F);

                LSystem.RuleSimple rs = new LSystem.RuleSimple()
                {
                    Target = F,
                    Result = new LSystem.Sequence(F, P, F, M, F, M, F, P, F)//F+F-F-F+F
                };

                LSystem treesys = new LSystem(initseq, new LSystem.Rule[] { rs });
                treesys.RegisterBodyType(F,P,M);
                return treesys;
            }
            public static LSystem GetTreeLSystem()
            {
                LSystem.BodyType B = new LSystem.BodyType("B");//branch
                //LSystem.BodyType F = new LSystem.BodyType("F");//flower
                LSystem.BodyType L = new LSystem.BodyType("L");//leaf
                LSystem.BodyType G = new LSystem.BodyType("G");//growth
                LSystem.BodyType TR = new LSystem.BodyType("-");//turn_right
                LSystem.BodyType TL = new LSystem.BodyType("+");//turn_left
                LSystem.BodyType D = new LSystem.BodyType("D");//Divergence


                LSystem.Sequence initseq = new LSystem.Sequence(B, G);

                LSystem.RuleProbability rp = new LSystem.RuleProbability(new Random());
                rp.Target = G;
                rp.AddSequence(new LSystem.Sequence()
                {
                    Content = new List<LSystem.Body>(){
            new LSystem.Body(D){
              Child = new LSystem.Sequence[]{
              new LSystem.Sequence(B, G, L),
              new LSystem.Sequence(TL, B, TL, B, G)
              }
              }
            }
                }, 0.5);
                rp.AddSequence(new LSystem.Sequence()
                {
                    Content = new List<LSystem.Body>(){
            new LSystem.Body(D){
              Child = new LSystem.Sequence[]{
              new LSystem.Sequence(TR, B, G, L),
              new LSystem.Sequence(TL, B, L, G)
              }
              }
            }
                }, 0.5);

                LSystem treesys = new LSystem(initseq, new LSystem.Rule[] { rp });
                //treesys.RegisterBodyType(B, F, L, G, TR, TL, D);
                treesys.RegisterBodyType(B, L, G, TR, TL, D);
                return treesys;

            }

            public static GraphObject.Graph GetTreeGraph(int Generation)
            {
                LSystem lsys = GetTreeLSystem();
                lsys.ApplyRules(Generation);
                GraphObject.Graph Result = new GraphObject.Graph(new GraphObject.Path() { ContentMember = new Brep[0], Length = 0 }, new Point3d(0, 0, 0));
                Result.RotationAngleY = -Math.PI / 2.0;
                GraphObject.Path EndPath = Result.RootPath;
                GetTreeGraphBranch(ref Result, EndPath, lsys.Tree, lsys.BodyTypes, Math.PI / 6.0, 1500, 0.8, 60, 0.7, 0, Math.PI / 24.0, Math.PI / 6.0);
                return Result;
            }

            private static void GetTreeGraphBranch(ref GraphObject.Graph Result, GraphObject.Path EndPath, LSystem.Sequence sq, Dictionary<string, LSystem.BodyType> bodyTypes, double Angle, double BaseLength, double LengthRate, double BaseRad, double RadRate, double TwistAngle, double TwistAngleRange, double TwistAngleTwisted)
            {
                Brep LeafBrep;
                //LeafBrep = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(0, 40), new Interval(-6, 6), new Interval(-1, 0)));
                {
                    Curve LeafBaseCrv1 = Curve.CreateInterpolatedCurve(new Point3d[] { new Point3d(0, 0, 0), new Point3d(60, 50, 0), new Point3d(200, 0, 0) }, 3);
                    Curve LeafBaseCrv2 = (Curve)LeafBaseCrv1.Duplicate();
                    LeafBaseCrv2.Transform(Transform.Mirror(Plane.WorldZX));
                    Curve LeafBaseCrv = Curve.JoinCurves(new Curve[] { LeafBaseCrv1, LeafBaseCrv2 })[0];
                    LeafBrep = Brep.CreatePlanarBreps(new Curve[] { LeafBaseCrv })[0];
                }
                foreach (LSystem.Body bd in sq.Content)
                {
                    if (bd.BodyType == bodyTypes["B"])
                    {
                        //GraphObject.Path TmpPath = GraphObject.Path.CreateFromPipeSimple(new double[] { 0, BaseLength * Math.Pow(LengthRate, bd.Generation) }, new double[] { BaseRad * Math.Pow(RadRate, bd.Generation), BaseRad * Math.Pow(RadRate, bd.Generation + 1) });
                        GraphObject.Path TmpPath = GraphObject.Path.CreateFromRegularPolygonTower(5, BaseRad * Math.Pow(RadRate, bd.Generation), BaseLength * Math.Pow(LengthRate, bd.Generation));
                        TmpPath.Name = "Branch";
                        var TmpNode = Result.Add(EndPath, TmpPath);
                        EndPath = TmpPath;

                        TmpNode.RotationAngleX = 0;
                        TmpNode.RotationAngleY = TwistAngle;
                        TmpNode.RotationAngleYLimitation = new Interval(TwistAngle - (TwistAngleRange / 2.0), TwistAngle + (TwistAngleRange / 2.0));
                        TmpNode.RotationAngleZ = 0;
                        TmpNode.RotationAngleZLimitation = new Interval(0, 0);

                        TwistAngle = 0;
                    }
                    else if (bd.BodyType == bodyTypes["+"])
                    {
                        TwistAngle = TwistAngleTwisted;
                    }
                    else if (bd.BodyType == bodyTypes["-"])
                    {
                        TwistAngle = -TwistAngleTwisted;
                    }
                    else if (bd.BodyType == bodyTypes["L"])
                    {
                        GraphObject.Path TmpPath = new GraphObject.Path() { ContentMember = new Brep[] { (Brep)LeafBrep.Duplicate() } };
                        TmpPath.Name = "Leaf";
                        var TmpNode = Result.Add(EndPath, TmpPath);
                        EndPath = TmpPath;
                    }
                    //F省略
                    else if (bd.BodyType == bodyTypes["D"])
                    {
                        foreach (LSystem.Sequence tmpsq in bd.Child)
                        {
                            GetTreeGraphBranch(ref Result, EndPath, tmpsq, bodyTypes, Angle, BaseLength, LengthRate, BaseRad, RadRate, TwistAngle, TwistAngleRange, TwistAngleTwisted);
                        }

                    }

                }
            }

            public static Polyline[] GetTree2D(int Generation)
            {
                double Angle = Math.PI / 6.0;
                LSystem lsys = GetTreeLSystem();
                lsys.ApplyRules(Generation);
                return GetTree2DBranch(lsys.Tree, lsys.BodyTypes, new Point3d(0, 0, 0), 0, Angle);
            }

            private static Polyline[] GetTree2DBranch(LSystem.Sequence sq, Dictionary<string, LSystem.BodyType> bodyTypes, Point3d CurrentPoint, double CurrentAngle, double Angle)
            {
                Polyline result = new Polyline();
                result.Add(CurrentPoint);
                List<Polyline> PLs = new List<Polyline>();
                foreach (LSystem.Body bd in sq.Content)
                {
                    if (bd.BodyType == bodyTypes["B"])
                    {
                        CurrentPoint += new Vector3d(Math.Cos(CurrentAngle), Math.Sin(CurrentAngle), 0);
                        result.Add(CurrentPoint);
                    }
                    else if (bd.BodyType == bodyTypes["-"])
                    {
                        CurrentAngle -= Angle;
                    }
                    else if (bd.BodyType == bodyTypes["+"])
                    {
                        CurrentAngle += Angle;
                    }
                    else if (bd.BodyType == bodyTypes["D"])
                    {
                        foreach (LSystem.Sequence seq in bd.Child)
                        {
                            PLs.AddRange(GetTree2DBranch(seq, bodyTypes, CurrentPoint, CurrentAngle, Angle));
                        }
                    }
                }
                PLs.Add(result);
                return PLs.ToArray();
            }

            public static Brep[] GetHumanRandom(double Height)
            {
                return GetHumanRandom(Height, new Random());
            }

            public static Brep[] GetHumanRandom(double Height,Random rd)
            {
                GraphObject.Graph human = GraphObject.Graph.GetHumanBody(Height);
                human.MoveRandom(rd);
                return human.GetBrep();
            }

            public static Brep GetRegularPolygonTower(int c, double Radius, double height, bool Cap)
            {
                Brep result = Brep.CreateFromSurface(Surface.CreateExtrusion(GetRegularPolygon(c, Radius), new Vector3d(0, 0, height)));
                if (Cap) { result = result.CapPlanarHoles(0); }
                return result;
            }

            public static Curve GetRegularPolygon(int c, double Radius)
            {
                Polyline pl = new Polyline(c + 1);
                for (int i = 0; i <= c; i++)
                {
                    pl.Add(Math.Cos(Math.PI * (double)i / (double)c * 2.0) * Radius, Math.Sin(Math.PI * (double)i / (double)c * 2.0) * Radius, 0);
                }
                return pl.ToNurbsCurve();
            }

            public static void GetGlassWindowLight(int CountX, int CountY, double Width, double Height, double FrameWidth, double GlassThickness, out Brep[] Glass, out Brep[] Frame)
            {
                {
                    Polyline secPL = new Polyline(5);
                    secPL.Add(0, 0, 0);
                    secPL.Add(Width * CountX, 0, 0);
                    secPL.Add(Width * CountX, 0, Height * CountY);
                    secPL.Add(0, 0, Height * CountY);
                    secPL.Add(0, 0, 0);

                    List<Brep> GlassBrep = new List<Brep>();
                    Curve secCv = secPL.ToNurbsCurve();
                    GlassBrep.AddRange(Brep.CreatePlanarBreps(secCv));
                    GlassBrep.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(secCv, new Vector3d(0, GlassThickness, 0))));
                    secCv.Translate(0, GlassThickness, 0);
                    GlassBrep.AddRange(Brep.CreatePlanarBreps(secCv));
                    Glass = Brep.CreateSolid(GlassBrep, GlassThickness / 100.0);
                }
                Brep[] ResultFrame = GetFrame2d(CountX, CountY, Width, Height, FrameWidth, FrameWidth, FrameWidth, FrameWidth);
                for (int i = 0; i < ResultFrame.GetLength(0); i++)
                {
                    ResultFrame[i].Translate(0, -GlassThickness, 0);
                }
                Frame = ResultFrame;
            }

            public static Brep[] GetFrame2d(int CountX, int CountY, double Width, double Height, double FrameWidthTop, double FrameWidthLeft, double FrameWidthRight, double FrameWidthBottom)
            {
                Curve RecBase;
                {
                    Polyline recPL = new Polyline(5);
                    recPL.Add(FrameWidthRight, 0, FrameWidthBottom);
                    recPL.Add(Width - FrameWidthLeft, 0, FrameWidthBottom);
                    recPL.Add(Width - FrameWidthLeft, 0, Height - FrameWidthTop);
                    recPL.Add(FrameWidthRight, 0, Height - FrameWidthTop);
                    recPL.Add(FrameWidthRight, 0, FrameWidthBottom);
                    RecBase = recPL.ToNurbsCurve();
                }
                List<Curve> ResultBase = new List<Curve>();
                {
                    Polyline recPL = new Polyline(5);
                    recPL.Add(0, 0, 0);
                    recPL.Add(Width * CountX, 0, 0);
                    recPL.Add(Width * CountX, 0, Height * CountY);
                    recPL.Add(0, 0, Height * CountY);
                    recPL.Add(0, 0, 0);
                    ResultBase.Add(recPL.ToNurbsCurve());
                }

                for (int cntx = 0; cntx < CountX; cntx++)
                {
                    for (int cnty = 0; cnty < CountY; cnty++)
                    {
                        Curve Rec = (Curve)RecBase.Duplicate();
                        Rec.Translate(cntx * Width, 0, cnty * Height);
                        ResultBase.Add(Rec);
                    }
                }
                return Brep.CreatePlanarBreps(ResultBase);
            }

            public static void GetJapaneseRoof(double LengthTop, double LengthBottom, double Depth, double Pitch, double Height, double RafterSpace, double RafterWidth, double RafterHeight, out Brep[] Main, out Brep[] Rafter)
            {
                Main = GetJapaneseRoofMain(LengthTop, LengthBottom, Depth, Pitch, Height);
                Rafter = GetJapaneseRoofRafter(LengthTop, LengthBottom, Depth, Pitch, Height, RafterSpace, RafterWidth, RafterHeight);
            }

            public static Brep[] GetJapaneseRoofRafter(double LengthTop, double LengthBottom, double Depth, double Pitch, double Height, double RafterSpace, double RafterWidth, double RafterHeight)
            {
                List<Brep> Lafters = new List<Brep>();
                double RafterSpaceBottom = LengthBottom / Math.Floor(LengthBottom / RafterSpace);
                double RafterSpaceTop = LengthTop / Math.Floor(LengthBottom / RafterSpace);

                Curve SectionBase;
                {
                    Polyline SectionPL = new Polyline(5);
                    SectionPL.Add(RafterWidth / 2.0, 0, 0);
                    SectionPL.Add(RafterWidth / 2.0, 0, -RafterHeight);
                    SectionPL.Add(-RafterWidth / 2.0, 0, -RafterHeight);
                    SectionPL.Add(-RafterWidth / 2.0, 0, 0);
                    SectionPL.Add(RafterWidth / 2.0, 0, 0);

                    SectionBase = SectionPL.ToNurbsCurve();
                }

                double CurrentPositonTop = -LengthTop / 2.0 + RafterSpaceTop;
                double CurrentPositonBottom = -LengthBottom / 2.0 + RafterSpaceBottom;
                for (; CurrentPositonBottom <= LengthBottom / 2.0 - RafterSpaceBottom; CurrentPositonBottom += RafterSpaceBottom, CurrentPositonTop += RafterSpaceTop)
                {
                    Point3d PointTop = new Point3d(CurrentPositonTop, 0, -Height);
                    Point3d PointBottom = new Point3d(CurrentPositonBottom, -Depth, -Depth * Pitch - Height);

                    Curve TempCurve1 = (Curve)SectionBase.Duplicate();
                    TempCurve1.Translate((Vector3d)PointTop);
                    Lafters.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(TempCurve1, PointBottom - PointTop)).CapPlanarHoles(Height / 100.0));
                }
                return Lafters.ToArray();
            }

            public static Brep[] GetJapaneseRoofMain(double LengthTop, double LengthBottom, double Depth, double Pitch, double Height)
            {
                List<Brep> ResultRoof = new List<Brep>();
                Polyline RoofPL = new Polyline(5);
                RoofPL.Add(LengthTop / 2.0, 0, 0);
                RoofPL.Add(LengthBottom / 2.0, -Depth, -Depth * Pitch);
                RoofPL.Add(-LengthBottom / 2.0, -Depth, -Depth * Pitch);
                RoofPL.Add(-LengthTop / 2.0, 0, 0);
                RoofPL.Add(LengthTop / 2.0, 0, 0);

                Curve RoofCurve = RoofPL.ToNurbsCurve();
                Brep RoofPlanar = (Brep.CreatePlanarBreps(RoofCurve))[0];
                ResultRoof.Add(RoofPlanar);
                RoofPlanar = (Brep)RoofPlanar.Duplicate();
                RoofPlanar.Translate(0, 0, -Height);
                ResultRoof.Add(RoofPlanar);

                ResultRoof.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(RoofCurve, new Vector3d(0, 0, -Height))));
                return Brep.CreateSolid(ResultRoof, Height / 100.0);
            }

            public static RealObject.Building GetHandrailGlass(Curve BaseCurve)
            {
                return GetHandrailGlass(BaseCurve, 700, 100, 600, 750, 50, 25, 37.5, 10, 100, 5.0);
            }

            public static RealObject.Building GetHandrailGlass(Curve BaseCurve,double Height,double Space)
            {
                return GetHandrailGlass(BaseCurve, Height, 100, Height - 100, Space, 50, 25, 37.5, 10, 100, 5.0);
            }

            public static RealObject.Building GetHandrailGlass(Curve BaseCurve, double Height, double GlassHeightBottom, double GlassHeightTop, double Space, double GlassMarginSide, double Radius1, double Radius2, double GlassThick, double EndSpace, double FrameWidth)
            {
                EndSpace = BaseCurve.IsClosed ? 0 : EndSpace;
                List<Brep> Handrail = new List<Brep>();
                List<Brep> Glass = new List<Brep>();
                List<Brep> Frame = new List<Brep>();
                {
                    Curve TopRailCurve = (Curve)BaseCurve.Duplicate();
                    TopRailCurve.Translate(0, 0, Height);
                    Handrail.Add(Brep.CreateFromSweep(TopRailCurve, GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(Radius2)), TopRailCurve), true, Space / 1000)[0]);
                }
                double Length = BaseCurve.GetLength();
                double EndTreatment = 0.5;
                for (double CurrentLength = EndSpace; CurrentLength + Space * EndTreatment + EndSpace < Length; CurrentLength += Space)
                {
                    Point3d Point1 = BaseCurve.PointAtLength(CurrentLength);
                    Point3d Point2 = BaseCurve.PointAtLength(CurrentLength + Space);
                    if (CurrentLength + Space * (EndTreatment + 1) > Length) { Point2 = BaseCurve.PointAtLength(Length - EndSpace); }

                    Handrail.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(Point1, Radius1)), new Vector3d(0, 0, Height))).CapPlanarHoles(0));

                    double GlassLength = (Point2 - Point1).Length;
                    if (GlassLength > GlassMarginSide * 2)
                    {
                        Brep GlassTemp = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(GlassMarginSide, GlassLength - GlassMarginSide), new Interval(-GlassThick / 2.0, GlassThick / 2.0), new Interval(GlassHeightBottom, GlassHeightTop)));
                        GlassTemp = GeneralHelper.FitTwoPoint(GlassTemp, Point1, Point2);
                        Glass.Add(GlassTemp);
                        if (FrameWidth > 0)
                        {
                            {
                                Brep FrameTemp = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(0, GlassLength), new Interval(-GlassThick / 2.0, GlassThick / 2.0), new Interval(GlassHeightBottom - FrameWidth, GlassHeightBottom)));
                                FrameTemp = GeneralHelper.FitTwoPoint(FrameTemp, Point1, Point2);
                                Frame.Add(FrameTemp);
                            }
                            {
                                Brep FrameTemp = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(0, GlassLength), new Interval(-GlassThick / 2.0, GlassThick / 2.0), new Interval(GlassHeightTop, GlassHeightTop + FrameWidth)));
                                FrameTemp = GeneralHelper.FitTwoPoint(FrameTemp, Point1, Point2);
                                Frame.Add(FrameTemp);
                            }
                            {
                                Brep FrameTemp = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(GlassMarginSide - FrameWidth, GlassMarginSide), new Interval(-GlassThick / 2.0, GlassThick / 2.0), new Interval(GlassHeightBottom, GlassHeightTop)));
                                FrameTemp = GeneralHelper.FitTwoPoint(FrameTemp, Point1, Point2);
                                Frame.Add(FrameTemp);
                            }
                            {
                                Brep FrameTemp = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(GlassLength - GlassMarginSide, GlassLength - GlassMarginSide + FrameWidth), new Interval(-GlassThick / 2.0, GlassThick / 2.0), new Interval(GlassHeightBottom, GlassHeightTop)));
                                FrameTemp = GeneralHelper.FitTwoPoint(FrameTemp, Point1, Point2);
                                Frame.Add(FrameTemp);
                            }
                        }
                    }
                }
                if (!BaseCurve.IsClosed)
                {
                    Handrail.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(BaseCurve.PointAtLength(Length - EndSpace), Radius1)), new Vector3d(0, 0, Height))).CapPlanarHoles(0));
                    Handrail[0] = Handrail[0].CapPlanarHoles(0);
                }
                RealObject.Building result = new RealObject.Building("GlassHandrail");
                result.Add("Rail", Handrail.ToArray());
                result.Add("Glass", Glass.ToArray());
                result.Add("Frame", Frame.ToArray());
                return result;
            }

            public static RealObject.Building GetHandrailGlassSimple(Curve BaseCurve,double Height,double Space)
            {
                return GetHandrailGlassSimple(BaseCurve, Height, Space, 10, 10, 50);
            }

            public static RealObject.Building GetHandrailGlassSimple(Curve BaseCurve,double Height,double Space,double FrameSizeHorizontal,double FrameSizeBottom,double FrameSizeTop)
            {
                RealObject.Building Result = new RealObject.Building("HandrailGlassSimple");
                double CurveLen = BaseCurve.GetLength();
                double SpaceModified = (CurveLen - 0.01) / (int)(CurveLen / Space);

                List<Brep> Glass = new List<Brep>();
                List<Brep> Frame = new List<Brep>();

                for (int i = 0; i < (int)(CurveLen / Space); i++)
                {
                    Point3d Point1 = BaseCurve.PointAtLength(SpaceModified * i);
                    Point3d Point2 = BaseCurve.PointAtLength(SpaceModified * (i + 1));
                    double Dist = Point2.DistanceTo(Point1);
                    if(FrameSizeHorizontal!=0)
                    {
                        Polyline PL1 = new Polyline(5);
                        Polyline PL2 = new Polyline(5);
                        PL1.Add(0, 0, 0);
                        PL1.Add(Dist, 0, 0);
                        PL1.Add(Dist, 0, Height);
                        PL1.Add(0, 0, Height);
                        PL1.Add(0, 0, 0);

                        PL2.Add(FrameSizeHorizontal, 0, FrameSizeBottom);
                        PL2.Add(Dist - FrameSizeHorizontal, 0, FrameSizeBottom);
                        PL2.Add(Dist - FrameSizeHorizontal, 0, Height - FrameSizeTop);
                        PL2.Add(FrameSizeHorizontal, 0, Height - FrameSizeTop);
                        PL2.Add(FrameSizeHorizontal, 0, FrameSizeBottom);

                        Brep Temp = Brep.CreatePlanarBreps(new Curve[] { PL1.ToNurbsCurve(), PL2.ToNurbsCurve() })[0];
                        Temp = GeneralHelper.FitTwoPoint(Temp, Point1, Point2);
                        Frame.Add(Temp);
                    }
                    {
                        Brep Temp = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(FrameSizeHorizontal, Dist - FrameSizeHorizontal), new Interval(0, 5), new Interval(FrameSizeBottom, Height - FrameSizeTop)));
                        Temp = GeneralHelper.FitTwoPoint(Temp, Point1, Point2);
                        Glass.Add(Temp);
                    }
                }
                Result.Add("Glass", Glass.ToArray());
                Result.Add("Frame", Frame.ToArray());

                return Result;
            }

            public static Brep GetPipeSimple(double[] Distance, double[] Radius)
            {
                return Brep.CreateFromRevSurface(RevSurface.Create(GetHeightMap(Distance, Radius), new Line(0, 0, 0, 1, 0, 0)), false, false);
            }

            public static Brep GetPipeHalf(double[] Distance, double[] Radius)
            {
                Curve RevBase = GetHeightMap(Distance, Radius);
                return Brep.CreateFromRevSurface(RevSurface.Create(RevBase, new Line(0, 0, 0, 1, 0, 0), Math.PI * 0, Math.PI), true, true);
            }

            public static Curve GetHeightMap(double[] Distance, double[] Radius)
            {
                List<Point3d> Points = new List<Point3d>();
                for (int i = 0; i < Distance.GetLength(0); i++)
                {
                    Points.Add(new Point3d(Distance[i], Radius[i], 0));
                }
                List<Curve> intcrv = new List<Curve>();
                intcrv.Add(Curve.CreateInterpolatedCurve(Points, 3));
                if (Radius[0] > 0) intcrv.Add((new Line(Distance[0], Radius[0], 0, Distance[0], 0, 0)).ToNurbsCurve());
                if (Radius[Radius.GetLength(0) - 1] > 0) intcrv.Add((new Line(Distance[Distance.GetLength(0) - 1], Radius[Radius.GetLength(0) - 1], 0, Distance[Distance.GetLength(0) - 1], 0, 0)).ToNurbsCurve());
                return Curve.JoinCurves(intcrv)[0];
            }

            public static Brep GetPipeHead(double[] Distance, double[] Radius)
            {
                List<Point3d> Points = new List<Point3d>();
                for (int i = 0; i < Radius.GetLength(0); i++)
                {
                    Points.Add(new Point3d(Distance[i], Radius[i], 0));
                }
                Points.Add(new Point3d(Distance[Distance.GetLength(0) - 1], 0, 0));
                for (int i = Radius.GetLength(0) - 1; i >= 0; i--)
                {
                    Points.Add(new Point3d(Distance[i], -Radius[i], 0));
                }
                Curve intcrv = Curve.CreateInterpolatedCurve(Points, 3, CurveKnotStyle.Chord);
                intcrv.Domain = new Interval(0, 1);
                intcrv = intcrv.Split(0.5)[0];
                return Brep.CreateFromRevSurface(RevSurface.Create(intcrv, new Line(0, 0, 0, 1, 0, 0), 0, Math.PI * 2.0), true, true);
            }

            public static Brep[][] GetClock(RhinoDoc rd, double r)
            {
                return GetClock(rd, System.DateTime.Now, r, "Cambria");
            }

            public static Brep[][] GetClock(RhinoDoc rd, System.DateTime dt, double r, string fontname)
            {
                return GetClock(rd, dt, r, fontname, new Interval(0.75, 0.85), new Interval(0.9, 1.0), 0.02, new Interval[] { new Interval(-0.0, 0.6), new Interval(-0.1, 0.7), new Interval(-0.2, 0.75) },
                  new double[] { 0.03, 0.02, 0.01 }, 5, 2, 2, 2);
            }

            public static Brep[][] GetClock(RhinoDoc rd, System.DateTime time, double Radius, string TextFontName, Interval TextRad, Interval Frame, double CenterRad, Interval[] HandsRad, double[] HandsWidth, double BaseThick, double HandsThick, double TextThick, double GlassThick)
            {
                Brep[][] Result = new Brep[5][];

                List<Brep> Texts = new List<Brep>();
                for (int i = 1; i <= 12; i++)
                {
                    TextEntity TE = new TextEntity();
                    TE.Plane = new Plane(new Point3d(0, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1));
                    TE.Justification = TextJustification.MiddleCenter;
                    TE.Text = "" + i;
                    TE.FontIndex = rd.Fonts.FindOrCreate(TextFontName, false, false);
                    TE.TextHeight = TextRad.Length * Radius;
                    TE.Translate(Radius * TextRad.Mid * Math.Sin(Math.PI * i / 6.0), -BaseThick, Radius * TextRad.Mid * Math.Cos(Math.PI * i / 6.0));
                    Texts.AddRange(GeneralHelper.CreateExtrusionCaped(TE.Explode(), new Vector3d(0, -TextThick, 0)));
                }
                Result[0] = Texts.ToArray();

                List<Brep> Hands = new List<Brep>();
                double[] Angles = new double[] { (time.Hour + time.Minute / 60.0) * Math.PI / 6.0, (time.Minute + time.Second / 60.0) * Math.PI / 30.0, time.Second * Math.PI / 30.0 };
                for (int i = 0; i < 3; i++)
                {
                    if (HandsRad[i].Length > 0)
                    {
                        Box bx = new Box(new Plane(new Point3d(0, 0, 0), new Vector3d(Math.Sin(Angles[i]), 0, Math.Cos(Angles[i])), new Vector3d(Math.Sin(Angles[i] + Math.PI / 2.0), 0, Math.Cos(Angles[i] + Math.PI / 2.0)))
                          , new Interval(HandsRad[i].Min * Radius, HandsRad[i].Max * Radius), new Interval(-Radius * HandsWidth[i] / 2.0, Radius * HandsWidth[i] / 2.0), new Interval(-BaseThick - TextThick * 2.0 - HandsThick * (i + 1), -BaseThick - TextThick * 2.0 - HandsThick * i));
                        Hands.Add(Brep.CreateFromBox(bx));
                    }
                }
                Result[1] = Hands.ToArray();

                Curve RevCrv =
                  (Curve.JoinCurves(new Curve[]{(new Arc(new Circle(new Plane(new Point3d(Frame.Min * Radius, -BaseThick, 0), new Vector3d(0, 0, 1)), Frame.Length * Radius), new Interval(-Math.PI / 2.0, 0))).ToNurbsCurve(),
                      new Line(Frame.Min * Radius, -BaseThick, 0, Frame.Min * Radius, -BaseThick - Frame.Length * Radius, 0).ToNurbsCurve(),
                      new Line(Frame.Min * Radius, -BaseThick, 0, Frame.Max * Radius, -BaseThick, 0).ToNurbsCurve()
                  }))[0];
                Result[2] = new Brep[]{
                    Brep.CreateFromCylinder(new Cylinder(new Circle(Plane.WorldZX, Radius), -BaseThick), true, true),
                    Brep.CreateFromRevSurface(RevSurface.Create(RevCrv, new Line(0, 0, 0, 0, 1, 0)), false, false)
                };
                Result[3] = new Brep[]{
                    Brep.CreateFromCylinder(new Cylinder(new Circle(new Plane(new Point3d(0, -Frame.Length * Radius - BaseThick, 0), new Vector3d(0, 1, 0)), Radius * Frame.Min), GlassThick), true, true)
                };
                Result[4] = new Brep[]{
                    Brep.CreateFromCylinder(new Cylinder(new Circle(new Plane(new Point3d(0, -BaseThick, 0), new Vector3d(0, 1, 0)), Radius * CenterRad), -TextThick * 2.0 - HandsThick * 4.0), true, true)
                };
                return Result;
            }

            public static Curve[] GetTextCurve(string TextContent, double Height, string FontName,bool Bold,bool Italic, RhinoDoc RhinoDocument)
            {
                Rhino.Geometry.TextEntity txt = new TextEntity();
                txt.Text = TextContent;
                txt.TextHeight = Height;

                

                txt.FontIndex = RhinoDocument.Fonts.FindOrCreate(FontName, Bold, Italic);
                return txt.Explode();
            }

            public static Curve[] GetTextCurve(string TextContent,double Height) { return (new TextEntity() { Text = TextContent ,TextHeight=Height}).Explode(); }
            public static Brep[] GetTextBrep(string TextContent, double Height, double Thickness) { return GeneralHelper.CreateExtrusionCaped((new TextEntity() { Text = TextContent, TextHeight = Height }).Explode(), new Vector3d(0, 0, Thickness)); }
            public static Brep[] GetTextBrep(string TextContent, double Height,double Thickness, string FontName, bool Bold, bool Italic, RhinoDoc RhinoDocument) { return GeneralHelper.CreateExtrusionCaped(GetTextCurve(TextContent,Height,FontName,Bold,Italic,RhinoDocument), new Vector3d(0, 0, Thickness)); }

            public static Curve GetRailroadBallast(double BallastWidthTop, double BallastWidthBottom, double BallastHeight)
            {
                Polyline BallastBasePL = new Polyline(5);
                BallastBasePL.Add(BallastWidthBottom / 2.0, 0, 0);
                BallastBasePL.Add(BallastWidthTop / 2.0, 0, BallastHeight);
                BallastBasePL.Add(-BallastWidthTop / 2.0, 0, BallastHeight);
                BallastBasePL.Add(-BallastWidthBottom / 2.0, 0, 0);
                BallastBasePL.Add(BallastWidthBottom / 2.0, 0, 0);
                return BallastBasePL.ToNurbsCurve();
            }

            public static Curve GetRailroadTrackShape()
            {
                return GetRailroadTrackShape(145.0, 65.0, 49.0, 94.9, 30.1, 16.5);
            }

            public static Curve GetRailroadTrackShape(double b, double c, double d, double e, double f, double g)
            {
                Polyline BallastBasePL = new Polyline(11);
                BallastBasePL.Add(b / 2.0, 0, 0);
                BallastBasePL.Add(b / 2.0, 0, f / 2.0);
                BallastBasePL.Add(g / 2.0, 0, f);
                BallastBasePL.Add(g / 2.0, 0, f + e);
                BallastBasePL.Add(c / 2.0, 0, f + e);
                BallastBasePL.Add(c / 2.0, 0, f + e + d);

                BallastBasePL.Add(-c / 2.0, 0, f + e + d);
                BallastBasePL.Add(-c / 2.0, 0, f + e);
                BallastBasePL.Add(-g / 2.0, 0, f + e);
                BallastBasePL.Add(-g / 2.0, 0, f);
                BallastBasePL.Add(-b / 2.0, 0, f / 2.0);
                BallastBasePL.Add(-b / 2.0, 0, 0);

                BallastBasePL.Add(b / 2.0, 0, 0);
                return BallastBasePL.ToNurbsCurve();
            }

            public static RealObject.Building GetRailroad(Curve[] RailCurves)
            {
                Brep[] brep1, brep2, brep3;
                GetRailroad(RailCurves, out brep1, out brep2, out brep3);
                RealObject.Building result = new RealObject.Building("Railroad");
                RealObject.Member mem;
                mem=result.Add("Rail", brep1);
                mem.Material = new RealObject.Material("Silver");
                mem = result.Add("Tie", brep2);
                mem.Material = new RealObject.Material("Wood");
                mem = result.Add("Ballast", brep3);
                mem.Material = new RealObject.Material("Stone");
                return result;
            }

            public static void GetRailroad(Curve[] RailCurves, out Brep[] Railroad, out Brep[] RailroadTie, out Brep[] TrackBallast)
            {
                double TrackGauge = 1067.0;
                GetRailroad(RailCurves, GetRailroadTrackShape(), TrackGauge, out Railroad, out RailroadTie, out TrackBallast);
            }

            public static void GetRailroad(Curve[] RailCurves, Curve TrackShape, double TrackGauge, out Brep[] Railroad, out Brep[] RailroadTie, out Brep[] TrackBallast)
            {
                GetRailroad(RailCurves, TrackShape, TrackGauge, TrackGauge * 2.0, 200, 50, 700, TrackGauge * 2.5, TrackGauge * 3.0, 140, out Railroad, out RailroadTie, out TrackBallast);
            }

            public static void GetRailroad(Curve[] RailCurves, Curve TrackShape, double TrackGauge, double TieWidth, double TieLength, double TieHeight, double TieSpace, double BallastWidthTop, double BallastWidthBottom, double BallastHeight, out Brep[] Railroad, out Brep[] RailroadTie, out Brep[] TrackBallast)
            {
                TrackGauge = Math.Abs(TrackGauge);
                List<Brep> ResultRoad = new List<Brep>();
                List<Brep> ResultTie = new List<Brep>();
                List<Brep> ResultBallast = new List<Brep>();

                Brep TieBase = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(-TieWidth / 2.0, TieWidth / 2.0), new Interval(-TieLength / 2.0, TieLength / 2.0), new Interval(0, TieHeight)));

                Curve BallastBase = GetRailroadBallast(BallastWidthTop, BallastWidthBottom, BallastHeight);

                foreach (Curve RailCurve in RailCurves)
                {
                    RailCurve.Domain = new Interval(0, 1.0);
                    double RailLength = RailCurve.GetLength();

                    Curve[] OffsetedCurves = new Curve[]{
        (RailCurve.Offset(Plane.WorldXY, TrackGauge / 2.0, TrackGauge / 100.0, CurveOffsetCornerStyle.Smooth))[0],
        (RailCurve.Offset(Plane.WorldXY, -TrackGauge / 2.0, TrackGauge / 100.0, CurveOffsetCornerStyle.Smooth))[0]};
                    OffsetedCurves[0].Translate(0, 0, BallastHeight + TieHeight);
                    OffsetedCurves[1].Translate(0, 0, BallastHeight + TieHeight);

                    for (int i = 0; i <= 1; i++)
                    {
                        Curve TempTrackShape = (Curve)TrackShape.Duplicate();
                        OffsetedCurves[i].Domain = new Interval(0, 1);
                        TempTrackShape.Rotate(GeneralHelper.GetCurvatureAsAngle(OffsetedCurves[i], 0), new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                        TempTrackShape.Translate((Vector3d)OffsetedCurves[i].PointAtStart);
                        ResultRoad.Add(Brep.CreateFromSweep(OffsetedCurves[i], TempTrackShape, true, TrackGauge / 1e6)[0].CapPlanarHoles(TrackGauge / 1e6));
                    }

                    if (TieHeight > 0 && TieWidth > 0)
                    {
                        for (double CurrentLength = TieSpace; CurrentLength < RailLength; CurrentLength += TieSpace)
                        {
                            double TempT;
                            RailCurve.LengthParameter(CurrentLength, out TempT);
                            Brep TempTie = (Brep)TieBase.Duplicate();
                            TempTie.Rotate(GeneralHelper.GetCurvatureAsAngle(RailCurve, TempT), new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                            TempTie.Translate((Vector3d)(RailCurve.PointAt(TempT)) + new Vector3d(0, 0, BallastHeight));
                            ResultTie.Add(TempTie);
                        }
                    }
                    if (BallastHeight > 0)
                    {
                        Curve TempBallaseBase = (Curve)BallastBase.Duplicate();
                        TempBallaseBase.Rotate(GeneralHelper.GetCurvatureAsAngle(RailCurve, 0), new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                        TempBallaseBase.Translate((Vector3d)RailCurve.PointAtStart);
                        ResultBallast.Add(Brep.CreateFromSweep(RailCurve, TempBallaseBase, true, TrackGauge / 1e6)[0].CapPlanarHoles(TrackGauge / 1e6));
                    }
                }
                Railroad = ResultRoad.ToArray();
                RailroadTie = ResultTie.ToArray();
                TrackBallast = ResultBallast.ToArray();
            }

            public static void GetTowerClane(double AngleClane,double AngleHead, out Brep[] Basement, out Brep[] LadderBasement, out Brep[] LiftFrame, out Brep[] RotationUnit,
                out Brep[] HeadHouseWall, out Brep[] HeadHouseGlass, out Brep[] HeadBody, out Brep[] HeadHandrail, out Brep[] HeadClaneBodyRed, out Brep[] HeadClaneBodyWhite, out Brep[] HeadClaneLadderRed, out Brep[] HeadClaneLadderWhite,
                out Brep[] HeadPole, out Brep[] HeadPoleWorkSpace, out Brep[] HeadClaneWorkSpace, out Brep[] HeadClaneWire)
            {
                Basement = GetTowerClaneBasement(20);
                LadderBasement = GeneralHelper.TranslateBreps(GetLadderSimple(20 * 800), new Point3d(0, 400, 0));
                LiftFrame = GeneralHelper.TranslateBreps(GetTowerClaneLiftFrame(1200, 2200), new Vector3d(0, 0, 20 * 800));
                RotationUnit = GeneralHelper.TranslateBreps(GetTowerClaneRotationUnit(600, 2300, 150, 2500), new Vector3d(0, 0, 20 * 800));

                Brep[] RetWall;
                Brep[] RetGlass;
                Brep[] RetBody;
                Brep[] RetHandrail;
                Brep[] RetPole;
                Brep[] ClaneWorkSpace;

                Brep[] RedBody;
                Brep[] WhiteBody;
                Brep[] ClaneLadderRed;
                Brep[] ClaneLadderWhite;
                Brep[] ClaneClaneWorkSpace;
                Brep[] Wire;
                
                GetTowerClaneHead(out RetBody, out RetHandrail, out RetWall, out RetGlass, out RetPole, out ClaneWorkSpace, AngleClane, out RedBody, out WhiteBody, out ClaneLadderRed, out ClaneLadderWhite, out ClaneClaneWorkSpace, 20000, out Wire);

                HeadHouseWall = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(RetWall, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadHouseGlass = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(RetGlass, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadBody = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(RetBody, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadHandrail = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(RetHandrail, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadPole = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(RetPole, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadPoleWorkSpace = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(ClaneWorkSpace, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));

                HeadClaneBodyRed = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(RedBody, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadClaneBodyWhite = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(WhiteBody, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadClaneLadderRed = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(ClaneLadderRed, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadClaneLadderWhite = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(ClaneLadderWhite, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadClaneWorkSpace = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(ClaneClaneWorkSpace, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
                HeadClaneWire = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(Wire, new Point3d(0, 0, 20 * 800 + 600)), AngleHead, new Vector3d(0, 0, 1));
            }

            public static void GetTowerClaneClane(int Count, double Angle, double Width1, double Width2, double Length, double Space, double Radius1, double Radius2, out Brep[] Body1, out Brep[] Body2, out Brep[] Ladder1, out Brep[] Ladder2, out Brep[] WorkSpace)
            {
                Angle = -Angle;
                Count = Math.Max(Count, 2);
                List<List<Brep>> ResultBody = new List<List<Brep>>();
                ResultBody.Add(new List<Brep>());
                ResultBody.Add(new List<Brep>());
                ResultBody.Add(new List<Brep>());
                ResultBody.Add(new List<Brep>());

                ResultBody[0].AddRange(GeneralHelper.RotateBreps(GetTowerClaneClaneUnit(Width1, Width2, Length, Space, Radius1, Radius2), Angle, new Vector3d(0, 1, 0)));
                for (int i = 1; i + 1 < Count; i++)
                {
                    ResultBody[i % 2].AddRange(GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(GetTowerClaneClaneUnit(Width2, Width2, Length, Space, Radius1, Radius2), new Vector3d(i * Length, 0, 0)), Angle, new Vector3d(0, 1, 0)));
                }
                Brep[] LadderBase = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(GeneralHelper.RotateBreps(GetLadderSimple(Length), Math.PI / 2.0, new Vector3d(0, 0, 1)), new Vector3d(Width1 / 2.0, 0, 0)), Math.PI / 2.0, new Vector3d(0, 1, 0));
                for (int i = 0; i < Count; i++)
                {
                    ResultBody[i % 2 + 2].AddRange(GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(GeneralHelper.DuplicateBreps(LadderBase), new Vector3d(i * Length, 0, 0)), Angle, new Vector3d(0, 1, 0)));
                }
                {
                    int i = Count - 1;
                    ResultBody[i % 2].AddRange(GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(GetTowerClaneClaneUnit(Width2, Width1, Length, Space, Radius1, Radius2), new Vector3d(i * Length, 0, 0)), Angle, new Vector3d(0, 1, 0)));
                }
                {
                    Brep[] WorkSpaceTemp = GetWorkSpace(Width1 * 2, Width1 * 2, 100);
                    double WSAngle = Math.PI / 4.0;
                    WorkSpace = GeneralHelper.RotateBreps(GeneralHelper.TranslateBreps(GeneralHelper.RotateBreps(WorkSpaceTemp, WSAngle, new Vector3d(0, 1, 0)), new Vector3d(Length * Count + Width1 * Math.Sin(WSAngle), 0, -Width1)), Angle, new Vector3d(0, 1, 0));
                }

                Body1 = ResultBody[0].ToArray();
                Body2 = ResultBody[1].ToArray();
                Ladder1 = ResultBody[2].ToArray();
                Ladder2 = ResultBody[3].ToArray();
            }



            public static Brep[] GetTowerClaneClaneUnit(double Width1, double Width2, double Length, double Space, double Radius1, double Radius2)
            {
                List<Brep> Result = new List<Brep>();
                Polyline PartsPL = new Polyline(2);
                PartsPL.Add(0, Width1 / 2.0, Width1 / 2.0);
                PartsPL.Add(Length, Width2 / 2.0, Width2 / 2.0);
                Brep BaseParts1 = (Brep.CreateFromSweep(PartsPL.ToNurbsCurve(), GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(Radius1)), PartsPL.ToNurbsCurve()), true, 1.0)[0]);
                for (int i = 0; i < 4; i++)
                {
                    Brep TempParts1 = (Brep)BaseParts1.Duplicate();
                    TempParts1.Rotate(Math.PI / 2.0 * i, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
                    Result.Add(TempParts1);
                }
                for (double CurrentLength = 0; CurrentLength < Length; CurrentLength += Space)
                {
                    PartsPL = new Polyline(2);
                    PartsPL.Add(CurrentLength, (Width1 + (Width2 - Width1) * (CurrentLength / Length)) / 2.0, (Width1 + (Width2 - Width1) * (CurrentLength / Length)) / 2.0);
                    PartsPL.Add((CurrentLength + Space / 2.0), -(Width1 + (Width2 - Width1) * ((CurrentLength + Space / 2.0) / Length)) / 2.0, (Width1 + (Width2 - Width1) * ((CurrentLength + Space / 2.0) / Length)) / 2.0);
                    Brep BaseParts2 = (Brep.CreateFromSweep(PartsPL.ToNurbsCurve(), GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(Radius1)), PartsPL.ToNurbsCurve()), true, 1.0)[0]);

                    PartsPL = new Polyline(2);
                    PartsPL.Add((CurrentLength + Space / 2.0), -(Width1 + (Width2 - Width1) * ((CurrentLength + Space / 2.0) / Length)) / 2.0, (Width1 + (Width2 - Width1) * ((CurrentLength + Space / 2.0) / Length)) / 2.0);
                    PartsPL.Add((CurrentLength + Space), (Width1 + (Width2 - Width1) * ((CurrentLength + Space) / Length)) / 2.0, (Width1 + (Width2 - Width1) * ((CurrentLength + Space) / Length)) / 2.0);
                    Brep BaseParts3 = (Brep.CreateFromSweep(PartsPL.ToNurbsCurve(), GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(Radius1)), PartsPL.ToNurbsCurve()), true, 1.0)[0]);
                    for (int i = 0; i < 4; i++)
                    {
                        Brep TempParts2 = (Brep)BaseParts2.Duplicate();
                        TempParts2.Rotate(Math.PI / 2.0 * i, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
                        Result.Add(TempParts2);

                        Brep TempParts3 = (Brep)BaseParts3.Duplicate();
                        TempParts3.Rotate(Math.PI / 2.0 * i, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
                        Result.Add(TempParts3);
                    }
                }
                return Result.ToArray();
            }

            public static void GetTowerClaneCableRoller(double Width, double CableRadius, double Radius1, double Radius2, double Thickness, out Brep[] Body, out Brep[] Cable, out Point3d CableTarget)
            {
                Body = new Brep[]{Brep.CreateFromCylinder(new Cylinder(new Circle(new Plane(new Point3d(0, -Width / 2.0, Radius2), new Vector3d(0, 1, 0)), Radius2), Thickness), true, true),
      Brep.CreateFromCylinder(new Cylinder(new Circle(new Plane(new Point3d(0, Width / 2.0, Radius2), new Vector3d(0, 1, 0)), Radius2), -Thickness), true, true)};

                List<Curve> Arcs = new List<Curve>();
                Curve BaseArc = NurbsCurve.CreateFromArc(new Arc(new Circle(new Plane(new Point3d(0, 0, 0), new Vector3d(1, 0, 0)), CableRadius), new Interval(0, Math.PI)));
                for (double CurrentLength = -Width / 2.0 + Thickness + (Width / 2.0 - Thickness) % (CableRadius * 2); CurrentLength < Width / 2.0 - Thickness; CurrentLength += CableRadius)
                {
                    Curve TempArc = (Curve)BaseArc.Duplicate();
                    TempArc.Translate(new Vector3d(0, CurrentLength, Radius1));
                    Arcs.Add(TempArc);
                }
                Brep TempCable = Brep.CreateFromRevSurface(RevSurface.Create(Curve.JoinCurves(Arcs)[0], new Line(new Point3d(0, 0, 0), new Point3d(0, 1, 0))), false, false);
                TempCable.Translate(0, 0, Radius2);
                Cable = new Brep[] { TempCable };

                CableTarget = new Point3d(Radius1 * Math.Cos(Math.PI * 3.0 / 4.0), 0, Radius1 * Math.Sin(Math.PI * 3.0 / 4.0) + Radius2);
            }

            public static void GetTowerClaneCableRollerLight(double Width, double CableRadius, double Radius1, double Radius2, double Thickness, out Brep[] Body, out Brep[] Cable, out Point3d CableTarget)
            {
                Body = new Brep[]{Brep.CreateFromCylinder(new Cylinder(new Circle(new Plane(new Point3d(0, -Width / 2.0, Radius2), new Vector3d(0, 1, 0)), Radius2), Thickness), true, true),
      Brep.CreateFromCylinder(new Cylinder(new Circle(new Plane(new Point3d(0, Width / 2.0, Radius2), new Vector3d(0, 1, 0)), Radius2), -Thickness), true, true)};
                Cable = new Brep[] { Brep.CreateFromCylinder(new Cylinder(new Circle(new Plane(new Point3d(0, -Width / 2.0 + Thickness, Radius2), new Vector3d(0, 1, 0)), Radius1), Width - Thickness * 2), false, false) };
                CableTarget = new Point3d(Radius1 * Math.Cos(Math.PI * 3.0 / 4.0), 0, Radius1 * Math.Sin(Math.PI * 3.0 / 4.0) + Radius2);
            }

            public static Brep[] GetWorkSpace(double Width, double Length, double Thickness)
            {
                List<Brep> Result = new List<Brep>();
                Result.Add(Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(-Length / 2.0, Length / 2.0), new Interval(-Width / 2.0, Width / 2.0), new Interval(-Thickness, 0))));

                Polyline HandrailBase = new Polyline();
                double HRRadius = 25;
                HandrailBase.Add(Length / 2.0 - HRRadius * 2, Width / 2.0 - HRRadius * 2, 0);
                HandrailBase.Add(Length / 2.0 - HRRadius * 2, -Width / 2.0 + HRRadius * 2, 0);
                HandrailBase.Add(-Length / 2.0 + HRRadius * 2, -Width / 2.0 + HRRadius * 2, 0);
                HandrailBase.Add(-Length / 2.0 + HRRadius * 2, Width / 2.0 - HRRadius * 2, 0);
                HandrailBase.Add(Length / 2.0 - HRRadius * 2, Width / 2.0 - HRRadius * 2, 0);
                Result.AddRange(GetHandrailSimple(HandrailBase.ToNurbsCurve(), 800, new double[] { 400, 600 }, 500, HRRadius, 15, 10));

                return Result.ToArray();
            }

            public static Brep[] GetTowerClanePole(double Width, double Length, double Height, double Size1, double Size2, double Radius, double Space)
            {
                List<Brep> Result = new List<Brep>();
                {
                    Polyline BasePoly = new Polyline(5);
                    BasePoly.Add(0, -Width / 2.0, 0);
                    BasePoly.Add(Size1, -Width / 2.0, 0);
                    BasePoly.Add(Size1, -Width / 2.0 + Size1, 0);
                    BasePoly.Add(0, -Width / 2.0 + Size1, 0);
                    BasePoly.Add(0, -Width / 2.0, 0);
                    Brep TempBrep = (Brep.CreateFromSurface(Surface.CreateExtrusion(BasePoly.ToNurbsCurve(), new Vector3d(0, 0, Height))).CapPlanarHoles(0));
                    Result.Add(TempBrep);
                    TempBrep = (Brep)TempBrep.Duplicate();
                    TempBrep.Translate(0, Width - Size1, 0);
                    Result.Add(TempBrep);
                }
                {
                    Polyline BasePoly = new Polyline(5);
                    BasePoly.Add(Length, -Width / 2.0, 0);
                    BasePoly.Add(Length - Size2, -Width / 2.0, 0);
                    BasePoly.Add(Length - Size2, -Width / 2.0 + Size2, 0);
                    BasePoly.Add(Length, -Width / 2.0 + Size2, 0);
                    BasePoly.Add(Length, -Width / 2.0, 0);
                    Brep TempBrep = (Brep.CreateFromSurface(Surface.CreateExtrusion(BasePoly.ToNurbsCurve(), new Vector3d(-Length + Size1, 0, Height))).CapPlanarHoles(0));
                    Result.Add(TempBrep);
                    TempBrep = (Brep)TempBrep.Duplicate();
                    TempBrep.Translate(0, Width - Size2, 0);
                    Result.Add(TempBrep);
                }
                for (double CrtLen = Space; CrtLen < Height; CrtLen += Space)
                {
                    Result.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(new Plane(new Point3d(Size1 / 2.0, -Width / 2.0 + Size1, CrtLen), new Vector3d(0, 1, 0)), Radius))
                      , new Vector3d(0, Width - Size1 * 2, 0))));
                }

                return Result.ToArray();
            }

            public static void GetTowerClaneHead(out Brep[] Body, out Brep[] Handrail, out Brep[] Wall, out Brep[] Glass, out Brep[] Pole
            , out Brep[] WorkSpace, double Angle, out Brep[] RedBody, out Brep[] WhiteBody, out Brep[] ClaneLadderRed, out Brep[] ClaneLadderWhite, out Brep[] ClaneWorkSpace, double WireLength, out Brep[] Wire)
            {

                GetTowerClaneHeadBase(4.5e3, 9e3, 500, out Body, out Handrail, out Wall, out Glass);
                Pole = GeneralHelper.TranslateBreps(GetTowerClanePole(4.5e3 / 3.0 + 500, 9e3 * 2 / 3, 10e3, 400, 400, 80, 2000), new Vector3d(-9e3 / 2.0, 0, 500));
                WorkSpace = GeneralHelper.TranslateBreps(GetWorkSpace(4.5e3 / 3.0 + 1e3, 1e3, 100), new Vector3d(-9e3 / 2.0 - (1e3) / 2.0 + 400, 0, 500 + 10e3 + 100));
                {
                    Brep[] ResultClane1;
                    Brep[] ResultClane2;
                    Brep[] ResultClane3;
                    Brep[] ResultClane4;
                    Brep[] ResultClane5;

                    GetTowerClaneClane(5, Angle, 500, 1000, 5000, 1000, 25, 10, out ResultClane1, out ResultClane2, out ResultClane3, out ResultClane4, out ResultClane5);
                    RedBody = GeneralHelper.TranslateBreps(ResultClane1, new Vector3d(0, 0, 500 + 500));
                    WhiteBody = GeneralHelper.TranslateBreps(ResultClane2, new Vector3d(0, 0, 500 + 500));
                    ClaneLadderRed = GeneralHelper.TranslateBreps(ResultClane3, new Vector3d(0, 0, 500 + 500));
                    ClaneLadderWhite = GeneralHelper.TranslateBreps(ResultClane4, new Vector3d(0, 0, 500 + 500));
                    ClaneWorkSpace = GeneralHelper.TranslateBreps(ResultClane5, new Vector3d(0, 0, 500 + 500));
                }
                {
                    double WireRadius = 25;
                    List<Brep> ResultWire = new List<Brep>();
                    {
                        Polyline WirePL = new Polyline();
                        WirePL.Add((-9e3 / 2.0 - 1e3 / 2.0 + 400) + 1e3 / 2.0, 0, 500 + 10e3 + 100 + 400);
                        WirePL.Add(5 * 5000 * Math.Cos(Angle), 0, 500 + 500 + 5 * 5000 * Math.Sin(Angle));

                        Curve RailCurve = WirePL.ToNurbsCurve();
                        RailCurve.Rotate(-Math.PI / 2.0, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
                        Brep WireBase = (Brep.CreateFromSweep(RailCurve, GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(WireRadius)), RailCurve), true, 0.1)[0]).CapPlanarHoles(0);
                        WireBase.Rotate(Math.PI / 2.0, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));

                        Brep WireTemp = (Brep)WireBase.Duplicate();
                        WireTemp.Translate(0, (500 / 2.0), 0);
                        ResultWire.Add(WireTemp);

                        WireTemp = (Brep)WireBase.Duplicate();
                        WireTemp.Translate(0, -(500 / 2.0), 0);
                        ResultWire.Add(WireTemp);
                    }
                    {
                        Polyline WirePL = new Polyline();
                        WirePL.Add((-9e3 / 2.0 - 1e3 / 2.0 + 400) - 1e3 / 2.0 - 1e3, 0, 500);
                        WirePL.Add((-9e3 / 2.0 - 1e3 / 2.0 + 400) - 1e3 / 2.0, 0, 500 + 10e3 + 100 + 400);
                        WirePL.Add((-9e3 / 2.0 - 1e3 / 2.0 + 400) + 1e3 / 2.0, 0, 500 + 10e3 + 100 + 400);
                        WirePL.Add(5 * 5000 * Math.Cos(Angle), 0, 500 + 500 + 5 * 5000 * Math.Sin(Angle));
                        WirePL.Add(5 * 5000 * Math.Cos(Angle), 0, 500 + 500 + 5 * 5000 * Math.Sin(Angle) - WireLength);

                        Curve RailCurve = WirePL.ToNurbsCurve();
                        RailCurve.Rotate(-Math.PI / 2.0, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
                        Brep WireBase = (Brep.CreateFromSweep(RailCurve, GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(WireRadius)), RailCurve), true, 0.1)[0]).CapPlanarHoles(0);
                        WireBase.Rotate(Math.PI / 2.0, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));

                        Brep WireTemp = (Brep)WireBase.Duplicate();
                        WireTemp.Translate(0, (500 / 2.0) / 3.0, 0);
                        ResultWire.Add(WireTemp);

                        WireTemp = (Brep)WireBase.Duplicate();
                        WireTemp.Translate(0, -(500 / 2.0) / 3.0, 0);
                        ResultWire.Add(WireTemp);
                    }
                    Wire = ResultWire.ToArray();
                }
            }


            public static void GetTowerClaneHeadBase(double Width, double Length, double Thickness, out Brep[] Body, out Brep[] Handrail, out Brep[] Wall, out Brep[] Glass)
            {
                GetTowerClaneControlUnit(Width / 3.0, Length / 2.0, Length / 2.0 + 500, 1.0e3, 2.5e3, 1.0e3, 150, out Wall, out Glass);
                Wall = GeneralHelper.TranslateBreps(Wall, new Point3d(-Length / 4.0, -Width / 2.0 - 500, Thickness));
                Glass = GeneralHelper.TranslateBreps(Glass, new Point3d(-Length / 4.0, -Width / 2.0 - 500, Thickness));
                Body = new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(-Length * 3.0 / 4.0, Length / 4.0), new Interval(-Width / 2.0, Width / 2.0), new Interval(0, Thickness))) };

                Polyline HandrailBase = new Polyline();
                double HRRadius = 25;
                HandrailBase.Add(-Length / 4.0 - HRRadius * 2, -Width / 2.0 + HRRadius * 2, 0);
                HandrailBase.Add(-Length * 3.0 / 4.0 + HRRadius * 2, -Width / 2.0 + HRRadius * 2, 0);
                HandrailBase.Add(-Length * 3.0 / 4.0 + HRRadius * 2, +Width / 2.0 - HRRadius * 2, 0);
                HandrailBase.Add(Length / 4.0 - HRRadius * 2, +Width / 2.0 - HRRadius * 2, 0);
                Handrail = GeneralHelper.TranslateBreps(GetHandrailSimple(HandrailBase.ToNurbsCurve(), 800, new double[] { 400, 600 }, 500, HRRadius, 15, 10), new Vector3d(0, 0, Thickness));
            }

            public static void GetTowerClaneControlUnit(out Brep[] Wall, out Brep[] Glass)
            {
                GetTowerClaneControlUnit(1.5e3, 5e3, 5.5e3, 1.0e3, 2.5e3, 1.0e3, 150, out Wall, out Glass);
            }

            public static void GetTowerClaneControlUnit(double Width, double BottomLength, double TopLength, double SideWindowLength, double Height, double BarHeight, double FrameWidth, out Brep[] Wall, out Brep[] Glass)
            {
                double WallThickness = 50.0;
                List<Brep> RetWall = new List<Brep>();
                {
                    Polyline Parts1PL = new Polyline(13);
                    Parts1PL.Add(0, 0, 0);
                    Parts1PL.Add(BottomLength, 0, 0);
                    Parts1PL.Add(BottomLength, 0, FrameWidth);
                    Parts1PL.Add(BottomLength - SideWindowLength, 0, FrameWidth);
                    Parts1PL.Add(BottomLength - SideWindowLength, 0, BarHeight);
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * BarHeight / Height, 0, BarHeight);
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * (BarHeight + FrameWidth) / Height, 0, (BarHeight + FrameWidth));
                    Parts1PL.Add(BottomLength - SideWindowLength, 0, (BarHeight + FrameWidth));
                    Parts1PL.Add(BottomLength - SideWindowLength, 0, (Height - FrameWidth));
                    Parts1PL.Add(TopLength - (TopLength - BottomLength) * FrameWidth / Height, 0, (Height - FrameWidth));
                    Parts1PL.Add(TopLength, 0, Height);
                    Parts1PL.Add(0, 0, Height);
                    Parts1PL.Add(0, 0, 0);
                    Curve Part1Curve = Parts1PL.ToNurbsCurve();
                    RetWall.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, -WallThickness, 0))).CapPlanarHoles(0));
                    Part1Curve.Translate(0, Width, 0);
                    RetWall.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, WallThickness, 0))).CapPlanarHoles(0));
                }
                {
                    Polyline Parts1PL = new Polyline(5);
                    Parts1PL.Add(BottomLength, 0, 0);
                    Parts1PL.Add(TopLength, 0, Height);
                    Parts1PL.Add(TopLength - FrameWidth, 0, Height);
                    Parts1PL.Add(BottomLength - FrameWidth, 0, 0);
                    Parts1PL.Add(BottomLength, 0, 0);
                    Curve Part1Curve = Parts1PL.ToNurbsCurve();
                    RetWall.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, -WallThickness, 0))).CapPlanarHoles(0));
                    Part1Curve.Translate(0, Width, 0);
                    RetWall.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, WallThickness, 0))).CapPlanarHoles(0));
                    Part1Curve.Translate(0, -Width / 2.0 - FrameWidth / 2.0, 0);
                    RetWall.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, FrameWidth, 0))).CapPlanarHoles(0));
                }
                {
                    Polyline Parts1PL = new Polyline(5);
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * BarHeight / Height, 0, BarHeight);
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * (BarHeight + FrameWidth) / Height, 0, (BarHeight + FrameWidth));
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * (BarHeight + FrameWidth) / Height - WallThickness, 0, (BarHeight + FrameWidth));
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * BarHeight / Height - WallThickness, 0, BarHeight);
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * BarHeight / Height, 0, BarHeight);
                    Curve Part1Curve = Parts1PL.ToNurbsCurve();
                    Part1Curve.Translate(0, -WallThickness / 2.0, 0);
                    RetWall.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, Width + WallThickness, 0))).CapPlanarHoles(0));
                }
                {
                    Polyline Parts1PL = new Polyline(13);
                    Parts1PL.Add(0, 0, 0);
                    Parts1PL.Add(BottomLength, 0, 0);
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * FrameWidth / Height, 0, FrameWidth);
                    Parts1PL.Add(BottomLength + (TopLength - BottomLength) * FrameWidth / Height - WallThickness, 0, FrameWidth);
                    Parts1PL.Add(BottomLength - WallThickness, 0, WallThickness);
                    Parts1PL.Add(WallThickness, 0, WallThickness);
                    Parts1PL.Add(WallThickness, 0, Height - WallThickness);
                    Parts1PL.Add(TopLength - WallThickness, 0, Height - WallThickness);
                    Parts1PL.Add(TopLength - (TopLength - BottomLength) * FrameWidth / Height - WallThickness, 0, (Height - FrameWidth));
                    Parts1PL.Add(TopLength - (TopLength - BottomLength) * FrameWidth / Height, 0, (Height - FrameWidth));
                    Parts1PL.Add(TopLength, 0, Height);
                    Parts1PL.Add(0, 0, Height);
                    Parts1PL.Add(0, 0, 0);
                    Curve Part1Curve = Parts1PL.ToNurbsCurve();
                    Part1Curve.Translate(0, -WallThickness / 2.0, 0);
                    RetWall.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, Width + WallThickness, 0))).CapPlanarHoles(0));
                }
                List<Brep> RetGlass = new List<Brep>();
                {
                    Polyline Parts1PL = new Polyline(5);
                    Parts1PL.Add(BottomLength - WallThickness, 0, WallThickness);
                    Parts1PL.Add(TopLength - WallThickness, 0, Height - WallThickness);
                    Parts1PL.Add(TopLength - WallThickness * 2, 0, Height - WallThickness);
                    Parts1PL.Add(BottomLength - WallThickness * 2, 0, WallThickness);
                    Parts1PL.Add(BottomLength - WallThickness, 0, WallThickness);
                    Curve Part1Curve = Parts1PL.ToNurbsCurve();
                    RetGlass.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, Width, 0))).CapPlanarHoles(0));
                }
                {
                    Polyline Parts1PL = new Polyline(5);
                    Parts1PL.Add(BottomLength - WallThickness, 0, WallThickness);
                    Parts1PL.Add(TopLength - WallThickness, 0, Height - WallThickness);
                    Parts1PL.Add(BottomLength - WallThickness - SideWindowLength, 0, Height - WallThickness);
                    Parts1PL.Add(BottomLength - WallThickness - SideWindowLength, 0, WallThickness);
                    Parts1PL.Add(BottomLength - WallThickness, 0, WallThickness);
                    Curve Part1Curve = Parts1PL.ToNurbsCurve();
                    RetGlass.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, WallThickness, 0))).CapPlanarHoles(0));
                    Part1Curve.Translate(0, Width, 0);
                    RetGlass.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Part1Curve, new Vector3d(0, -WallThickness, 0))).CapPlanarHoles(0));
                }
                Wall = RetWall.ToArray();
                Glass = RetGlass.ToArray();
            }

            public static Brep[] GetHandrailSimple(Curve BaseCurve, double Height, double[] RailHeight, double Space, double Radius1, double Radius2, double Radius3)
            {
                List<Brep> Result = new List<Brep>();
                {
                    Curve TopRailCurve = (Curve)BaseCurve.Duplicate();
                    TopRailCurve.Translate(0, 0, Height);
                    Result.Add(Brep.CreateFromSweep(TopRailCurve, GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(Radius2)), TopRailCurve), true, Space / 1000)[0]);
                }
                foreach (double TempHeight in RailHeight)
                {
                    Curve TopRailCurve = (Curve)BaseCurve.Duplicate();
                    TopRailCurve.Translate(0, 0, TempHeight);
                    Result.Add(Brep.CreateFromSweep(TopRailCurve, GeneralHelper.GetCurveForSweep(NurbsCurve.CreateFromCircle(new Circle(Radius3)), TopRailCurve), true, Space / 1000)[0]);
                }
                double Length = BaseCurve.GetLength();
                for (double CurrentLength = 0; CurrentLength < Length; CurrentLength += Space)
                {
                    Result.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(BaseCurve.PointAtLength(CurrentLength), Radius1)), new Vector3d(0, 0, Height))).CapPlanarHoles(0));
                }
                if (!BaseCurve.IsClosed)
                {
                    Result.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(BaseCurve.PointAtLength(Length), Radius1)), new Vector3d(0, 0, Height))).CapPlanarHoles(0));
                    Result[0] = Result[0].CapPlanarHoles(0);
                    for (int i = 0; i < RailHeight.GetLength(0); i++)
                    {
                        Result[1 + i] = Result[1 + i].CapPlanarHoles(0);
                    }
                }

                return Result.ToArray();
            }

            public static Brep[] GetTowerClaneRotationUnit(double Height1, double Radius1, double Height2, double Radius2)
            {
                Brep Cylinder2 = Brep.CreateFromCylinder(new Cylinder(new Circle(Radius2 / 2.0), Height2), true, true);
                Cylinder2.Translate(0, 0, Height1 - Height2);
                return new Brep[]{
      Brep.CreateFromCylinder(new Cylinder(new Circle(Radius1 / 2.0), Height1), true, false),Cylinder2
      };
            }

            public static Brep[] GetTowerClaneLiftFrame(double Size1, double Size2)
            {
                Brep[] Parts1;
                Brep[] Parts2;
                Brep[] Parts3;
                GetTowerClaneLiftFrame(Size1, Size2, out Parts1, out Parts2, out  Parts3);
                List<Brep> Result = new List<Brep>();
                Result.AddRange(Parts1);
                Result.AddRange(Parts2);
                Result.AddRange(Parts3);
                return Result.ToArray();
            }

            public static void GetTowerClaneLiftFrame(double Size1, double Size2, out Brep[] Parts1, out Brep[] Parts2, out Brep[] Parts3)
            {
                double UnitHeight = Size1 * 0.8;
                int UnitCount = 4;
                double TotalHeight = UnitHeight * UnitCount;
                double Elevation = 200;

                Parts1 = GeneralHelper.TranslateBreps(GetTowerClaneBasement(UnitCount, Size1, UnitHeight, 30, 150, 100), new Vector3d(0, 0, -TotalHeight - Elevation));
                double HandrailRadius = 25.0;
                Brep[] HandrailBase = GetHandrailSimple((new Rhino.Geometry.Rectangle3d(Plane.WorldXY, new Interval(-Size2 / 2.0 + HandrailRadius, Size2 / 2.0 - HandrailRadius), new Interval(-Size2 / 2.0 + HandrailRadius, Size2 / 2.0 - HandrailRadius))
                  ).ToNurbsCurve(), 800, new double[] { 500 }, 500, HandrailRadius, 15, 10);
                List<Brep> RetParts2 = new List<Brep>();
                RetParts2.AddRange(GeneralHelper.TranslateBreps(HandrailBase, new Vector3d(0, 0, -TotalHeight - Elevation)));
                RetParts2.AddRange(GeneralHelper.TranslateBreps(HandrailBase, new Vector3d(0, 0, -TotalHeight / 2.0 - Elevation)));
                Parts2 = RetParts2.ToArray();

                List<Brep> RetParts3 = new List<Brep>();

                Brep[] FloorBase = new Brep[4];
                {
                    double Size3 = Size1 + 30 * 2;
                    double Parts3Width = 10.0;
                    double Parts3Bending = 50.0;
                    Polyline Parts3PL = new Polyline(9);
                    Parts3PL.Add(-Size2 / 2.0, -Size2 / 2.0, 0);
                    Parts3PL.Add(-Size2 / 2.0, -Size3 / 2.0, 0);
                    Parts3PL.Add(-Size2 / 2.0, -Size3 / 2.0, -Parts3Bending);
                    Parts3PL.Add(-Size2 / 2.0, -Size3 / 2.0 - Parts3Width, -Parts3Bending);
                    Parts3PL.Add(-Size2 / 2.0, -Size3 / 2.0 - Parts3Width, -Parts3Width);
                    Parts3PL.Add(-Size2 / 2.0, -Size2 / 2.0 + Parts3Width, -Parts3Width);
                    Parts3PL.Add(-Size2 / 2.0, -Size2 / 2.0 + Parts3Width, -Parts3Bending);
                    Parts3PL.Add(-Size2 / 2.0, -Size2 / 2.0, -Parts3Bending);
                    Parts3PL.Add(-Size2 / 2.0, -Size2 / 2.0, 0);
                    Brep FloorBasePart = Brep.CreateFromSurface(Surface.CreateExtrusion(Parts3PL.ToNurbsCurve(), new Vector3d(Size2 / 2.0 + Size3 / 2.0, 0, 0))).CapPlanarHoles(0);

                    for (int i = 0; i < 4; i++)
                    {
                        Brep TempFloor = (Brep)FloorBasePart.Duplicate();
                        TempFloor.Rotate(Math.PI / 2.0 * i, new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                        FloorBase[i] = TempFloor;
                    }
                }
                RetParts3.AddRange(GeneralHelper.TranslateBreps(FloorBase, new Vector3d(0, 0, -TotalHeight - Elevation)));
                RetParts3.AddRange(GeneralHelper.TranslateBreps(FloorBase, new Vector3d(0, 0, -TotalHeight / 2.0 - Elevation)));
                Parts3 = RetParts3.ToArray();
            }

            public static Brep[] GetLadderSimple(double Length)
            {
                return GetLadderSimple(Length, 300, 400, 25, 15);
            }

            public static Brep[] GetLadderSimple(double Length, double Space, double Width, double Radius1, double Radius2)
            {
                List<Brep> Result = new List<Brep>();
                Result.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(new Point3d(-Width / 2.0, 0, 0), Radius1)), new Vector3d(0, 0, Length))).CapPlanarHoles(0));
                Result.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(new Point3d(+Width / 2.0, 0, 0), Radius1)), new Vector3d(0, 0, Length))).CapPlanarHoles(0));

                for (double CurrentLength = Space; CurrentLength < Length; CurrentLength += Space)
                {
                    Result.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(NurbsCurve.CreateFromCircle(new Circle(new Plane(new Point3d(-Width / 2.0, 0, CurrentLength), new Vector3d(1, 0, 0)), Radius2)), new Vector3d(Width, 0, 0))).CapPlanarHoles(0));
                }
                return Result.ToArray();
            }


            public static Brep[] GetTowerClaneBasement(int Count)
            {
                return GetTowerClaneBasement(Count, 1000, 800, 30, 150, 100);
            }

            public static Brep[] GetTowerClaneBasement(int Count, double UnitWidth, double UnitHeight, double Thickness, double Width1, double Width2)
            {
                double Tr1 = UnitWidth / 2.0 - Width1 / 2.0;
                double Tr2 = UnitHeight - Width2;
                double Tr3 = Math.Sqrt(Tr1 * Tr1 + Tr2 * Tr2);

                Polyline BasePL1 = new Polyline(7);
                BasePL1.Add(0, 0, 0);
                BasePL1.Add(Width1, 0, 0);
                BasePL1.Add(Width1, -Thickness, 0);
                BasePL1.Add(-Thickness, -Thickness, 0);
                BasePL1.Add(-Thickness, Width1, 0);
                BasePL1.Add(0, Width1, 0);
                BasePL1.Add(0, 0, 0);
                Brep Parts1 = Brep.CreateFromSurface(Surface.CreateExtrusion((BasePL1.ToNurbsCurve()), new Vector3d(0, 0, UnitHeight * Count))).CapPlanarHoles(0);
                Parts1.Translate(-UnitWidth / 2.0, -UnitWidth / 2.0, 0);

                Polyline BasePL3 = new Polyline(12);
                BasePL3.Add(Width1 / 2.0, 0, 0);
                BasePL3.Add(UnitWidth / 2.0, 0, UnitHeight - Width2);
                BasePL3.Add(UnitWidth - Width1 / 2.0, 0, 0);
                BasePL3.Add(UnitWidth - Width1 / 2.0, 0, Width2 / Tr1 * Tr3);
                BasePL3.Add(UnitWidth / 2.0 + Width2 / Tr2 * Tr3, 0, UnitHeight - Width2);
                BasePL3.Add(UnitWidth - Width1 / 2.0, 0, UnitHeight - Width2);
                BasePL3.Add(UnitWidth - Width1 / 2.0, 0, UnitHeight);

                BasePL3.Add(Width1 / 2.0, 0, UnitHeight);
                BasePL3.Add(Width1 / 2.0, 0, UnitHeight - Width2);
                BasePL3.Add(UnitWidth / 2.0 - Width2 / Tr2 * Tr3, 0, UnitHeight - Width2);
                BasePL3.Add(Width1 / 2.0, 0, Width2 / Tr1 * Tr3);
                BasePL3.Add(Width1 / 2.0, 0, 0);
                Brep Parts3 = Brep.CreateFromSurface(Surface.CreateExtrusion((BasePL3.ToNurbsCurve()), new Vector3d(0, Thickness, 0))).CapPlanarHoles(0);
                Parts3.Translate(-UnitWidth / 2.0, -UnitWidth / 2.0, 0);

                Brep[] Result = new Brep[4 + Count * 4];
                for (int i = 0; i < 4; i++)
                {
                    Brep TempParts1 = (Brep)Parts1.Duplicate();
                    TempParts1.Rotate(Math.PI / 2.0 * i, new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                    Result[i] = TempParts1;

                    for (int j = 0; j < Count; j++)
                    {
                        Brep TempParts3 = (Brep)Parts3.Duplicate();
                        TempParts3.Rotate(Math.PI / 2.0 * i, new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                        TempParts3.Translate(0, 0, j * UnitHeight);
                        Result[4 + j * 4 + i] = TempParts3;
                    }
                }
                return Result;
            }


            public static void GetGlassWithSpandrel(double WindowWidth, double WindowHeight, double FrameSize, double FrameThickness, double GlassThickness, double SpandrelHeight, out Brep[] Frame, out Brep Glass, out Brep Spandrel)
            {
                double SpandrelSpace = FrameSize;
                double SpandrelSpaceZ = GlassThickness * 2;
                double SpandrelThickness = GlassThickness;

                GetGlassWithSpandrel(WindowWidth, WindowHeight, FrameSize, FrameThickness, GlassThickness, SpandrelHeight, SpandrelSpace, SpandrelSpaceZ, SpandrelThickness, out Frame, out Glass, out Spandrel);
            }

            public static void GetGlassWithSpandrel(double WindowWidth, double WindowHeight, double FrameSize, double FrameThickness, double GlassThickness, double SpandrelHeight, double SpandrelSpace, double SpandrelSpaceZ, double SpandrelThickness, out Brep[] Frame, out Brep Glass, out Brep Spandrel)
            {
                Brep BaseFrame;
                GetGlassSimple(WindowWidth, WindowHeight, FrameSize, FrameThickness, GlassThickness, out BaseFrame, out Glass);
                Spandrel = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, 0), new Vector3d(0, 1, 0)), new Interval(SpandrelSpace, SpandrelHeight - SpandrelSpace), new Interval(SpandrelSpace, WindowWidth - SpandrelSpace), new Interval(SpandrelSpaceZ, SpandrelSpaceZ + SpandrelThickness)));
                Brep SpandrelFrame = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, 0), new Vector3d(0, 1, 0)), new Interval(SpandrelHeight - FrameSize, SpandrelHeight), new Interval(FrameSize / 2.0, WindowWidth - FrameSize / 2.0), new Interval(-FrameThickness, GlassThickness)));
                Frame = new Brep[] { BaseFrame, SpandrelFrame };
            }

            public static void GetGlassWithSpandrelUpsidedown(double WindowWidth, double WindowHeight, double FrameSize, double FrameThickness, double GlassThickness, double SpandrelHeight, out Brep[] Frame, out Brep Glass, out Brep Spandrel)
            {
                double SpandrelSpace = FrameSize;
                double SpandrelSpaceZ = GlassThickness * 2;
                double SpandrelThickness = GlassThickness;

                GetGlassWithSpandrelUpsidedown(WindowWidth, WindowHeight, FrameSize, FrameThickness, GlassThickness, SpandrelHeight, SpandrelSpace, SpandrelSpaceZ, SpandrelThickness, out Frame, out Glass, out Spandrel);
            }

            public static void GetGlassWithSpandrelUpsidedown(double WindowWidth, double WindowHeight, double FrameSize, double FrameThickness, double GlassThickness, double SpandrelHeight, double SpandrelSpace, double SpandrelSpaceZ, double SpandrelThickness, out Brep[] Frame, out Brep Glass, out Brep Spandrel)
            {
                Brep BaseFrame;
                GetGlassSimple(WindowWidth, WindowHeight, FrameSize, FrameThickness, GlassThickness, out BaseFrame, out Glass);
                Spandrel = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, WindowHeight - SpandrelHeight), new Vector3d(0, 1, 0)), new Interval(SpandrelSpace, SpandrelHeight - SpandrelSpace), new Interval(SpandrelSpace, WindowWidth - SpandrelSpace), new Interval(SpandrelSpaceZ, SpandrelSpaceZ + SpandrelThickness)));
                Brep SpandrelFrame = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, 0), new Vector3d(0, 1, 0)), new Interval(WindowHeight - SpandrelHeight, WindowHeight - SpandrelHeight + FrameSize), new Interval(FrameSize / 2.0, WindowWidth - FrameSize / 2.0), new Interval(-FrameThickness, GlassThickness)));
                Frame = new Brep[] { BaseFrame, SpandrelFrame };
            }

            public static void GetGlassSimple(double WindowWidth, double WindowHeight, double FrameSize, double FrameThickness, double GlassThickness, out Brep Frame, out Brep Glass)
            {
                GetGlassSimple(WindowWidth, WindowHeight, FrameSize, FrameSize, FrameThickness, GlassThickness, out Frame, out Glass);
            }

            public static void GetGlassSimple(double WindowWidth, double WindowHeight, double FrameSizeX, double FrameSizeY, double FrameThickness, double GlassThickness, out Brep Frame, out Brep Glass)
            {
                Brep FrameBase = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, 0), new Vector3d(0, 1, 0)), new Interval(0, WindowHeight), new Interval(0, WindowWidth), new Interval(-FrameThickness, GlassThickness)));
                Brep Splitter = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, 0), new Vector3d(0, 1, 0)), new Interval(FrameSizeY, WindowHeight - FrameSizeY), new Interval(FrameSizeX, WindowWidth - FrameSizeX), new Interval(-FrameThickness * 2, GlassThickness * 2)));
                var SplitResult = (FrameBase.Split(Splitter, 0.10));
                Frame = SplitResult[0];
                Glass = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, 0), new Vector3d(0, 1, 0)), new Interval(FrameSizeY, WindowHeight - FrameSizeY), new Interval(FrameSizeX, WindowWidth - FrameSizeX), new Interval(0, GlassThickness)));
            }

            public static void GetIsolatedWindow(double FrameSizeX, double FrameSizeY, double FrameThickness1, double FrameThickness2, double UnitSizeX, double UnitSizeY, double GlassThickness, int UnitCountX, int UnitCountY, out Brep[] Glass, out Brep[] Frame1, out Brep[] Frame2)
            {
                Frame1 = new Brep[UnitCountY * 2];
                for (int i = 0; i < UnitCountY; i++)
                {
                    Frame1[i * 2] = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, UnitSizeY * i), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)), new Interval(0, UnitSizeX * UnitCountX), new Interval(0, FrameSizeY), new Interval(-GlassThickness, FrameThickness1)));
                    Frame1[i * 2 + 1] = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, 0, UnitSizeY * (i + 1)), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)), new Interval(0, UnitSizeX * UnitCountX), new Interval(-FrameSizeY, 0), new Interval(-GlassThickness, FrameThickness1)));
                }
                Frame2 = new Brep[UnitCountX * 2];
                for (int i = 0; i < UnitCountX; i++)
                {
                    Frame2[i * 2] = Brep.CreateFromBox(new Box(new Plane(new Point3d(UnitSizeX * i, 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)), new Interval(0, FrameSizeX), new Interval(0, UnitSizeY * UnitCountY), new Interval(-GlassThickness, FrameThickness2)));
                    Frame2[i * 2 + 1] = Brep.CreateFromBox(new Box(new Plane(new Point3d(UnitSizeX * (i + 1), 0, 0), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)), new Interval(-FrameSizeX, 0), new Interval(0, UnitSizeY * UnitCountY), new Interval(-GlassThickness, FrameThickness2)));
                }
                Glass = new Brep[UnitCountX * UnitCountY];
                for (int i = 0; i < UnitCountX; i++)
                {
                    for (int j = 0; j < UnitCountY; j++)
                    {
                        Glass[i * UnitCountY + j] = Brep.CreateFromBox(new Box(new Plane(new Point3d(UnitSizeX * i, 0, UnitSizeY * j), new Vector3d(1, 0, 0), new Vector3d(0, 0, 1)), new Interval(FrameSizeX, UnitSizeX - FrameSizeX), new Interval(FrameSizeY, UnitSizeY - FrameSizeY), new Interval(-GlassThickness, 0)));
                    }
                }
            }

            public static Brep GetStairsSolid(int Count, double StairWidth, double StairHeight, double StairLength, double StairThicknss)
            {
                StairThicknss = Math.Max(1, Math.Abs(StairThicknss));
                Rhino.Geometry.Polyline pl = new Polyline(4 + Count * 2);
                pl.Add(0, StairLength * (Count), StairHeight * (Count));
                pl.Add(0, StairLength * (Count), StairHeight * (Count) - StairThicknss);
                pl.Add(0, StairLength, StairHeight - StairThicknss);
                pl.Add(0, 0, StairHeight - StairThicknss);
                for (int i = 1; i <= Count; i++)
                {
                    pl.Add(0, StairLength * (i - 1), StairHeight * i);
                    pl.Add(0, StairLength * (i), StairHeight * i);
                }
                return Brep.CreateFromSurface(Surface.CreateExtrusion((pl.ToNurbsCurve()), new Vector3d(StairWidth, 0, 0))).CapPlanarHoles(0);
            }

            public static Brep[] GetStairsSimple(int Count, double StairWidth, double StairHeight, double StairLength1, double StairLength2, double StairThickness)
            {
                Brep[] result = new Brep[Count];
                for (int i = 1; i <= Count; i++)
                {
                    result[i - 1] = Brep.CreateFromBox(new Box(new Plane(new Point3d(0, StairLength2 * (i - 1), StairHeight * i), new Vector3d(1, 0, 0), new Vector3d(0, 1, 0))
                      , new Interval(0, StairWidth), new Interval(0, StairLength1), new Interval(-StairThickness, 0)));
                }
                return result;
            }

            public static void GetStairsBasic(int Count, double StairWidth, double StairHeight, double StairLength1, double StairLength2, double StairThickness, double OtherThickness, out Brep[] Stairs, out Brep[] Other)
            {
                Stairs = GetStairsSimple(Count, StairWidth, StairHeight, StairLength1, StairLength2, StairThickness);

                Rhino.Geometry.Polyline pl = new Polyline(7);
                pl.Add(0, 0, StairHeight - StairThickness);
                pl.Add(0, StairLength1, StairHeight - StairThickness);
                pl.Add(0, StairLength1 + StairLength2 * Count, StairHeight * (Count + 1) - StairThickness);
                pl.Add(0, StairLength1 + StairLength2 * Count, StairHeight * (Count + 1));
                pl.Add(0, StairLength2 * Count, StairHeight * (Count + 1));
                pl.Add(0, 0, StairHeight);
                pl.Add(0, 0, StairHeight - StairThickness);

                Curve plc = pl.ToNurbsCurve();
                Brep otherSrf1 = Brep.CreateFromSurface(Surface.CreateExtrusion(plc, new Vector3d(-OtherThickness, 0, 0))).CapPlanarHoles(0);
                Brep otherSrf2 = (Brep)otherSrf1.Duplicate();
                otherSrf2.Translate(StairWidth + OtherThickness, 0, 0);
                Other = new Brep[] { otherSrf1, otherSrf2 };
            }

            public static Brep[] GetTactilePavingAlongCurve(Curve[] Target)
            {
                double UnitSize = 302.5;
                List<Brep> Result = new List<Brep>();

                Brep TPP = GetTactilePavingPoint();
                Brep TPL = GetTactilePavingLine();

                foreach (Curve cv in Target)
                {
                    double length = cv.GetLength();
                    double t;
                    Vector3d Normal;
                    cv.Domain = new Interval(0, 1);
                    if (length < UnitSize * 3)
                    {
                        Normal = cv.CurvatureAt(0);
                        cv.LengthParameter(length / 2.0, out t);
                        Result.Add(GetTactilePavingAlongCurveHelper1(TPP, cv, t));
                    }
                    Result.AddRange(GetTactilePavingAlongCurveHelper2(TPP, cv, 0, UnitSize));
                    double CurrentLen = 0;
                    for (CurrentLen = UnitSize; CurrentLen + UnitSize < length; CurrentLen += UnitSize)
                    {
                        cv.LengthParameter(CurrentLen, out t);
                        Result.Add(GetTactilePavingAlongCurveHelper1(TPL, cv, t));
                    }
                    cv.LengthParameter(CurrentLen, out t);
                    Result.AddRange(GetTactilePavingAlongCurveHelper2(TPP, cv, t, UnitSize));
                }
                return Result.ToArray();
            }

            private static Brep GetTactilePavingAlongCurveHelper1(Brep Origin, Curve cv, double t)
            {
                return GetTactilePavingAlongCurveHelper1(Origin, cv, t, GeneralHelper.GetCurvatureAsAngle(cv, t));
            }

            private static Brep GetTactilePavingAlongCurveHelper1(Brep Origin, Curve cv, double t, double angle)
            {
                return GetTactilePavingAlongCurveHelper1(Origin, cv.PointAt(t), GeneralHelper.GetCurvatureAsAngle(cv, t));
            }

            private static Brep GetTactilePavingAlongCurveHelper1(Brep Origin, Point3d point, double angle)
            {
                Brep TempTP = (Brep)Origin.Duplicate();
                angle += Math.PI / 2.0;
                TempTP.Rotate(angle, new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                TempTP.Translate((Vector3d)point);
                return TempTP;
            }

            private static Brep[] GetTactilePavingAlongCurveHelper2(Brep Origin, Curve cv, double t, double UnitSize)
            {
                double angle = GeneralHelper.GetCurvatureAsAngle(cv, t);
                Vector3d Curvature = cv.CurvatureAt(t);
                Curvature /= Curvature.Length;
                return new Brep[]{ GetTactilePavingAlongCurveHelper1(Origin, cv.PointAt(t), angle)
                    ,GetTactilePavingAlongCurveHelper1(Origin, cv.PointAt(t) + (Curvature * UnitSize), angle)
                    ,GetTactilePavingAlongCurveHelper1(Origin, cv.PointAt(t) - (Curvature * UnitSize), angle)
                };
            }

            /*
            public static double GeneralHelper.GetCurvatureAsAngle(Curve cv, double t)
            {
                Vector3d Curvature = cv.CurvatureAt(t);
                double angle = 0;
                if (Curvature.X == 0) { angle = Math.PI / 2.0; }
                else
                {
                    angle = Math.Atan(Curvature.Y / Curvature.X);
                }
                return angle;
            }
             */

            public static Brep GetTactilePavingPoint()
            {
                return GetTactilePavingPoint(300, 5, 12, 22, 5, 60, 5);
            }

            public static Brep GetTactilePavingPoint(double BlockSize, int PointCount, double PointSizeTop, double PointSizeBottom, double PointHeight, double PointSpace, double BlockThickness)
            {
                Brep[] Result = new Brep[1 + PointCount * PointCount];
                Result[0] = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(-BlockSize / 2.0, BlockSize / 2.0), new Interval(-BlockSize / 2.0, BlockSize / 2.0), new Interval(0, BlockThickness)));

                Brep BasicShape = (Brep.CreateFromLoft(
                  new Curve[]{
                      NurbsCurve.CreateFromCircle(new Circle(new Point3d(0, 0, BlockThickness / 2.0), PointSizeBottom))
                      ,NurbsCurve.CreateFromCircle(new Circle(new Point3d(0, 0, BlockThickness), PointSizeBottom))
                      ,NurbsCurve.CreateFromCircle(new Circle(new Point3d(0, 0, BlockThickness + PointHeight), PointSizeTop))}, new Point3d(0, 0, BlockThickness), new Point3d(0, 0, BlockThickness + PointHeight), LoftType.Straight, false))[0];
                BasicShape.CapPlanarHoles(PointSizeTop / 100.0);

                for (int i = 0; i < PointCount; i++)
                {
                    for (int j = 0; j < PointCount; j++)
                    {
                        Brep TempShape = (Brep)BasicShape.Duplicate();
                        TempShape.Translate((i - PointCount / 2.0 + 0.5) * PointSpace, (j - PointCount / 2.0 + 0.5) * PointSpace, 0);
                        Result[i * PointCount + j + 1] = TempShape;
                    }
                }
                return (Brep.CreateBooleanUnion(Result, PointSizeTop / 100.0))[0];
            }

            public static Brep GetTactilePavingLine()
            {
                return GetTactilePavingLine(300, 4, 17, 27, 270, 5, 75, 5);
            }

            public static Brep GetTactilePavingLine(double BlockSize, int LineCount, double LineWidthTop, double LineWidthBottom, double LineLength, double LineHeight, double LineSpace, double BlockThickness)
            {
                LineLength -= LineWidthTop;

                Brep[] Result = new Brep[1 + LineCount * LineCount];
                Result[0] = Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(-BlockSize / 2.0, BlockSize / 2.0), new Interval(-BlockSize / 2.0, BlockSize / 2.0), new Interval(0, BlockThickness)));

                Curve BottomCurve = Curve.JoinCurves(new Curve[]{
                    NurbsCurve.CreateFromArc(new Arc(new Circle(new Point3d(-LineLength / 2.0, 0, 0), LineWidthBottom / 2.0), new Interval(Math.PI / 2.0, Math.PI / 2.0 * 3.0))),
                    NurbsCurve.CreateFromArc(new Arc(new Circle(new Point3d(LineLength / 2.0, 0, 0), LineWidthBottom / 2.0), new Interval(-Math.PI / 2.0, Math.PI / 2.0))),
                    NurbsCurve.CreateFromLine(new Line(new Point3d(-LineLength / 2.0, LineWidthBottom / 2.0, 0), new Point3d(LineLength / 2.0, LineWidthBottom / 2.0, 0))),
                    NurbsCurve.CreateFromLine(new Line(new Point3d(-LineLength / 2.0, -LineWidthBottom / 2.0, 0), new Point3d(LineLength / 2.0, -LineWidthBottom / 2.0, 0)))})[0];
                Curve MiddleCurve = (Curve)BottomCurve.Duplicate();
                BottomCurve.Translate(0, 0, BlockThickness / 2.0);
                MiddleCurve.Translate(0, 0, BlockThickness);
                Curve TopCurve = Curve.JoinCurves(new Curve[]{
                    NurbsCurve.CreateFromArc(new Arc(new Circle(new Point3d(-LineLength / 2.0, 0, 0), LineWidthTop / 2.0), new Interval(Math.PI / 2.0, Math.PI / 2.0 * 3.0))),
                    NurbsCurve.CreateFromArc(new Arc(new Circle(new Point3d(LineLength / 2.0, 0, 0), LineWidthTop / 2.0), new Interval(-Math.PI / 2.0, Math.PI / 2.0))),
                    NurbsCurve.CreateFromLine(new Line(new Point3d(-LineLength / 2.0, LineWidthTop / 2.0, 0), new Point3d(LineLength / 2.0, LineWidthTop / 2.0, 0))),
                    NurbsCurve.CreateFromLine(new Line(new Point3d(-LineLength / 2.0, -LineWidthTop / 2.0, 0), new Point3d(LineLength / 2.0, -LineWidthTop / 2.0, 0)))})[0];
                TopCurve.Translate(0, 0, BlockThickness + LineHeight);


                Brep BasicShape = (Brep.CreateFromLoft(
                  new Curve[] { BottomCurve, MiddleCurve, TopCurve }, new Point3d(0, 0, BlockThickness), new Point3d(0, 0, BlockThickness + LineHeight), LoftType.Straight, false))[0];
                BasicShape.CapPlanarHoles(LineWidthTop / 100.0);

                for (int i = 0; i < LineCount; i++)
                {
                    Brep TempShape = (Brep)BasicShape.Duplicate();
                    TempShape.Translate(0, (i - LineCount / 2.0 + 0.5) * LineSpace, 0);
                    Result[i + 1] = TempShape;
                }
                return (Brep.CreateBooleanUnion(Result, LineWidthTop / 100.0))[0];
            }

            public static Curve[] TrimOutline(Curve[] Origin, double[] Width)
            {
                Curve[] OutlineCurve = new Curve[Origin.GetLength(0)];
                List<List<Curve>> Result = new List<List<Curve>>();
                for (int i = 0; i < Origin.GetLength(0); i++)
                {
                    OutlineCurve[i] = GetCurveWithWidth(Origin[i], Width[Math.Min(i, Width.GetLength(0) - 1)]);
                    Result.Add(new List<Curve>());
                    //Result[i].Add((Curve) OutlineCurve[i].Duplicate());
                    Result[i].Add((Curve)Origin[i].Duplicate());
                }
                for (int i = 0; i < Origin.GetLength(0); i++)
                {
                    for (int j = 0; j < Result.Count(); j++)
                    {
                        if (i == j) { continue; }
                        for (int k = 0; k < Result[j].Count(); k++)
                        {
                            Curve[] TempRet = TrimCurveByOutline(Result[j][k], OutlineCurve[i]);
                            if (TempRet.GetLength(0) > 1)
                            {
                                Result[j].Remove(Result[j][k]);
                                Result[j].AddRange(TempRet);
                                k--;
                            }
                        }
                    }
                }
                List<Curve> TotalResult = new List<Curve>();
                for (int j = 0; j < Result.Count(); j++)
                {
                    TotalResult.AddRange(Result[j]);
                }
                return TotalResult.ToArray();
            }

            public static Curve[] TrimCurveByOutline(Curve Origin, Curve Cutter)
            {
                Origin.Domain = new Interval(0, 1);
                Cutter.Domain = new Interval(0, 1);
                Rhino.Geometry.Intersect.CurveIntersections CurveIntersectRet = Rhino.Geometry.Intersect.Intersection.CurveCurve(Origin, Cutter, 1.0, 1.0);
                double[] SumLength = new double[] { 0.0, 0.0 };
                int cnt = 0;
                double LastPara = 0.0;
                for (int i = 0; i < CurveIntersectRet.Count(); i++)
                {
                    if (CurveIntersectRet[i].ParameterA % 1.0 == 0.0) continue;
                    if (CurveIntersectRet[i].IsOverlap) continue;
                    SumLength[cnt % 2] += CurveIntersectRet[i].ParameterA - LastPara;
                    LastPara = CurveIntersectRet[i].ParameterA;
                    cnt++;
                }
                int Choice = SumLength[0] < SumLength[1] ? 1 : 0;
                List<Curve> Result = new List<Curve>();
                cnt = 0;
                LastPara = 0.0;
                for (int i = 0; i < CurveIntersectRet.Count(); i++)
                {
                    if (CurveIntersectRet[i].ParameterA % 1.0 == 0.0) continue;
                    if (CurveIntersectRet[i].IsOverlap) continue;
                    if (cnt % 2 == Choice)
                    {
                        if (LastPara == 0)
                        {
                            Result.Add((Origin.Split(CurveIntersectRet[i].ParameterA))[0]);
                        }
                        else
                        {
                            Result.Add((Origin.Split(new double[] { LastPara, CurveIntersectRet[i].ParameterA }))[1]);
                        }
                    }
                    LastPara = CurveIntersectRet[i].ParameterA;
                    cnt++;
                }
                if (cnt % 2 == Choice)
                {
                    if (LastPara == 0)
                    {
                        Result.Add((Curve)Origin.Duplicate());
                    }
                    else
                    {
                        Result.Add((Origin.Split(LastPara))[1]);
                    }
                }
                return Result.ToArray();
            }

            public static Curve[] GetOutlineSimple(Curve[] Origin, double Width)
            {
                Curve[] OutlineCurveBase = new Curve[Origin.GetLength(0)];
                for (int i = 0; i < Origin.GetLength(0); i++)
                {
                    OutlineCurveBase[i] = GetCurveWithWidth(Origin[i], Width);
                }
                return Curve.CreateBooleanUnion(OutlineCurveBase);
            }

            public static Curve[] GetOutlineSimple(Curve[] Origin, double[] Width)
            {
                Curve[] OutlineCurveBase = new Curve[Origin.GetLength(0)];
                for (int i = 0; i < Origin.GetLength(0); i++)
                {
                    OutlineCurveBase[i] = GetCurveWithWidth(Origin[i], Width[Math.Min(i, Width.GetLength(0) - 1)]);
                }
                return Curve.CreateBooleanUnion(OutlineCurveBase);
            }

            public static Curve[] GetCurveWithWidth(Curve[] Origin, double Width)
            {
                Curve[] Result = new Curve[Origin.GetLength(0)];
                for (int i = 0; i < Origin.GetLength(0); i++)
                {
                    Result[i] = GetCurveWithWidth(Origin[i], Width);
                }
                return Result;
            }

            public static Curve GetCurveWithWidth(Curve Origin, double Width)
            {
                Width /= 2;
                Curve cv1 = (Origin.Offset(Plane.WorldXY, Width, Width / 100.0, Rhino.Geometry.CurveOffsetCornerStyle.Sharp))[0];
                Curve cv2 = (Origin.Offset(Plane.WorldXY, -Width, Width / 100.0, Rhino.Geometry.CurveOffsetCornerStyle.Sharp))[0];

                return (Curve.JoinCurves(new Curve[] { cv1, cv2, NurbsCurve.CreateFromLine(new Line(cv1.PointAtEnd, cv2.PointAtEnd)), NurbsCurve.CreateFromLine(new Line(cv1.PointAtStart, cv2.PointAtStart)) }))[0];
            }

            public static Curve[] GetCurveWithWidthRound(Curve[] Origin, double Width)
            {
                Curve[] Result = new Curve[Origin.GetLength(0)];
                for (int i = 0; i < Origin.GetLength(0); i++)
                {
                    Result[i] = GetCurveWithWidthRound(Origin[i], Width);
                }
                return Result;
            }

            public static Curve GetCurveWithWidthRound(Curve Origin, double Width)
            {
                Width /= 2;
                Curve cv1 = (Origin.Offset(Plane.WorldXY, Width, Width / 100.0, Rhino.Geometry.CurveOffsetCornerStyle.Sharp))[0];
                Curve cv2 = (Origin.Offset(Plane.WorldXY, -Width, Width / 100.0, Rhino.Geometry.CurveOffsetCornerStyle.Sharp))[0];
                cv1.Domain = new Interval(0, 1);
                cv2.Domain = new Interval(0, 1);

                return (Curve.JoinCurves(new Curve[] { cv1, cv2, NurbsCurve.CreateFromArc(new Arc(cv1.PointAtEnd, cv1.TangentAt(1.0), cv2.PointAtEnd)), NurbsCurve.CreateFromArc(new Arc(cv1.PointAtStart, cv1.TangentAt(0.0), cv2.PointAtStart)) }))[0];
            }


            public static Curve[] GetDashedLine(Curve Origin, double SegmentLength)
            {
                int Count = (int)Math.Floor(Origin.GetLength() / SegmentLength / 2.0);
                Curve[] result = new Curve[Count + 1];

                for (int i = 0; i <= Count; i++)
                {
                    Curve TempCurve = (Curve)Origin.Duplicate();
                    Point3d a = TempCurve.PointAtLength(i * SegmentLength * 2.0);
                    Point3d b = TempCurve.PointAtLength(i * SegmentLength * 2.0 + SegmentLength);
                    double at, bt;
                    TempCurve.ClosestPoint(a, out at);
                    TempCurve.ClosestPoint(b, out bt);
                    TempCurve = (TempCurve.Split(new double[] { at, bt }))[i == 0 ? 0 : 1];
                    result[i] = TempCurve;
                }
                return result;
            }
        }
    }
}