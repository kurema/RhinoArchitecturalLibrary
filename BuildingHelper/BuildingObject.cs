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
    /// 建築を示すオブジェクト等を含みます。
    /// </summary>
    public class BuildingObject
    {
        /// <summary>
        /// 3Dモデルを生成可能です。
        /// </summary>
        public interface BuildingObjectProvider
        {
            /// <summary>
            /// 建築の3Dモデルを取得します。
            /// </summary>
            /// <returns>建築の3Dモデル</returns>
            RealObject.Building GetBuilding();
        }

        /// <summary>
        /// 3Dモデルと平面図を生成可能です。
        /// </summary>
        public interface Building : BuildingObjectProvider
        {
            /// <summary>
            /// 建築の平面図全体を取得します。
            /// </summary>
            /// <returns>建築の平面図全体</returns>
            PlanObject.Building GetPlan();
        }

        /// <summary>
        /// 複数階層を保持している建築を示します。
        /// 比較的単純な建築に向きます。スキップフロアなどには向きません。
        /// </summary>
        public class BuildingMultipleFloor : Building
        {
            /// <summary>
            /// 建築に含まれる階層の集合を示します。
            /// 要素の番号が0から始まる事に注意してください。
            /// 地階を含む場合など注意が必要です。
            /// </summary>
            public List<FloorGeneral> Content = new List<FloorGeneral>();
            /// <summary>
            /// 建築の名前です。レイヤーの設定などに利用されます。
            /// </summary>
            public string Name = "Building";

            /// <summary>
            /// クラスの新しいインスタンスを指定した名前で設定します。
            /// </summary>
            /// <param name="Name">建築の名前</param>
            public BuildingMultipleFloor(string Name)
            {
                this.Name = Name;
            }

            /// <summary>
            /// 建築全体の3Dモデルを取得します。
            /// </summary>
            /// <returns>建築全体の3Dモデル</returns>
            public RealObject.Building GetBuilding()
            {
                RealObject.Building Result = new RealObject.Building(this.Name);
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

            /// <summary>
            /// 建築全体の軽量な3Dモデルを取得します。遠景用など。
            /// </summary>
            /// <returns>建築全体の3Dモデル</returns>
            public RealObject.Building GetBuildingLight()
            {
                RealObject.Building Result = new RealObject.Building(this.Name);
                double Height = 0;
                for (int i = 0; i < Content.Count(); i++)
                {
                    FloorGeneral item = Content[i];


                    List<Brep> Exts = new List<Brep>();
                    Curve[] Cvs = item.GetOuterLine();
                    foreach (Curve Cv in Cvs)
                    {
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

            /// <summary>
            /// 断面図を取得します。
            /// </summary>
            /// <param name="l">切断線</param>
            /// <returns>断面図</returns>
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

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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

        /// <summary>
        /// 比較的単純な建築の一階層を保持します。
        /// </summary>
        public class FloorGeneral : BuildingObjectProvider
        {
            /// <summary>
            /// 階層に含まれる壁面の集合を示します。
            /// </summary>
            public List<Wall> Walls = new List<Wall>();
            /// <summary>
            /// 階層に含まれる壁面以外の3Dオブジェクトを示します。
            /// </summary>
            public List<RealObject.Building> Objects = new List<RealObject.Building>();
            /// <summary>
            /// 階層の高さを示します。
            /// </summary>
            public double Height = 0;
            /// <summary>
            /// 階層名です。
            /// </summary>
            public string Name = "Floor";
            /// <summary>
            /// 天井の厚さを示します。
            /// </summary>
            public double CeilingThick = 300;
            /// <summary>
            /// 床の地表面からの高さを示します。
            /// </summary>
            public double GroundOffset = 200;

            /// <summary>
            /// クラスの新しいインスタンスを指定した名前で生成します。
            /// </summary>
            /// <param name="Name">階層の名前</param>
            public FloorGeneral(string Name)
            {
                this.Name = Name;
            }

            /// <summary>
            /// 階層の3Dモデルを取得します。
            /// </summary>
            /// <returns>階層の3Dモデル</returns>
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

            /// <summary>
            /// 階層の輪郭線から天井を取得します。
            /// </summary>
            /// <returns>天井</returns>
            public RealObject.Member GetCeiling()
            {
                Brep[] CeilBase = Brep.CreatePlanarBreps(GetOuterLine());
                return new RealObject.Member("Ceiling", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, Height - CeilingThick)));
            }
            /// <summary>
            /// 階層の輪郭線から床を取得します。
            /// </summary>
            /// <returns>床</returns>
            public RealObject.Member GetFloor()
            {
                Brep[] CeilBase = Brep.CreatePlanarBreps(GetOuterLine());
                return new RealObject.Member("Floor", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, 1)));
            }
            /// <summary>
            /// 階層の輪郭線から屋根を取得します。
            /// 階層が最上階の場合に利用するのが望ましいです。
            /// </summary>
            /// <returns>屋根</returns>
            public RealObject.Member GetLoof()
            {
                Brep[] CeilBase = Brep.CreatePlanarBreps(GetOuterLine());
                return new RealObject.Member("Loof", GeneralHelper.TranslateBreps(CeilBase, new Vector3d(0, 0, Height)));
            }

            /// <summary>
            /// 階層の輪郭線を取得します。
            /// </summary>
            /// <returns>輪郭線</returns>
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

            /// <summary>
            /// 階層の平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            public PlanObject.Floor GetPlan()
            {
                PlanObject.Floor Result = new PlanObject.Floor("Floor");
                foreach (Wall w in this.Walls)
                {
                    Result.Content.AddRange(w.GetPlan());
                }
                return Result;
            }

            /// <summary>
            /// 階層に壁を追加します。
            /// </summary>
            /// <param name="item">壁</param>
            public void Add(params Wall[] item)
            {
                Walls.AddRange(item);
            }

            /// <summary>
            /// 階層に3Dオブジェクトを追加します。
            /// </summary>
            /// <param name="item">3Dオブジェクト</param>
            public void Add(params RealObject.Building[] item)
            {
                Objects.AddRange(item);
            }
        }

        /// <summary>
        /// 同種の階層のみからなる建築を示します。
        /// </summary>
        public class BuildingSingleFloor : Building
        {
            /// <summary>
            /// 全階層に含まれる壁の集合です。
            /// </summary>
            public List<Wall> Walls = new List<Wall>();
            /// <summary>
            /// 全階層に含まれる3Dオブジェクトを示します。
            /// </summary>
            public List<RealObject.Building> Objects = new List<RealObject.Building>();
            /// <summary>
            /// 建築の階数です。
            /// </summary>
            public int Floor = 1;
            /// <summary>
            /// 一階層当たりの高さを示します。
            /// </summary>
            public double Height;
            /// <summary>
            /// 建築の名前を示します。
            /// </summary>
            public string Name = "Building";
            /// <summary>
            /// 天井の厚さを示します。
            /// </summary>
            public double CeilingThick = 300;
            /// <summary>
            /// 建築の設置高さを示します。
            /// </summary>
            public double GroundOffset = 200;

            /// <summary>
            /// クラスの新しいインスタンスを指定した名前で生成します。
            /// </summary>
            /// <param name="Name">建築の名前</param>
            public BuildingSingleFloor(string Name)
            {
                this.Name = Name;
            }
            /// <summary>
            /// クラスの新しいインスタンスを生成します。
            /// </summary>
            public BuildingSingleFloor()
            {
            }

            /// <summary>
            /// 建築全体の3Dモデルを取得します。
            /// </summary>
            /// <returns>3Dモデル</returns>
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
                            newMem.Transform(Transform.Translation(0, 0, GroundOffset + Height * i));
                            Result.Add(newMem);
                        }
                    }

                    RealObject.Member newLoof = Loof.Duplicate();
                    newLoof.Transform(Transform.Translation(0, 0, GroundOffset + Height * i));
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
                        newMem.Transform(Transform.Translation(0, 0, GroundOffset + Height * (Floor - 1)));
                        Result.Add(newMem);
                    }
                }

                return Result;
            }

            /// <summary>
            /// 建築全体の平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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

            /// <summary>
            /// 各階に壁面を追加します。
            /// </summary>
            /// <param name="item">壁</param>
            public void Add(params Wall[] item)
            {
                Walls.AddRange(item);
            }

            /// <summary>
            /// 各階に3Dオブジェクトを追加します。
            /// </summary>
            /// <param name="item">3Dオブジェクト</param>
            public void Add(params RealObject.Building[] item)
            {
                Objects.AddRange(item);
            }

            /// <summary>
            /// 各階の輪郭線を取得します。
            /// </summary>
            /// <returns>輪郭線</returns>
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

        /// <summary>
        /// 定義済みの建築作成クラスを提供します。
        /// </summary>
        public static class BuildingProvider
        {
            /// <summary>
            /// 適当なアパートメントを提供します。
            /// </summary>
            /// <param name="UnitX">ユニットのX方向の大きさ</param>
            /// <param name="UnitY">ユニットのY方向の大きさ</param>
            /// <param name="UnitZ">ユニットのZ方向の大きさ</param>
            /// <param name="UnitCountX">X方向の個数</param>
            /// <param name="UnitCountZ">Z方向の個数(階数)</param>
            /// <param name="CeilingThick">天井の高さ</param>
            /// <param name="WallThick">壁の厚さ</param>
            /// <returns>建築</returns>
            public static BuildingObject.Building GetApartment(double UnitX, double UnitY, double UnitZ, int UnitCountX, int UnitCountZ, double CeilingThick = 300, double WallThick = 0)
            {
                BuildingObject.BuildingSingleFloor Result = new BuildingObject.BuildingSingleFloor();
                Result.Height = UnitZ;
                Result.Name = "Apartment";
                Result.CeilingThick = CeilingThick;
                Result.Floor = UnitCountZ;

                WallGeneral WallSouth = new WallGeneral(Point3d.Origin, new Vector2d(UnitX, 0), UnitZ, WallThick);
                WallSouth.Add(new VerandaSimple(new Interval(0, UnitX), 100, 2000, 100, 1000, 150) { SideHeight1 = UnitZ, SideHeight2 = UnitZ });
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
                    WallNorth.Add(new DoorSimple(new Point2d(UnitX * i + 500, 0), 1000, 2500, DoorSimple.Side.FrontRight));
                }
                Result.Add(WallNorth);

                return Result;
            }
        }

        /// <summary>
        /// 階段を識別するためのインターフェースです。
        /// </summary>
        public interface Stair : BuildingObjectProvider
        {
        }

        /// <summary>
        /// 壁を示します。
        /// </summary>
        public interface Wall : BuildingObjectProvider
        {
            /// <summary>
            /// 最上階における建築の3Dモデルを取得します。
            /// </summary>
            /// <returns>建築の3Dモデル</returns>
            RealObject.Building GetBuildingTop();
            /// <summary>
            /// 壁を指定された方向に移動します。
            /// </summary>
            /// <param name="v3d">移動方向</param>
            void Translate(Vector3d v3d);
            /// <summary>
            /// 壁を指定された角度回転します。
            /// </summary>
            /// <param name="angle">角度</param>
            void Rotate(double angle);
            /// <summary>
            /// 平面図で壁面を示す形状を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            Curve[] GetCurves();
            /// <summary>
            /// 壁の内側を示す線です。
            /// </summary>
            /// <returns>線</returns>
            Curve GetLineIn();
            /// <summary>
            /// 壁の外側を示す線です。
            /// </summary>
            /// <returns>線</returns>
            Curve GetLineOut();
            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            Wall Duplicate();
            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            PlanObject.Member[] GetPlan();
        }

        /// <summary>
        /// 連続した壁です。
        /// </summary>
        public class WallRepeat : Wall
        {
            /// <summary>
            /// 基本となる壁です。
            /// </summary>
            public Wall Content;
            /// <summary>
            /// 繰り返し回数です。
            /// </summary>
            public int Count;
            /// <summary>
            /// 繰り返される向きです。
            /// </summary>
            public Vector3d Direction { get { if (Content is WallGeneral) { Vector2d v2d = ((WallGeneral)Content).Direction; return new Vector3d(v2d.X, v2d.Y, 0); } else { return _Direction; } } set { if (!(Content is WallGeneral)) { _Direction = value; } } }
            private Vector3d _Direction;

            /// <summary>
            /// クラスの新しいインスタンスを基本となる壁と長さから生成します。
            /// </summary>
            /// <param name="Content">基本となる壁</param>
            /// <param name="Count">繰り返し回数</param>
            public WallRepeat(WallGeneral Content, int Count)
            {
                this.Content = Content;
                this.Count = Count;
            }

            /// <summary>
            /// クラスの新しいインスタンスを基本となる壁と長さと方向から生成します。
            /// </summary>
            /// <param name="Content">基本となる壁</param>
            /// <param name="Count">長さ</param>
            /// <param name="Direction">繰り返し方向</param>
            public WallRepeat(Wall Content, int Count, Vector3d Direction)
            {
                this.Content = Content;
                this.Count = Count;
                this.Direction = Direction;
            }

            /// <summary>
            /// 構成する壁を取得します。
            /// </summary>
            /// <returns>壁の集合</returns>
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

            /// <summary>
            /// 壁面を建築として取得します。
            /// </summary>
            /// <returns>建築</returns>
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

            /// <summary>
            /// 最上階の場合の壁面を建築として取得します。
            /// </summary>
            /// <returns>建築</returns>
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

            /// <summary>
            /// 壁を指定された方向に移動します。
            /// </summary>
            /// <param name="v3d">移動方向</param>
            public void Translate(Vector3d v3d)
            {
                Content.Translate(v3d);
            }

            /// <summary>
            /// 壁を指定された角度回転します。
            /// </summary>
            /// <param name="angle">角度</param>
            public void Rotate(double angle)
            {
                Content.Rotate(angle);
            }

            /// <summary>
            /// 平面図で壁面を示す形状を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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

            /// <summary>
            /// 壁の内側を示す線です。
            /// </summary>
            /// <returns>線</returns>
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

            /// <summary>
            /// 壁の外側を示す線です。
            /// </summary>
            /// <returns>線</returns>
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

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public Wall Duplicate()
            {
                return new WallRepeat(this.Content, this.Count, this.Direction);
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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

        /// <summary>
        /// 基本的な直方体の壁を示します。
        /// </summary>
        public class WallGeneral : Wall
        {
            /// <summary>
            /// 壁の始点です。
            /// </summary>
            public Point3d StartPoint;
            /// <summary>
            /// 壁の方向です。
            /// </summary>
            public Vector2d Direction;
            /// <summary>
            /// 壁の高さです。
            /// </summary>
            public double Height;
            /// <summary>
            /// 壁の厚さです。
            /// 厚さは壁の方向を90°回転させた向きを正とします。
            /// </summary>
            public double Thickness;
            /// <summary>
            /// 壁の終点です。
            /// </summary>
            public Point3d EndPoint { get { return StartPoint + new Vector3d(Direction.X, Direction.Y, Height); } set { Vector3d v3d = value - StartPoint; Direction = new Vector2d(v3d.X, v3d.Y); Height = v3d.Z; } }
            /// <summary>
            /// trueの場合、3Dモデルを生成する際に、壁の厚さを0とみなします。
            /// </summary>
            public bool ApplyZeroThicknessToBrep = false;
            /// <summary>
            /// trueの場合、平面図では表示しません。
            /// </summary>
            public bool OmitFromFloorLine = false;

            /// <summary>
            /// 壁面に含まれる窓の集合です。
            /// </summary>
            public List<BuildingObject.Window> Windows = new List<BuildingObject.Window>();
            /// <summary>
            /// 壁面を構成するその他の添加物の集合です。
            /// </summary>
            public List<BuildingObject.WallAttachment> Attachments = new List<BuildingObject.WallAttachment>();
            /// <summary>
            /// 最上階の場合、壁面を構成するその他の添加物の集合です。
            /// </summary>
            public List<BuildingObject.WallAttachment> AttachmentsTop = new List<BuildingObject.WallAttachment>();

            /// <summary>
            /// 壁面を建築として取得します。
            /// </summary>
            /// <returns>建築</returns>
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

            /// <summary>
            /// 最上階の場合の壁面を建築として取得します。
            /// </summary>
            /// <returns>建築</returns>
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

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public Wall Duplicate()
            {
                WallGeneral Result = new WallGeneral(this.StartPoint, this.Direction, this.Height, this.Thickness);

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

            /// <summary>
            /// 壁の外側を示す線です。
            /// </summary>
            /// <returns>線</returns>
            public Curve GetLineOut()
            {
                if (OmitFromFloorLine)
                {
                    return null;
                }
                else
                {
                    return new Line(StartPoint, StartPoint + new Vector3d(Direction.X, Direction.Y, 0)).ToNurbsCurve();
                }
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <returns>壁</returns>
            public Curve GetLineIn()
            {
                Vector3d V3dNormal = new Vector3d(-Direction.Y, Direction.X, 0);
                V3dNormal.Unitize();

                Vector3d V3dBase = new Vector3d(Direction.X, Direction.Y, 0);
                double BaseLen = V3dBase.Length;
                V3dBase.Unitize();

                return new Line(StartPoint + V3dNormal * Thickness, StartPoint + V3dNormal * Thickness + V3dBase * BaseLen).ToNurbsCurve();
            }

            /// <summary>
            /// 平面図で壁面を示す四角形を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            public Curve[] GetCurves()
            {
                List<Curve> Result = new List<Curve>();

                Result.Add(new Rectangle3d(Plane.WorldZX, Point3d.Origin, new Point3d(Direction.Length, 0, Height)).ToNurbsCurve());

                foreach (BuildingObject.Window w in Windows)
                {
                    Result.Add(new Rectangle3d(new Plane(new Point3d(w.StartPoint.X, 0, w.StartPoint.Y + 0.1), Vector3d.XAxis, Vector3d.ZAxis), w.Width, w.Height).ToNurbsCurve());
                }
                return Result.ToArray();
            }
            /// <summary>
            /// クラスの新しいインスタンスを生成します。
            /// </summary>
            /// <param name="StartPoint">開始点</param>
            /// <param name="Direction">向き</param>
            /// <param name="Height">高さ</param>
            /// <param name="Thickness">厚さ</param>
            public WallGeneral(Point3d StartPoint, Vector2d Direction, double Height, double Thickness = 0)
            {
                this.StartPoint = StartPoint;
                this.Direction = Direction;
                this.Height = Height;
                this.Thickness = Thickness;
            }

            /// <summary>
            /// 窓を追加します。
            /// </summary>
            /// <param name="item">窓</param>
            public void Add(params Window[] item)
            {
                Windows.AddRange(item);
            }
            /// <summary>
            /// 壁の添加物を追加します。
            /// </summary>
            /// <param name="item">壁の添加物</param>
            public void Add(params WallAttachment[] item)
            {
                Attachments.AddRange(item);
            }

            /// <summary>
            /// 壁を指定された方向に移動します。
            /// </summary>
            /// <param name="v3d">移動方向</param>
            public void Translate(Vector3d v3d)
            {
                StartPoint += v3d;
            }

            /// <summary>
            /// 壁を指定された角度回転します。
            /// </summary>
            /// <param name="angle">角度</param>
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

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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
                    PointList.Add(item.StartPoint.X + item.Width);
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

        /// <summary>
        /// 壁の添加物です。
        /// </summary>
        public interface WallAttachment : BuildingObjectProvider
        {
            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            WallAttachment Duplicate();
            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            PlanObject.Member[] GetPlan();

        }

        /// <summary>
        /// 軒を示します。
        /// </summary>
        public class Eaves : WallAttachment
        {
            /// <summary>
            /// 軒の壁方向の範囲です。
            /// </summary>
            public Interval Domain;
            /// <summary>
            /// 軒の長さです。
            /// </summary>
            public double Length;
            /// <summary>
            /// 軒の厚さです。
            /// </summary>
            public double Thickness;
            /// <summary>
            /// 軒の高さです。
            /// </summary>
            public double Height;

            /// <summary>
            /// 3Dモデルを取得します。
            /// </summary>
            /// <returns>3Dモデル</returns>
            public RealObject.Building GetBuilding()
            {
                RealObject.Building Result = new RealObject.Building("Eaves");
                Result.Add("Body", Brep.CreateFromBox(new Box(Plane.WorldXY, this.Domain, new Interval(-Length, 0), new Interval(Height - Thickness, Height))));
                return Result;
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            public PlanObject.Member[] GetPlan()
            {
                return new PlanObject.Member[0];
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public WallAttachment Duplicate()
            {
                return new Eaves(this.Domain, this.Length, this.Thickness, this.Height);
            }

            /// <summary>
            /// クラスの新しいインスタンスを生成します。
            /// </summary>
            /// <param name="Domain">軒の壁方向の範囲(X方向)</param>
            /// <param name="Length">軒の長さ(Y方向)</param>
            /// <param name="Thickness">軒の厚さ(Z方向)</param>
            /// <param name="Height">軒の高さ(Z方向)</param>
            public Eaves(Interval Domain, double Length, double Thickness, double Height)
            {
                this.Domain = Domain;
                this.Length = Length;
                this.Thickness = Thickness;
                this.Height = Height;
            }

        }

        /// <summary>
        /// 基本的なベランダを示します。
        /// </summary>
        public class VerandaSimple : WallAttachment
        {
            /// <summary>
            /// 配置される壁における範囲
            /// </summary>
            public Interval Domain;
            /// <summary>
            /// 床の厚さ
            /// </summary>
            public double FloorThickness;
            /// <summary>
            /// 床の長さ
            /// </summary>
            public double FloorLength;
            /// <summary>
            /// 手すりの厚さ
            /// </summary>
            public double HandrailThickness;
            /// <summary>
            /// 手すりの高さ
            /// </summary>
            public double HandrailHeight;
            /// <summary>
            /// 横の手すり厚さ。Domainの最小側に配置します。0の場合は作成しません。
            /// </summary>
            public double SideThickness1;
            /// <summary>
            /// 横の手すり高さ。Domainの最小側に配置します。
            /// </summary>
            public double SideHeight1;
            /// <summary>
            /// 横の手すり厚さ。Domainの最大側に配置します。0の場合は作成しません。
            /// </summary>
            public double SideThickness2;
            /// <summary>
            /// 横の手すり高さ。Domainの最大側に配置します。
            /// </summary>
            public double SideHeight2;

            /// <summary>
            /// ベランダを建築として取得します。
            /// </summary>
            /// <returns>ベランダ</returns>
            public RealObject.Building GetBuilding()
            {
                RealObject.Building Result = new RealObject.Building("VerandaSimple");
                Result.Add("Floor", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Min + SideThickness1, Domain.Max - SideThickness2), new Interval(-FloorLength, 0), new Interval(-FloorThickness, 0))) });
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

            /// <summary>
            /// クラスの新しいインスタンスを作成します。
            /// </summary>
            /// <param name="Domain">配置される壁における範囲</param>
            /// <param name="FloorThickness">床の厚さ</param>
            /// <param name="FloorLength">床の長さ</param>
            /// <param name="HandrailThickness">手すりの厚さ</param>
            /// <param name="HandrailHeight">手すりの高さ</param>
            /// <param name="SideThickness">横の手すりの厚さ</param>
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

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public WallAttachment Duplicate()
            {
                return new VerandaSimple(this.Domain, this.FloorThickness, this.FloorLength, this.HandrailThickness, this.HandrailHeight, 0) { SideThickness1 = this.SideThickness1, SideThickness2 = this.SideThickness2, SideHeight1 = this.SideHeight1, SideHeight2 = this.SideHeight2 };
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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

        /// <summary>
        /// ガラス手すりのベランダを示します。
        /// </summary>
        public class VerandaGlass : WallAttachment
        {
            /// <summary>
            /// 配置される壁における範囲
            /// </summary>
            public Interval Domain;
            /// <summary>
            /// 床の厚さ
            /// </summary>
            public double FloorThickness;
            /// <summary>
            /// 床の長さ
            /// </summary>
            public double FloorLength;
            /// <summary>
            /// 横の手すり厚さ。0の場合は作成しません。
            /// </summary>
            public double SideThickness;
            /// <summary>
            /// 横の手すり高さ。
            /// </summary>
            public double SideHeight;
            /// <summary>
            /// 手すりの高さ
            /// </summary>
            public double HandrailHeight = 1200;
            /// <summary>
            /// 手すりのガラス一枚当たりの長さ
            /// </summary>
            public double HandrailSpace = 1000;

            /// <summary>
            /// ベランダを建築として取得します。
            /// </summary>
            /// <returns>ベランダ</returns>
            public RealObject.Building GetBuilding()
            {
                RealObject.Building Result = new RealObject.Building("VerandaGlass");
                Result.Add("Floor", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, Domain, new Interval(-FloorLength, 0), new Interval(-FloorThickness, 0))) });
                Result.Add(Providers.GetHandrailGlass(new Line(Domain.Min, -FloorLength, 0, Domain.Max, -FloorLength, 0).ToNurbsCurve(), HandrailHeight, HandrailSpace));
                if (SideThickness != 0 && SideHeight != 0)
                {
                    Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Min, Domain.Min + SideThickness), new Interval(-FloorLength, 0), new Interval(0, SideHeight))) });
                    Result.Add("Side", new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(Domain.Max - SideThickness, Domain.Max), new Interval(-FloorLength, 0), new Interval(0, SideHeight))) });
                }
                return Result;
            }

            /// <summary>
            /// クラスのインスタンスを生成します。
            /// </summary>
            /// <param name="Domain">配置される壁における範囲</param>
            /// <param name="FloorThickness">床の厚さ</param>
            /// <param name="FloorLength">床の長さ</param>
            /// <param name="SideThickness">横の手すり厚さ。0の場合は作成しません。</param>
            /// <param name="SideHeight">横の手すり高さ。</param>
            public VerandaGlass(Interval Domain, double FloorThickness, double FloorLength, double SideThickness = 0, double SideHeight = 0)
            {
                this.Domain = Domain;
                this.FloorLength = FloorLength;
                this.FloorThickness = FloorThickness;
                this.SideThickness = SideThickness;
                this.SideHeight = SideHeight;
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public WallAttachment Duplicate()
            {
                return new VerandaGlass(this.Domain, this.FloorThickness, this.FloorLength, this.SideThickness, this.SideHeight);
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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
        /// <summary>
        /// 単純なガラス手すりのベランダを示します。
        /// </summary>
        public class VerandaGlassSimple : WallAttachment
        {
            /// <summary>
            /// 配置される壁における範囲
            /// </summary>
            public Interval Domain;
            /// <summary>
            /// 床の厚さ
            /// </summary>
            public double FloorThickness;
            /// <summary>
            /// 床の長さ
            /// </summary>
            public double FloorLength;
            /// <summary>
            /// 横の手すり厚さ。0の場合は作成しません。
            /// </summary>
            public double SideThickness;
            /// <summary>
            /// 横の手すり高さ。
            /// </summary>
            public double SideHeight;
            /// <summary>
            /// 手すりの高さ
            /// </summary>
            public double HandrailHeight = 1200;
            /// <summary>
            /// 手すりのガラス一枚当たりの長さ
            /// </summary>
            public double HandrailSpace = 1000;

            /// <summary>
            /// ベランダを建築として取得します。
            /// </summary>
            /// <returns>ベランダ</returns>
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

            /// <summary>
            /// クラスの新しいインスタンスを作成します。
            /// </summary>
            /// <param name="Domain">配置される壁における範囲</param>
            /// <param name="FloorThickness">床の厚さ</param>
            /// <param name="FloorLength">床の長さ</param>
            /// <param name="SideThickness">横の手すりの厚さ</param>
            /// <param name="SideHeight">横の手すりの高さ</param>
            public VerandaGlassSimple(Interval Domain, double FloorThickness, double FloorLength, double SideThickness = 0, double SideHeight = 0)
            {
                this.Domain = Domain;
                this.FloorLength = FloorLength;
                this.FloorThickness = FloorThickness;
                this.SideThickness = SideThickness;
                this.SideHeight = SideHeight;
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public WallAttachment Duplicate()
            {
                return new VerandaGlass(this.Domain, this.FloorThickness, this.FloorLength, this.SideThickness, this.SideHeight);
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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

        /// <summary>
        /// 幅の変化する単純なガラス手すりのベランダを示します。
        /// </summary>
        public class VerandaGlassSimpleWide : WallAttachment
        {
            /// <summary>
            /// 配置される壁における範囲
            /// </summary>
            public Interval Domain;
            /// <summary>
            /// 床の厚さ
            /// </summary>
            public double FloorThickness;
            /// <summary>
            /// 床の長さ
            /// </summary>
            public double FloorLength;
            /// <summary>
            /// 外側のベランダの配置される壁に対する範囲
            /// </summary>
            public Interval DomainVeranda;
            /// <summary>
            /// 横の手すりの有無。Domainの最小側に配置するか。
            /// </summary>
            public bool SideExist1;
            /// <summary>
            /// 横の手すりの有無。Domainの最大側に配置するか。
            /// </summary>
            public bool SideExist2;
            /// <summary>
            /// 手すりの高さ
            /// </summary>
            public double HandrailHeight = 1200;
            /// <summary>
            /// 手すりのガラス一枚当たりの長さ
            /// </summary>
            public double HandrailSpace = 1000;

            /// <summary>
            /// ベランダを建築として取得します。
            /// </summary>
            /// <returns>ベランダ</returns>
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
                    Result.Add(Providers.GetHandrailGlassSimple(new Line(DomainVeranda.Min, -FloorLength, 0, Domain.Min, 0, 0).ToNurbsCurve(), HandrailHeight, HandrailSpace));
                }
                if (SideExist2)
                {
                    Result.Add(Providers.GetHandrailGlassSimple(new Line(DomainVeranda.Max, -FloorLength, 0, Domain.Max, 0, 0).ToNurbsCurve(), HandrailHeight, HandrailSpace));
                }
                return Result;
            }

            /// <summary>
            /// クラスの新しいインスタンスを生成します。
            /// </summary>
            /// <param name="Domain">配置される壁における範囲</param>
            /// <param name="DomainVeranda">外側のベランダの配置される壁に対する範囲</param>
            /// <param name="FloorThickness">床の厚さ</param>
            /// <param name="FloorLength">床の長さparam>
            /// <param name="SideExist1">横の手すりの有無。Domainの最小側に配置するか。</param>
            /// <param name="SideExist2">横の手すりの有無。Domainの最大側に配置するか。</param>
            public VerandaGlassSimpleWide(Interval Domain, Interval DomainVeranda, double FloorThickness, double FloorLength, bool SideExist1 = true, bool SideExist2 = true)
            {
                this.Domain = Domain;
                this.DomainVeranda = DomainVeranda;
                this.FloorLength = FloorLength;
                this.FloorThickness = FloorThickness;
                this.SideExist1 = SideExist1;
                this.SideExist2 = SideExist2;
            }
            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public WallAttachment Duplicate()
            {
                return new VerandaGlassSimpleWide(this.Domain, this.DomainVeranda, this.FloorThickness, this.FloorLength, this.SideExist1, this.SideExist2);
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
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


        /// <summary>
        /// 窓を示します
        /// </summary>
        public interface Window
        {
            /// <summary>
            /// 開始点を壁における座標で指定します。
            /// </summary>
            Point2d StartPoint { get; set; }
            /// <summary>
            /// 幅
            /// </summary>
            double Width { get; set; }
            /// <summary>
            /// 高さ
            /// </summary>
            double Height { get; set; }

            RealObject.Building GetBuilding();
            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            Window Duplicate();
            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            PlanObject.Member[] GetPlan(double WallThickness);

        }

        /// <summary>
        /// 単純なエレベータを示します。3Dモデルを提供しません。
        /// </summary>
        public class ElevatorSimple : Window
        {
            /// <summary>
            /// 開始点を壁における座標で指定します。
            /// </summary>
            public Point2d StartPoint { get; set; }
            /// <summary>
            /// ドアの幅
            /// </summary>
            public double Width { get; set; }
            /// <summary>
            /// ドアの高さ
            /// </summary>
            public double Height { get; set; }
            /// <summary>
            /// エレベーターの幅
            /// </summary>
            public double ElevatorWidth { get; set; }
            /// <summary>
            /// エレベーターの長さ
            /// </summary>
            public double ElevatorLength { get; set; }
            /// <summary>
            /// エレベーターの高さ。現時点では利用されていません。
            /// </summary>
            public double ElevatorHeight { get; set; }
            /// <summary>
            /// エレベーターの配置される向きを示します。
            /// </summary>
            public bool IsFront = true;

            /// <summary>
            /// コンストラクター。引数の詳細はメンバ変数で確認してください。
            /// </summary>
            /// <param name="StartPoint"></param>
            /// <param name="Width"></param>
            /// <param name="Height"></param>
            /// <param name="IsFrone"></param>
            /// <param name="ElevatorWidth"></param>
            /// <param name="ElevatorLength"></param>
            /// <param name="ElevatorHeight"></param>
            public ElevatorSimple(Point2d StartPoint, double Width, double Height, bool IsFrone, double ElevatorWidth, double ElevatorLength, double ElevatorHeight)
            {
                this.StartPoint = StartPoint;
                this.Width = Width;
                this.Height = Height;
                this.IsFront = IsFrone;
                this.ElevatorWidth = ElevatorWidth;
                this.ElevatorLength = ElevatorLength;
                this.ElevatorHeight = ElevatorHeight;
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
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

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            public PlanObject.Member[] GetPlan(double WallThickness)
            {
                PlanObject.Member Result = new PlanObject.Member("Elevator");
                if (IsFront)
                {
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

            /// <summary>
            /// エレベータを示すEVの文字を線の集合で与えます。
            /// </summary>
            /// <returns></returns>
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

        /// <summary>
        /// 単純な扉を示します。
        /// </summary>
        public class DoorSimple : Window
        {
            /// <summary>
            /// 開始点を壁における座標で指定します。
            /// </summary>
            public Point2d StartPoint { get; set; }
            /// <summary>
            /// 扉の幅を示します。
            /// </summary>
            public double Width { get; set; }
            /// <summary>
            /// 扉の高さを示します。
            /// </summary>
            public double Height { get; set; }
            /// <summary>
            /// 扉の方向と向きを示します。
            /// </summary>
            public Side OpenSide = Side.FrontRight;

            /// <summary>
            /// コンストラクター。
            /// </summary>
            /// <param name="StartPoint">開始点を壁における座標で指定します</param>
            /// <param name="Width">扉の幅</param>
            /// <param name="Height">扉の高さ</param>
            /// <param name="OpenSide">扉の方向と向き</param>
            public DoorSimple(Point2d StartPoint, double Width, double Height, Side OpenSide)
            {
                this.StartPoint = StartPoint;
                this.Width = Width;
                this.Height = Height;
                this.OpenSide = OpenSide;
            }
            /// <summary>
            /// 扉の方向と向き
            /// </summary>
            public enum Side
            {
                /// <summary>
                /// 正面側右向き
                /// </summary>
                FrontRight,
                /// <summary>
                /// 正面側左向き
                /// </summary>
                FrontLeft,
                /// <summary>
                /// 背面側右向き
                /// </summary>
                BackRight,
                /// <summary>
                /// 背面側左向き
                /// </summary>
                BackLeft
                    
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public Window Duplicate()
            {
                DoorSimple Result = new DoorSimple(StartPoint, Width, Height, OpenSide);
                return Result;
            }

            /// <summary>
            /// 扉を建築として取得します。
            /// </summary>
            /// <returns>扉</returns>
            public RealObject.Building GetBuilding()
            {
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
            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            public PlanObject.Member[] GetPlan(double WallThickness)
            {
                PlanObject.Member Result = new PlanObject.Member("Door");
                switch (this.OpenSide)
                {
                    case Side.FrontLeft:
                        Result.Content.Add(new Line(StartPoint.X, 0, 0, StartPoint.X, -Width, 0).ToNurbsCurve());
                        Result.Content.Add(new Arc(new Circle(new Point3d(StartPoint.X, 0, 0), Width), new Interval(-Math.PI / 2, 0)).ToNurbsCurve());
                        return new[] { Result };
                    case Side.FrontRight:
                        Result.Content.Add(new Line(StartPoint.X + Width, 0, 0, StartPoint.X + Width, -Width, 0).ToNurbsCurve());
                        Result.Content.Add(new Arc(new Circle(new Point3d(StartPoint.X + Width, 0, 0), Width), new Interval(-Math.PI, -Math.PI / 2)).ToNurbsCurve());
                        return new[] { Result };
                    case Side.BackLeft:
                        Result.Content.Add(new Line(StartPoint.X, WallThickness, 0, StartPoint.X, WallThickness + Width, 0).ToNurbsCurve());
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

        /// <summary>
        /// はめ殺しまたは片引きのガラス扉を示します。
        /// </summary>
        public class WindowGlassSimpleSingle : Window
        {
            /// <summary>
            /// 開始点を壁における座標で指定します。
            /// </summary>
            public Point2d StartPoint { get; set; }
            /// <summary>
            /// 扉の幅を示します。
            /// </summary>
            public double Width { get; set; }
            /// <summary>
            /// 扉の高さを示します。
            /// </summary>
            public double Height { get; set; }

            /// <summary>
            /// ガラス扉のフレーム幅を示します。
            /// </summary>
            public double FrameWidth = 30;
            /// <summary>
            /// ガラス扉のフレーム厚さを示します。
            /// </summary>
            public double FrameThickness = 30;
            /// <summary>
            /// ガラスの厚さを示します。住宅用は通常2～4mmといった所です。
            /// </summary>
            public double GlassThickness = 5;

            /// <summary>
            /// ガラス扉を建築として取得します。
            /// </summary>
            /// <returns>ガラス扉</returns>
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

            /// <summary>
            /// コンストラクター。
            /// </summary>
            /// <param name="StartPoint">開始点を壁における座標で指定します。</param>
            /// <param name="Width">扉の幅</param>
            /// <param name="Height">扉の高さ</param>
            public WindowGlassSimpleSingle(Point2d StartPoint, double Width, double Height)
            {
                this.StartPoint = StartPoint;
                this.Width = Width;
                this.Height = Height;
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public Window Duplicate()
            {
                return new WindowGlassSimpleSingle(this.StartPoint, this.Width, this.Height)
                {
                    FrameWidth = this.FrameWidth,
                    FrameThickness = this.FrameThickness,
                    GlassThickness = this.GlassThickness
                };
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            public PlanObject.Member[] GetPlan(double WallThickness)
            {
                PlanObject.Member Result = new PlanObject.Member("WindowSingle");
                Result.Content.Add(new Line(StartPoint.X, 0, 0, StartPoint.X, WallThickness, 0).ToNurbsCurve());
                Result.Content.Add(new Line(StartPoint.X + Width, 0, 0, StartPoint.X + Width, WallThickness, 0).ToNurbsCurve());
                return new[] { Result };
            }
        }

        /// <summary>
        /// 引き違いのガラス扉を示します。
        /// </summary>
        public class WindowGlassSimpleDouble : Window
        {
            /// <summary>
            /// 開始点を壁における座標で指定します。
            /// </summary>
            public Point2d StartPoint { get; set; }
            /// <summary>
            /// 扉全体の幅を示します。
            /// </summary>
            public double Width { get; set; }
            /// <summary>
            /// 扉の高さを示します。
            /// </summary>
            public double Height { get; set; }

            /// <summary>
            /// ガラスフレームの幅を示します。
            /// </summary>
            public double FrameWidth = 30;
            /// <summary>
            /// ガラスフレームの厚さを示します。
            /// </summary>
            public double FrameThickness = 30;
            /// <summary>
            /// ガラスの厚さを示します。
            /// </summary>
            public double GlassThickness = 5;

            /// <summary>
            /// ガラス扉を建築として取得します。
            /// </summary>
            /// <returns>ガラス扉</returns>
            public RealObject.Building GetBuilding()
            {
                RealObject.Building Result = new RealObject.Building("WindowDouble");
                Brep[] breps = new Brep[2];

                Providers.GetGlassSimple(Width / 2.0, Height, FrameWidth, FrameThickness, GlassThickness, out breps[0], out breps[1]);
                Result.Add("Glass", new Brep[] { breps[1] });
                Result.Add("Glass", GeneralHelper.TranslateBreps(new Brep[] { breps[1] }, new Point3d(Width / 2.0, FrameThickness, 0)));
                Result.Add("Frame", new Brep[] { breps[0] });
                Result.Add("Frame", GeneralHelper.TranslateBreps(new Brep[] { breps[0] }, new Point3d(Width / 2.0, FrameThickness, 0)));

                Result.Transform(Transform.Translation(StartPoint.X, FrameThickness, StartPoint.Y));

                return Result;
            }

            /// <summary>
            /// コンストラクター。
            /// </summary>
            /// <param name="StartPoint">開始点を壁における座標で指定します。</param>
            /// <param name="Width">幅</param>
            /// <param name="Height">高さ</param>
            public WindowGlassSimpleDouble(Point2d StartPoint, double Width, double Height)
            {
                this.StartPoint = StartPoint;
                this.Width = Width;
                this.Height = Height;
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public Window Duplicate()
            {
                return new WindowGlassSimpleDouble(this.StartPoint, this.Width, this.Height)
                {
                    FrameWidth = this.FrameWidth,
                    FrameThickness = this.FrameThickness,
                    GlassThickness = this.GlassThickness
                };
            }

            /// <summary>
            /// 平面図を取得します。
            /// </summary>
            /// <returns>平面図</returns>
            public PlanObject.Member[] GetPlan(double WallThickness)
            {
                PlanObject.Member Result = new PlanObject.Member("WindowDouble");
                Result.Content.Add(new Line(StartPoint.X, 0, 0, StartPoint.X, WallThickness, 0).ToNurbsCurve());
                Result.Content.Add(new Line(StartPoint.X + Width, 0, 0, StartPoint.X + Width, WallThickness, 0).ToNurbsCurve());
                Result.Content.Add(new Line(StartPoint.X, WallThickness / 3, 0, StartPoint.X + Width * 3 / 4, WallThickness / 3, 0).ToNurbsCurve());
                Result.Content.Add(new Line(StartPoint.X + Width / 4, WallThickness * 2 / 3, 0, StartPoint.X + Width, WallThickness * 2 / 3, 0).ToNurbsCurve());
                Result.Content.Add(new Line(StartPoint.X + Width / 2, -WallThickness / 2, 0, StartPoint.X + Width / 2, WallThickness * 3 / 2, 0).ToNurbsCurve());
                return new[] { Result };
            }
        }
    }
}
