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
        public class BuildingObject
        {
            public interface BuildingObjectProvider
            {
                RealObject.Building GetBuilding();
            }


            public interface Building : BuildingObjectProvider
            {
                PlanObject.Building GetPlan();
            }

            public class BuildingMultipleFloor : Building
            {
                public List<FloorGeneral> Content = new List<FloorGeneral>();
                public string Name = "Building";

                public BuildingMultipleFloor(string Name)
                {
                    this.Name = Name;
                }

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result=new RealObject.Building(this.Name);
                    double Height = 0;
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        FloorGeneral item = Content[i];

                        RealObject.Building Body = item.GetBuilding();
                        Body.Transform(Transform.Translation(0, 0, Height));
                        Result.Add(Body);

                        RealObject.Member Floor = item.GetFloor();
                        Floor.Transform(Transform.Translation(0, 0, Height));
                        Result.Add(Floor);

                        RealObject.Member Ceiling = item.GetCeiling();
                        Ceiling.Transform(Transform.Translation(0, 0, Height));
                        Result.Add(Ceiling);

                        RealObject.Member Loof = item.GetLoof();
                        Loof.Transform(Transform.Translation(0, 0, Height));
                        Result.Add(Loof);
                     
                        Height += item.Height;
                    }
                    return Result;
                }

                public RealObject.Building GetBuildingLight()
                {
                    RealObject.Building Result=new RealObject.Building(this.Name);
                    double Height = 0;
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        FloorGeneral item = Content[i];


                        List<Brep> Exts = new List<Brep>();
                        Curve[] Cvs=item.GetOuterLine();
                        foreach(Curve Cv in Cvs){
                            Exts.Add(Brep.CreateFromSurface(Surface.CreateExtrusion(Cv, Vector3d.ZAxis * item.Height)));
                        }
                        RealObject.Member Wall = new RealObject.Member("Wall", Exts.ToArray());
                        Wall.Transform(Transform.Translation(0, 0, Height));
                        Result.Add(Wall);


                        RealObject.Member Loof = item.GetLoof();
                        Loof.Transform(Transform.Translation(0, 0, Height));
                        Result.Add(Loof);

                        Height += item.Height;

                    }
                    return Result;
                }

                public PlanObject.Floor GetSection(Line l)
                {
                    Curve IntersectLine = l.ToNurbsCurve();
                    Point3d Start = l.From;
                    PlanObject.Floor Result = new PlanObject.Floor("Section");
                    double CurrentHeight = 0;

                    for (int i = 0; i < Content.Count(); i++)
                    {
                        FloorGeneral item = Content[i];
                        double FloorHeight = item.Height;
                        List<Point3d> TPoints = new List<Point3d>();

                        Curve[] OCurves = item.GetOuterLine();

                        foreach (Curve Curve in OCurves)
                        {
                            var intersects = Rhino.Geometry.Intersect.Intersection.CurveCurve(Curve, IntersectLine, 100, 100);
                            foreach (var intersect in intersects)
                            {
                                TPoints.Add(intersect.PointA);
                            }
                        }

                        List<double> Distances = new List<double>();
                        foreach (Point3d TPoint in TPoints)
                        {
                            Distances.Add(Start.DistanceTo(TPoint));
                        }
                        Distances.Sort();

                        PlanObject.Member Wall = new PlanObject.Member("Wall");
                        PlanObject.Member Loof = new PlanObject.Member("Loof");
                        PlanObject.Member Floor = new PlanObject.Member("Floor");
                        for (int j = 0; j < Distances.Count() - 1; j += 2)
                        {
                            Wall.Content.Add(new Line(Distances[j], CurrentHeight, 0, Distances[j], CurrentHeight + FloorHeight, 0).ToNurbsCurve());
                            Wall.Content.Add(new Line(Distances[j + 1], CurrentHeight, 0, Distances[j + 1], CurrentHeight + FloorHeight, 0).ToNurbsCurve());

                            Floor.Content.Add(new Line(Distances[j], CurrentHeight, 0, Distances[j + 1], CurrentHeight, 0).ToNurbsCurve());
                            Loof.Content.Add(new Line(Distances[j], CurrentHeight + FloorHeight, 0, Distances[j + 1], CurrentHeight + FloorHeight, 0).ToNurbsCurve());
                        }
                        Result.Content.Add(Wall);
                        Result.Content.Add(Loof);
                        Result.Content.Add(Floor);

                        CurrentHeight += FloorHeight;
                    }
                    return Result;
                }


                public PlanObject.Building GetPlan()
                {
                    PlanObject.Building Result = new PlanObject.Building(this.Name);
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        FloorGeneral item = Content[i];

                        Result.Content.Add(item.GetPlan());
                    }
                    return Result;
                }
            }

            public class FloorGeneral:BuildingObjectProvider{
                public List<Wall> Walls = new List<Wall>();
                public List<RealObject.Building> Objects = new List<RealObject.Building>();
                public double Height = 0;
                public string Name = "Floor";
                public double CeilingThick = 300;
                public double GroundOffset = 200;

                public FloorGeneral(string Name)
                {
                    this.Name = Name;
                }

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building(this.Name);
                    foreach (Wall w in Walls)
                    {
                        Result.Add(w.GetBuilding());
                    }
                    Result.Add(Objects.ToArray());
                    return Result;
                }

                public RealObject.Member GetCeiling()
                {
                    Brep[] CeilBase = Brep.CreatePlanarBreps(GetOuterLine());
                    return new RealObject.Member("Ceiling", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, Height - CeilingThick)));
                }
                public RealObject.Member GetFloor()
                {
                    Brep[] CeilBase = Brep.CreatePlanarBreps(GetOuterLine());
                    return new RealObject.Member("Floor", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, 1)));
                }
                public RealObject.Member GetLoof()
                {
                    Brep[] CeilBase = Brep.CreatePlanarBreps(GetOuterLine());
                    return new RealObject.Member("Loof", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, Height)));
                }

                public Curve[] GetOuterLine()
                {
                    List<Curve> cvs = new List<Curve>();
                    foreach (Wall w in Walls)
                    {
                        Curve cv = w.GetLineOut();
                        if (cv != null) { cvs.Add(cv); }
                    }
                    return Curve.JoinCurves(cvs);
                }

                public PlanObject.Floor GetPlan()
                {
                    PlanObject.Floor Result = new PlanObject.Floor("Floor");
                    foreach (Wall w in this.Walls)
                    {
                        Result.Content.AddRange(w.GetPlan());
                    }
                    return Result;
                }

                public void Add(params Wall[] item)
                {
                    Walls.AddRange(item);
                }

                public void Add(params RealObject.Building[] item)
                {
                    Objects.AddRange(item);
                }
            }

            public class BuildingSingleFloor : Building
            {
                public List<Wall> Walls = new List<Wall>();
                public List<RealObject.Building> Objects = new List<RealObject.Building>();
                public int Floor = 1;
                public double Height;
                public string Name = "Building";
                public double CeilingThick = 300;
                public double GroundOffset = 200;

                public BuildingSingleFloor(string Name)
                {
                    this.Name = Name;
                }
                public BuildingSingleFloor()
                {
                }

                public RealObject.Building GetBuilding()
                {
                    List<RealObject.Building> BldBase = new List<RealObject.Building>();
                    List<RealObject.Building> BldBaseTop = new List<RealObject.Building>();
                    foreach (Wall w in Walls)
                    {
                        BldBase.Add(w.GetBuilding());
                        BldBaseTop.Add(w.GetBuildingTop());
                    }
                    RealObject.Building Ceiling = new RealObject.Building("Ceiling");
                    Brep[] CeilBase = Brep.CreatePlanarBreps(GetOuterLine());
                    Ceiling.Add("Ceiling", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, Height - CeilingThick)));
                    BldBase.Add(Ceiling);

                    BldBase.AddRange(Objects);
 
                    RealObject.Member Loof = new RealObject.Member("Floor", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, Height)));

                    RealObject.Building Result = new RealObject.Building(Name);
                    for (int i = 0; i < Floor; i++)
                    {
                        foreach (RealObject.Building bld in BldBase)
                        {
                            foreach (RealObject.Member mb in bld.Content)
                            {
                                RealObject.Member newMem = mb.Duplicate();
                                newMem.Transform(Transform.Translation(0, 0, GroundOffset+Height * i));
                                Result.Add(newMem);
                            }
                        }

                        RealObject.Member newLoof = Loof.Duplicate();
                        newLoof.Transform(Transform.Translation(0, 0, GroundOffset+Height * i));
                        if (i + 1 == Floor)
                        {
                            newLoof.SetName("Loof");
                        }
                        Result.Add(newLoof);
                    }
                    {
                        RealObject.Member newLoof = Loof.Duplicate();
                        newLoof.Transform(Transform.Translation(0, 0, GroundOffset - Height));
                        Result.Add(newLoof);
                    }
                    if (GroundOffset != 0)
                    {
                        Curve[] cvs = GetOuterLine();
                        foreach (Curve cv in cvs)
                        {
                            Result.Add("Basement", Brep.CreateFromSurface(Surface.CreateExtrusion(cv, Vector3d.ZAxis * GroundOffset)));
                        }
                    }
                    foreach (RealObject.Building bld in BldBaseTop)
                    {
                        foreach (RealObject.Member mb in bld.Content)
                        {
                            RealObject.Member newMem = mb.Duplicate();
                            newMem.Transform(Transform.Translation(0, 0, GroundOffset + Height * (Floor-1)));
                            Result.Add(newMem);
                        }
                    }

                    return Result;
                }

                public PlanObject.Building GetPlan()
                {
                    PlanObject.Floor ResultF = new PlanObject.Floor("Floor");
                    foreach (Wall w in this.Walls)
                    {
                        ResultF.Content.AddRange(w.GetPlan());
                    }

                    PlanObject.Building ResultB = new PlanObject.Building("Building");
                    for (int i = 0; i < this.Floor; i++)
                    {
                        ResultB.Content.Add(ResultF);
                    }
                    return ResultB;
                }


                public void Add(params Wall[] item)
                {
                    Walls.AddRange(item);
                }

                public void Add(params RealObject.Building[] item)
                {
                    Objects.AddRange(item);
                }

                public Curve[] GetOuterLine()
                {
                    List<Curve> cvs = new List<Curve>();
                    foreach (Wall w in Walls)
                    {
                        Curve cv = w.GetLineOut();
                        if (cv != null) { cvs.Add(cv); }
                    }
                    return Curve.JoinCurves(cvs);
                }
            }


            public static class BuildingProvider
            {
                public static BuildingObject.Building GetApartment(double UnitX, double UnitY, double UnitZ, int UnitCountX, int UnitCountZ, double CeilingThick = 300,double WallThick=0)
                {
                    BuildingObject.BuildingSingleFloor Result = new BuildingObject.BuildingSingleFloor();
                    Result.Height = UnitZ;
                    Result.Name = "Apartment";
                    Result.CeilingThick = CeilingThick;
                    Result.Floor = UnitCountZ;

                    WallGeneral WallSouth = new WallGeneral(Point3d.Origin, new Vector2d(UnitX, 0), UnitZ, WallThick);
                    WallSouth.Add(new VerandaSimple(new Interval(0, UnitX), 100, 2000, 100, 1000, 150) { SideHeight1=UnitZ,SideHeight2=UnitZ});
                    WallSouth.AttachmentsTop.Add(new Eaves(new Interval(0, UnitX), 2000, 300, UnitZ));
                    for (double WindowX = 1000; WindowX + 2000 + 500 < UnitX; WindowX += 3000)
                    {
                        WallSouth.Add(new BuildingObject.WindowGlassSimpleDouble(new Point2d(WindowX, 0), 2000, 2500));
                    }
                    Result.Add(new WallRepeat(WallSouth, UnitCountX));

                    Result.Add(new WallGeneral(new Point3d(0, UnitY, 0), new Vector2d(0, -UnitY), UnitZ, WallThick));
                    for (int i = 1; i < UnitCountX; i++)
                    {
                        Result.Add(new WallGeneral(new Point3d(UnitX * i, 0, 0), new Vector2d(0, UnitY), UnitZ, WallThick));
                    }
                    Result.Add(new WallGeneral(new Point3d(UnitX * UnitCountX, 0, 0), new Vector2d(0, UnitY), UnitZ, WallThick));


                    WallGeneral WallNorth = new WallGeneral(new Point3d(UnitX * UnitCountX, UnitY, 0), new Vector2d(-UnitX * UnitCountX, 0), UnitZ, WallThick);
                    WallNorth.Add(new VerandaSimple(new Interval(0, UnitX * UnitCountX), 100, 2000, 100, 1000, 100));
                    for (int i = 0; i < UnitCountX; i++)
                    {
                        WallNorth.Add(new DoorSimple(new Point2d(UnitX*i+500,0),1000,2500,DoorSimple.Side.FrontRight) );
                    }
                    Result.Add(WallNorth);

                    return Result;
                }
            }

            public interface Stair : BuildingObjectProvider
            {
            }


            public interface Wall : BuildingObjectProvider
            {
                RealObject.Building GetBuildingTop();
                void Translate(Vector3d v3d);
                void Rotate(double angle);
                Curve[] GetCurves();
                Curve GetLineIn();
                Curve GetLineOut();
                Wall Duplicate();
                PlanObject.Member[] GetPlan();
            }

            public class WallRepeat : Wall
            {
                public Wall Content;
                public int Count;
                public Vector3d Direction{get{if(Content is WallGeneral){Vector2d v2d= ((WallGeneral)Content).Direction;return new Vector3d(v2d.X,v2d.Y,0);}else{return _Direction;}}set{if(!(Content is WallGeneral)){_Direction=value;}}}
                private Vector3d _Direction;

                public WallRepeat(WallGeneral Content, int Count)
                {
                    this.Content = Content;
                    this.Count = Count;
                }

                public WallRepeat(Wall Content, int Count,Vector3d Direction)
                {
                    this.Content = Content;
                    this.Count = Count;
                    this.Direction = Direction;
                }

                public Wall[] GetWalls()
                {
                    List<Wall> w = new List<Wall>();
                    for (int i = 0; i < Count; i++)
                    {
                        Wall tmpw = Content.Duplicate();
                        tmpw.Translate(Direction * i);
                        w.Add(tmpw);
                    }
                    return w.ToArray();
                }

                public RealObject.Building GetBuilding()
                {
                    Wall[] ws = GetWalls();
                    RealObject.Building Result = new RealObject.Building("WallRepeat");
                    foreach (Wall w in ws)
                    {
                        Result.Add(w.GetBuilding());
                    }
                    return Result;
                }

                public RealObject.Building GetBuildingTop()
                {
                    Wall[] ws = GetWalls();
                    RealObject.Building Result = new RealObject.Building("WallRepeat");
                    foreach (Wall w in ws)
                    {
                        Result.Add(w.GetBuildingTop());
                    }
                    return Result;
                }

                public void Translate(Vector3d v3d)
                {
                    Content.Translate(v3d);
                }

                public void Rotate(double angle)
                {
                    Content.Rotate(angle);
                }

                public Curve[] GetCurves()
                {
                    Wall[] ws = GetWalls();
                    List<Curve> cvs = new List<Curve>();
                    foreach (Wall w in ws)
                    {
                        cvs.AddRange(w.GetCurves());
                    }
                    return cvs.ToArray();
                }

                public Curve GetLineIn()
                {
                    Wall[] ws = GetWalls();
                    List<Curve> cvs = new List<Curve>();
                    foreach (Wall w in ws)
                    {
                        cvs.Add(w.GetLineIn());
                    }
                    return Curve.JoinCurves(cvs.ToArray())[0];
                }
                public Curve GetLineOut()
                {
                    Wall[] ws = GetWalls();
                    List<Curve> cvs = new List<Curve>();
                    foreach (Wall w in ws)
                    {
                        cvs.Add(w.GetLineOut());
                    }
                    return Curve.JoinCurves(cvs.ToArray())[0];
                }

                public Wall Duplicate()
                {
                    return new WallRepeat(this.Content, this.Count, this.Direction);
                }

                public PlanObject.Member[] GetPlan()
                {
                    Wall[] ws = GetWalls();
                    List<PlanObject.Member> Result = new List<PlanObject.Member>();
                    foreach (Wall w in ws)
                    {
                        Result.AddRange(w.GetPlan());
                    }
                    return Result.ToArray();
                }

            }

            public class WallGeneral:Wall
            {
                public Point3d StartPoint;
                public Vector2d Direction;
                public double Height;
                public double Thickness;
                public Point3d EndPoint { get { return StartPoint + new Vector3d(Direction.X, Direction.Y, Height); } set { Vector3d v3d = value - StartPoint; Direction = new Vector2d(v3d.X, v3d.Y); Height = v3d.Z; } }
                public bool ApplyZeroThicknessToBrep = false;
                public bool OmitFromFloorLine = false;

                public List<BuildingObject.Window> Windows = new List<BuildingObject.Window>();
                public List<BuildingObject.WallAttachment> Attachments = new List<BuildingObject.WallAttachment>();
                public List<BuildingObject.WallAttachment> AttachmentsTop = new List<BuildingObject.WallAttachment>();

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("Wall");
                    if (Thickness == 0 || ApplyZeroThicknessToBrep)
                    {
                        Result.Add("Wall", Brep.CreatePlanarBreps(GetCurves()));
                    }
                    else
                    {
                        Result.Add("Wall", GeneralHelper.CreateExtrusionCaped(GetCurves(), Vector3d.YAxis * Thickness));
                    }

                    foreach (BuildingObject.Window w in Windows)
                    {
                        Result.Add(w.GetBuilding());
                    }
                    foreach (BuildingObject.WallAttachment at in Attachments)
                    {
                        Result.Add(at.GetBuilding());
                    }
                    Result.Transform(GeneralHelper.FitTwoPoint(StartPoint, StartPoint + new Vector3d(Direction.X, Direction.Y, 0)));
                    return Result;
                }

                public RealObject.Building GetBuildingTop()
                {
                    RealObject.Building Result = new RealObject.Building("Wall.AttachmentTop");

                    foreach (BuildingObject.WallAttachment at in AttachmentsTop)
                    {
                        Result.Add(at.GetBuilding());
                    }
                    Result.Transform(GeneralHelper.FitTwoPoint(StartPoint, StartPoint + new Vector3d(Direction.X, Direction.Y, 0)));
                    return Result;
                }

                public Wall Duplicate()
                {
                    WallGeneral Result= new WallGeneral(this.StartPoint, this.Direction, this.Height, this.Thickness);

                    foreach (BuildingObject.Window item in Windows)
                    {
                        Result.Add(item.Duplicate());
                    }
                    foreach (BuildingObject.WallAttachment item in Attachments)
                    {
                        Result.Add(item.Duplicate());
                    }
                    foreach (BuildingObject.WallAttachment item in AttachmentsTop)
                    {
                        Result.AttachmentsTop.Add(item.Duplicate());
                    }
                    return Result;
                }

                public Curve GetLineOut()
                {
                    if (OmitFromFloorLine) {
                        return null;
                    }
                    else
                    {
                        return new Line(StartPoint, StartPoint + new Vector3d(Direction.X, Direction.Y, 0)).ToNurbsCurve();
                    }
                }

                public Curve GetLineIn()
                {
                    Vector3d V3dNormal = new Vector3d(-Direction.Y, Direction.X, 0);
                    V3dNormal.Unitize();

                    Vector3d V3dBase=new Vector3d(Direction.X, Direction.Y, 0);
                    double BaseLen = V3dBase.Length;
                    V3dBase.Unitize();

                    return new Line(StartPoint + V3dNormal * Thickness, StartPoint + V3dNormal * Thickness + V3dBase*BaseLen).ToNurbsCurve();
                }

                public Curve[] GetCurves()
                {
                    List<Curve> Result = new List<Curve>();

                    Result.Add(new Rectangle3d(Plane.WorldZX, Point3d.Origin, new Point3d(Direction.Length, 0, Height)).ToNurbsCurve());

                    foreach (BuildingObject.Window w in Windows)
                    {
                        Result.Add(new Rectangle3d(new Plane(new Point3d(w.StartPoint.X, 0, w.StartPoint.Y+0.1), Vector3d.XAxis, Vector3d.ZAxis), w.Width, w.Height).ToNurbsCurve());
                    }
                    return Result.ToArray();
                }

                public WallGeneral(Point3d StartPoint, Vector2d Direction, double Height, double Thickness = 0)
                {
                    this.StartPoint = StartPoint;
                    this.Direction = Direction;
                    this.Height = Height;
                    this.Thickness = Thickness;
                }

                public void Add(params Window[] item)
                {
                    Windows.AddRange(item);
                }

                public void Add(params WallAttachment[] item)
                {
                    Attachments.AddRange(item);
                }

                public void Translate(Vector3d v3d)
                {
                    StartPoint += v3d;
                }

                public void Rotate(double angle)
                {
                    Point3d p3d = new Point3d(Direction.X, Direction.Y, 0);
                    p3d.Transform(Transform.Rotation(angle, Point3d.Origin));
                    Direction.X = p3d.X;
                    Direction.Y = p3d.Y;
                }

                /*
                public void Transform(Transform tf)
                {
                    Point3d a = Point3d.Origin;
                    Point3d b = Point3d.Origin + Vector3d.YAxis;
                    a.Transform(tf);
                    b.Transform(tf);
                    Thickness = (a - b).Length;

                    Point3d End = EndPoint;
                    End.Transform(tf);
                    EndPoint = End;
                    StartPoint.Transform(tf);

                }*/

                public PlanObject.Member[] GetPlan()
                {
                    List<PlanObject.Member> Result = new List<PlanObject.Member>();

                    foreach (BuildingObject.WallAttachment item in Attachments)
                    {
                        Result.AddRange(item.GetPlan());
                    }
                    List<double> PointList = new List<double>() { 0, Direction.Length };
                    foreach (BuildingObject.Window item in Windows)
                    {
                        Result.AddRange(item.GetPlan(this.Thickness));
                        PointList.Add(item.StartPoint.X);
                        PointList.Add(item.StartPoint.X+item.Width);
                    }
                    PointList.Sort();

                    PlanObject.Member WallResult = new PlanObject.Member("Wall");
                    for (int i = 0; i < PointList.Count / 2; i++)
                    {
                        WallResult.Content.Add(Providers.GetRectangle3d(new Point3d(PointList[i * 2], 0, 0), PointList[i * 2 + 1] - PointList[i * 2], this.Thickness));
                    }
                    Result.Add(WallResult);
                    foreach (var item in Result)
                    {
                        item.Transform(GeneralHelper.FitTwoPoint(StartPoint, StartPoint + new Vector3d(Direction.X, Direction.Y, 0)));
                    }
                    return Result.ToArray();
                }
            }

            public interface WallAttachment : BuildingObjectProvider
            {
                WallAttachment Duplicate();
                PlanObject.Member[] GetPlan();

            }

            public class Eaves : WallAttachment
            {
                public Interval Domain;
                public double Length;
                public double Thickness;
                public double Height;

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("Eaves");
                    Result.Add("Body", Brep.CreateFromBox(new Box(Plane.WorldXY, this.Domain, new Interval(-Length, 0), new Interval(Height - Thickness, Height))));
                    return Result;
                }

                public PlanObject.Member[] GetPlan()
                {
                    return new PlanObject.Member[0];
                }

                public WallAttachment Duplicate()
                {
                    return new Eaves(this.Domain,this.Length,this.Thickness,this.Height);
                }

                public Eaves(Interval Domain, double Length, double Thickness, double Height)
                {
                    this.Domain = Domain;
                    this.Length = Length;
                    this.Thickness = Thickness;
                    this.Height = Height;
                }

            }

            public class VerandaSimple : WallAttachment
            {
                public Interval Domain;
                public double FloorThickness;
                public double FloorLength;
                public double HandrailThickness;
                public double HandrailHeight;
                public double SideThickness1;
                public double SideHeight1;
                public double SideThickness2;
                public double SideHeight2;

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("VerandaSimple");
                    Result.Add("Floor", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Min+SideThickness1,Domain.Max-SideThickness2), new Interval(-FloorLength, 0), new Interval(-FloorThickness, 0))) });
                    Result.Add("Handrail", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, Domain, new Interval(-HandrailThickness - FloorLength, -FloorLength), new Interval(-FloorThickness, HandrailHeight))) });
                    if (SideThickness1 != 0)
                    {
                        Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Min, Domain.Min + SideThickness1), new Interval(-FloorLength, 0), new Interval(0, SideHeight1))) });
                    }
                    if (SideThickness2 != 0)
                    {
                        Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Max - SideThickness2, Domain.Max), new Interval(-FloorLength, 0), new Interval(0, SideHeight2))) });
                    }
                    return Result;
                }

                public VerandaSimple(Interval Domain, double FloorThickness, double FloorLength, double HandrailThickness, double HandrailHeight, double SideThickness = 0)
                {
                    this.Domain = Domain;
                    this.FloorLength = FloorLength;
                    this.FloorThickness = FloorThickness;
                    this.HandrailThickness = HandrailThickness;
                    this.HandrailHeight = HandrailHeight;
                    this.SideThickness1 = SideThickness;
                    this.SideHeight1 = HandrailHeight;
                    this.SideThickness2 = SideThickness;
                    this.SideHeight2 = HandrailHeight;
                }

                public WallAttachment Duplicate()
                {
                    return new VerandaSimple(this.Domain, this.FloorThickness, this.FloorLength, this.HandrailThickness, this.HandrailHeight, 0) { SideThickness1=this.SideThickness1,SideThickness2=this.SideThickness2, SideHeight1 = this.SideHeight1,SideHeight2=this.SideHeight2 };
                }

                public PlanObject.Member[] GetPlan()
                {
                    PlanObject.Member Result = new PlanObject.Member("Veranda");
                    Polyline PL = new Polyline(4);
                    PL.Add(Domain.Min, 0, 0);
                    PL.Add(Domain.Min, -FloorLength, 0);
                    PL.Add(Domain.Max, -FloorLength, 0);
                    PL.Add(Domain.Max, 0, 0);
                    Result.Content.Add(PL.ToNurbsCurve());
                    return new[] { Result };
                }
            }

            public class VerandaGlass : WallAttachment
            {
                public Interval Domain;
                public double FloorThickness;
                public double FloorLength;
                public double SideThickness;
                public double SideHeight;
                public double HandrailHeight=1200;
                public double HandrailSpace=1000;

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("VerandaGlass");
                    Result.Add("Floor", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, Domain, new Interval(-FloorLength, 0), new Interval(-FloorThickness, 0))) });
                    Result.Add(Providers.GetHandrailGlass(new Line(Domain.Min, -FloorLength, 0, Domain.Max, -FloorLength, 0).ToNurbsCurve(),HandrailHeight,HandrailSpace));
                    if (SideThickness != 0 && SideHeight!=0)
                    {
                        Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Min, Domain.Min + SideThickness), new Interval(-FloorLength, 0), new Interval(0, SideHeight))) });
                        Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Max - SideThickness, Domain.Max), new Interval(-FloorLength, 0), new Interval(0, SideHeight))) });
                    }
                    return Result;
                }

                public VerandaGlass(Interval Domain, double FloorThickness, double FloorLength, double SideThickness = 0, double SideHeight=0)
                {
                    this.Domain = Domain;
                    this.FloorLength = FloorLength;
                    this.FloorThickness = FloorThickness;
                    this.SideThickness = SideThickness;
                    this.SideHeight = SideHeight;
                }

                public WallAttachment Duplicate()
                {
                    return new VerandaGlass(this.Domain, this.FloorThickness, this.FloorLength, this.SideThickness,this.SideHeight);
                }

                public PlanObject.Member[] GetPlan()
                {
                    PlanObject.Member Result = new PlanObject.Member("Veranda");
                    Polyline PL = new Polyline(4);
                    PL.Add(Domain.Min, 0, 0);
                    PL.Add(Domain.Min, -FloorLength, 0);
                    PL.Add(Domain.Max, -FloorLength, 0);
                    PL.Add(Domain.Max, 0, 0);
                    Result.Content.Add(PL.ToNurbsCurve());
                    return new[] { Result };
                }

            }

            public class VerandaGlassSimple : WallAttachment
            {
                public Interval Domain;
                public double FloorThickness;
                public double FloorLength;
                public double SideThickness;
                public double SideHeight;
                public double HandrailHeight = 1200;
                public double HandrailSpace = 1000;

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("VerandaGlass");
                    Result.Add("Floor", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, Domain, new Interval(-FloorLength, 0), new Interval(-FloorThickness, 0))) });
                    Result.Add(Providers.GetHandrailGlassSimple(new Line(Domain.Min, -FloorLength, 0, Domain.Max, -FloorLength, 0).ToNurbsCurve(), HandrailHeight, HandrailSpace));
                    if (SideThickness != 0 && SideHeight != 0)
                    {
                        Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Min, Domain.Min + SideThickness), new Interval(-FloorLength, 0), new Interval(0, SideHeight))) });
                        Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Max - SideThickness, Domain.Max), new Interval(-FloorLength, 0), new Interval(0, SideHeight))) });
                    }
                    return Result;
                }

                public VerandaGlassSimple(Interval Domain, double FloorThickness, double FloorLength, double SideThickness = 0, double SideHeight = 0)
                {
                    this.Domain = Domain;
                    this.FloorLength = FloorLength;
                    this.FloorThickness = FloorThickness;
                    this.SideThickness = SideThickness;
                    this.SideHeight = SideHeight;
                }

                public WallAttachment Duplicate()
                {
                    return new VerandaGlass(this.Domain, this.FloorThickness, this.FloorLength, this.SideThickness, this.SideHeight);
                }

                public PlanObject.Member[] GetPlan()
                {
                    PlanObject.Member Result = new PlanObject.Member("Veranda");
                    Polyline PL = new Polyline(4);
                    PL.Add(Domain.Min, 0, 0);
                    PL.Add(Domain.Min, -FloorLength, 0);
                    PL.Add(Domain.Max, -FloorLength, 0);
                    PL.Add(Domain.Max, 0, 0);
                    Result.Content.Add(PL.ToNurbsCurve());
                    return new[] { Result };
                }

            }

            public class VerandaGlassSimpleWide : WallAttachment
            {
                public Interval Domain;
                public double FloorThickness;
                public double FloorLength;
                public Interval DomainVeranda;
                public bool SideExist1;
                public bool SideExist2;
                public double HandrailHeight = 1200;
                public double HandrailSpace = 1000;

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("VerandaGlass");

                    Polyline FloorBase = new Polyline();
                    FloorBase.Add(Domain.Min, 0, 0);
                    FloorBase.Add(DomainVeranda.Min, -FloorLength, 0);
                    FloorBase.Add(DomainVeranda.Max, -FloorLength, 0);
                    FloorBase.Add(Domain.Max, 0, 0);
                    FloorBase.Add(Domain.Min, 0, 0);


                    Result.Add("Floor", GeneralHelper.CreateExtrusionCaped(new Curve[] { FloorBase.ToNurbsCurve() }, -Vector3d.ZAxis * FloorThickness));
                    Result.Add(Providers.GetHandrailGlassSimple(new Line(DomainVeranda.Min, -FloorLength, 0, DomainVeranda.Max, -FloorLength, 0).ToNurbsCurve(), HandrailHeight, HandrailSpace));
                    if (SideExist1)
                    {
                        Result.Add(Providers.GetHandrailGlassSimple(new Line(DomainVeranda.Min, -FloorLength, 0, Domain.Min,0,0).ToNurbsCurve(), HandrailHeight, HandrailSpace));
                    }
                    if (SideExist2)
                    {
                        Result.Add(Providers.GetHandrailGlassSimple(new Line(DomainVeranda.Max, -FloorLength, 0, Domain.Max, 0, 0).ToNurbsCurve(), HandrailHeight, HandrailSpace));
                    }
                    return Result;
                }

                public VerandaGlassSimpleWide(Interval Domain, Interval DomainVeranda, double FloorThickness, double FloorLength, bool SideExist1 = true, bool SideExist2 = true)
                {
                    this.Domain = Domain;
                    this.DomainVeranda = DomainVeranda;
                    this.FloorLength = FloorLength;
                    this.FloorThickness = FloorThickness;
                    this.SideExist1 = SideExist1;
                    this.SideExist2 = SideExist2;
                }

                public WallAttachment Duplicate()
                {
                    return new VerandaGlassSimpleWide(this.Domain, this.DomainVeranda, this.FloorThickness, this.FloorLength, this.SideExist1,this.SideExist2);
                }

                public PlanObject.Member[] GetPlan()
                {
                    PlanObject.Member Result = new PlanObject.Member("Veranda");
                    Polyline PL = new Polyline();
                    if (SideExist1) { PL.Add(Domain.Min, 0, 0); }
                    PL.Add(DomainVeranda.Min, -FloorLength, 0);
                    PL.Add(DomainVeranda.Max, -FloorLength, 0);
                    if (SideExist2) { PL.Add(Domain.Max, 0, 0); }
                    Result.Content.Add(PL.ToNurbsCurve());
                    return new[] { Result };
                }

            }


            public interface Window
            {
                Point2d StartPoint { get; set; }
                double Width { get; set; }
                double Height { get; set; }

                RealObject.Building GetBuilding();
                Window Duplicate();
                PlanObject.Member[] GetPlan(double WallThickness);

            }

            public class ElevatorSimple : Window
            {
                public Point2d StartPoint { get; set; }
                public double Width { get; set; }
                public double Height { get; set; }
                public double ElevatorWidth { get; set; }
                public double ElevatorLength { get; set; }
                public double ElevatorHeight { get; set; }

                public bool IsFront = true;

                public ElevatorSimple(Point2d StartPoint, double Width, double Height, bool IsFrone,double ElevatorWidth,double ElevatorLength,double ElevatorHeight)
                {
                    this.StartPoint = StartPoint;
                    this.Width = Width;
                    this.Height = Height;
                    this.IsFront = IsFrone;
                    this.ElevatorWidth = ElevatorWidth;
                    this.ElevatorLength = ElevatorLength;
                    this.ElevatorHeight = ElevatorHeight;
                }

                public Window Duplicate()
                {
                    ElevatorSimple Result = new ElevatorSimple(StartPoint, Width, Height, IsFront, ElevatorWidth, ElevatorLength, ElevatorHeight);
                    return Result;
                }

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("Elevator");

                    if (IsFront)
                    {
                        Result.Add("Door", Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(StartPoint.X, StartPoint.X + Width / 2), new Interval(-100, 0), new Interval(StartPoint.Y, StartPoint.Y + Height))));
                        Result.Add("Door", Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(StartPoint.X + Width / 2, StartPoint.X + Width), new Interval(-100, 0), new Interval(StartPoint.Y, StartPoint.Y + Height))));
                    }
                    else
                    {
                        Result.Add("Door", Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(StartPoint.X, StartPoint.X + Width / 2), new Interval(0, 100), new Interval(StartPoint.Y, StartPoint.Y + Height))));
                        Result.Add("Door", Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(StartPoint.X + Width / 2, StartPoint.X + Width), new Interval(0, 100), new Interval(StartPoint.Y, StartPoint.Y + Height))));
                    }
                    return Result;
                }

                public PlanObject.Member[] GetPlan(double WallThickness) {
                    PlanObject.Member Result = new PlanObject.Member("Elevator");
                    if (IsFront) {
                        Point3d A = new Point3d(StartPoint.X + Width / 2 - ElevatorWidth / 2, -WallThickness * 1.5, 0);
                        Point3d B = A + Vector3d.XAxis * ElevatorWidth;
                        Point3d C = A - Vector3d.YAxis * ElevatorLength;
                        Point3d D = B - Vector3d.YAxis * ElevatorLength;
                        Point3d E = A + Vector3d.XAxis * ElevatorWidth / 2 - Vector3d.YAxis * ElevatorLength / 2;
                        Result.Content.Add(Providers.GetRectangle3d(A, ElevatorWidth, ElevatorLength));
                        Result.Content.Add(new Line(A, A + Vector3d.XAxis * ElevatorWidth / 4 - Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());
                        Result.Content.Add(new Line(B, B - Vector3d.XAxis * ElevatorWidth / 4 - Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());
                        Result.Content.Add(new Line(C, C + Vector3d.XAxis * ElevatorWidth / 4 + Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());
                        Result.Content.Add(new Line(D, D - Vector3d.XAxis * ElevatorWidth / 4 + Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());

                        Curve[] Cvs = GetEVText();
                        double ScaleFactor = Math.Min(ElevatorWidth / 3 / 3.0, ElevatorLength / 3 / 2);
                        foreach (Curve cv in Cvs)
                        {
                            cv.Rotate(Math.PI, Vector3d.ZAxis, Point3d.Origin);
                            cv.Scale(ScaleFactor);
                            cv.Translate((Vector3d)E);
                            Result.Content.Add(cv);
                        }

                    }
                    else
                    {
                        Point3d A = new Point3d(StartPoint.X + Width / 2 - ElevatorWidth / 2, WallThickness * 1.5, 0);
                        Point3d B = A + Vector3d.XAxis * ElevatorWidth;
                        Point3d C = A + Vector3d.YAxis * ElevatorLength;
                        Point3d D = B + Vector3d.YAxis * ElevatorLength;
                        Point3d E = A + Vector3d.XAxis * ElevatorWidth / 2 + Vector3d.YAxis * ElevatorLength / 2;
                        Result.Content.Add(Providers.GetRectangle3d(A, ElevatorWidth, ElevatorLength));
                        Result.Content.Add(new Line(A, A + Vector3d.XAxis * ElevatorWidth / 4 + Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());
                        Result.Content.Add(new Line(B, B - Vector3d.XAxis * ElevatorWidth / 4 + Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());
                        Result.Content.Add(new Line(C, C + Vector3d.XAxis * ElevatorWidth / 4 - Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());
                        Result.Content.Add(new Line(D, D - Vector3d.XAxis * ElevatorWidth / 4 - Vector3d.YAxis * ElevatorLength / 4).ToNurbsCurve());

                        Curve[] Cvs = GetEVText();
                        double ScaleFactor = Math.Min(ElevatorWidth / 3 / 3.0, ElevatorLength / 3 / 2);
                        foreach (Curve cv in Cvs)
                        {
                            cv.Scale(ScaleFactor);
                            cv.Translate((Vector3d)E);
                            Result.Content.Add(cv);
                        }
                    }
                    return new[] { Result };

                }

                public static Curve[] GetEVText()
                {
                    var pls = new List<Curve>();
                    {
                        Polyline pl = new Polyline();
                        pl.Add(-0.5, -1, 0);
                        pl.Add(-1.5, -1, 0);
                        pl.Add(-1.5, 1, 0);
                        pl.Add(-0.5, 1, 0);
                        pls.Add(pl.ToNurbsCurve());
                    }
                    {
                        Polyline pl = new Polyline();
                        pl.Add(-1.5, 0, 0);
                        pl.Add(-0.5, 0, 0);
                        pls.Add(pl.ToNurbsCurve());
                    }

                    {
                        Polyline pl = new Polyline();
                        pl.Add(0.5, 1, 0);
                        pl.Add(1, -1, 0);
                        pl.Add(1.5, 1, 0);
                        pls.Add(pl.ToNurbsCurve());
                    }
                    return pls.ToArray();
                }

            }

            public class DoorSimple : Window
            {
                public Point2d StartPoint { get; set; }
                public double Width { get; set; }
                public double Height { get; set; }
                public Side OpenSide = Side.FrontRight;

                public DoorSimple(Point2d StartPoint, double Width, double Height,Side OpenSide)
                {
                    this.StartPoint = StartPoint;
                    this.Width = Width;
                    this.Height = Height;
                    this.OpenSide=OpenSide;
                }

                public enum Side{
                    FrontRight,FrontLeft,BackRight,BackLeft

                }

                public Window Duplicate()
                {
                    DoorSimple Result = new DoorSimple(StartPoint,Width,Height,OpenSide);
                    return Result;
                }

                public RealObject.Building GetBuilding() {
                    RealObject.Building Door = new RealObject.Building("Door");

                    Polyline PL = new Polyline(5);
                    PL.Add(StartPoint.X, 0, StartPoint.Y);
                    PL.Add(StartPoint.X + Width, 0, StartPoint.Y);
                    PL.Add(StartPoint.X + Width, 0, StartPoint.Y + Height);
                    PL.Add(StartPoint.X, 0, StartPoint.Y + Height);
                    PL.Add(StartPoint.X, 0, StartPoint.Y);

                    Door.Add("Body", Brep.CreatePlanarBreps(PL.ToNurbsCurve()));

                    return Door;
                }
                public PlanObject.Member[] GetPlan(double WallThickness)
                {
                    PlanObject.Member Result = new PlanObject.Member("Door");
                    switch (this.OpenSide)
                    {
                        case Side.FrontLeft:
                            Result.Content.Add(new Line(StartPoint.X, 0, 0, StartPoint.X, -Width, 0).ToNurbsCurve());
                            Result.Content.Add(new Arc(new Circle(new Point3d(StartPoint.X, 0, 0), Width),new Interval(-Math.PI/2,0)).ToNurbsCurve());
                            return new[]{Result};
                        case Side.FrontRight:
                            Result.Content.Add(new Line(StartPoint.X+Width, 0, 0, StartPoint.X+Width, -Width, 0).ToNurbsCurve());
                            Result.Content.Add(new Arc(new Circle(new Point3d(StartPoint.X + Width, 0, 0), Width), new Interval(-Math.PI, -Math.PI / 2)).ToNurbsCurve());
                            return new[] { Result };
                        case Side.BackLeft:
                            Result.Content.Add(new Line(StartPoint.X, WallThickness, 0, StartPoint.X, WallThickness+Width, 0).ToNurbsCurve());
                            Result.Content.Add(new Arc(new Circle(new Point3d(StartPoint.X, WallThickness, 0), Width), new Interval(0, Math.PI / 2)).ToNurbsCurve());
                            return new[] { Result };
                        case Side.BackRight:
                            Result.Content.Add(new Line(StartPoint.X + Width, WallThickness, 0, StartPoint.X + Width, WallThickness + Width, 0).ToNurbsCurve());
                            Result.Content.Add(new Arc(new Circle(new Point3d(StartPoint.X + Width, WallThickness, 0), Width), new Interval(Math.PI / 2, Math.PI)).ToNurbsCurve());
                            return new[] { Result };
                        default:
                            return new[] { Result };
                    }
                }

                
            }


            public class WindowGlassSimpleSingle : Window
            {
                public Point2d StartPoint { get; set; }
                public double Width { get; set; }
                public double Height { get; set; }

                public double FrameWidth = 30;
                public double FrameThickness = 30;
                public double GlassThickness = 5;

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("WindowSingle");
                    Brep[] breps = new Brep[2];

                    Providers.GetGlassSimple(Width, Height, FrameWidth, FrameThickness, GlassThickness, out breps[0], out breps[1]);
                    Result.Add("Glass", new Brep[] { breps[1] });
                    Result.Add("Frame", new Brep[] { breps[0] });

                    Result.Transform(Transform.Translation(StartPoint.X, FrameThickness, StartPoint.Y));

                    return Result;
                }

                public WindowGlassSimpleSingle(Point2d StartPoint, double Width, double Height)
                {
                    this.StartPoint = StartPoint;
                    this.Width = Width;
                    this.Height = Height;
                }

                public Window Duplicate()
                {
                    return new WindowGlassSimpleSingle(this.StartPoint, this.Width, this.Height)
                    {
                        FrameWidth = this.FrameWidth,
                        FrameThickness = this.FrameThickness,
                        GlassThickness = this.GlassThickness
                    };
                }

                public PlanObject.Member[] GetPlan(double WallThickness)
                {
                    PlanObject.Member Result = new PlanObject.Member("WindowSingle");
                    Result.Content.Add(new Line(StartPoint.X, 0, 0, StartPoint.X, WallThickness, 0).ToNurbsCurve());
                    Result.Content.Add(new Line(StartPoint.X + Width, 0, 0, StartPoint.X + Width, WallThickness, 0).ToNurbsCurve());
                    return new[] { Result };
                }

            }

            public class WindowGlassSimpleDouble : Window
            {
                public Point2d StartPoint { get; set; }
                public double Width { get; set; }
                public double Height { get; set; }

                public double FrameWidth = 30;
                public double FrameThickness = 30;
                public double GlassThickness = 5;

                public RealObject.Building GetBuilding()
                {
                    RealObject.Building Result = new RealObject.Building("WindowDouble");
                    Brep[] breps = new Brep[2];

                    Providers.GetGlassSimple(Width/2.0, Height, FrameWidth, FrameThickness, GlassThickness, out breps[0], out breps[1]);
                    Result.Add("Glass", new Brep[] { breps[1] });
                    Result.Add("Glass", GeneralHelper.TranslateBreps(new Brep[] { breps[1] }, new Point3d(Width / 2.0, FrameThickness, 0)));
                    Result.Add("Frame", new Brep[] { breps[0] });
                    Result.Add("Frame", GeneralHelper.TranslateBreps(new Brep[] { breps[0] }, new Point3d(Width / 2.0, FrameThickness, 0)));

                    Result.Transform(Transform.Translation(StartPoint.X, FrameThickness, StartPoint.Y));

                    return Result;
                }

                public WindowGlassSimpleDouble(Point2d StartPoint, double Width, double Height)
                {
                    this.StartPoint = StartPoint;
                    this.Width = Width;
                    this.Height = Height;
                }

                public Window Duplicate()
                {
                    return new WindowGlassSimpleDouble(this.StartPoint, this.Width, this.Height)
                    {
                        FrameWidth = this.FrameWidth,
                        FrameThickness = this.FrameThickness,
                        GlassThickness = this.GlassThickness
                    };
                }

                public PlanObject.Member[] GetPlan(double WallThickness)
                {
                    PlanObject.Member Result = new PlanObject.Member("WindowDouble");
                    Result.Content.Add(new Line(StartPoint.X, 0, 0, StartPoint.X, WallThickness, 0).ToNurbsCurve());
                    Result.Content.Add(new Line(StartPoint.X + Width, 0, 0, StartPoint.X + Width, WallThickness, 0).ToNurbsCurve());
                    Result.Content.Add(new Line(StartPoint.X, WallThickness/3, 0, StartPoint.X + Width*3/4, WallThickness/3, 0).ToNurbsCurve());
                    Result.Content.Add(new Line(StartPoint.X+Width/4, WallThickness *2/ 3, 0, StartPoint.X + Width , WallThickness*2 / 3, 0).ToNurbsCurve());
                    Result.Content.Add(new Line(StartPoint.X + Width / 2, -WallThickness/2, 0, StartPoint.X + Width / 2, WallThickness*3/2, 0).ToNurbsCurve());
                    return new[] { Result };
                }
            }

        }

    }
}
