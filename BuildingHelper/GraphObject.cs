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
    /// 簡単なIKに関する機能を含みます。
    /// </summary>
    public class GraphObject
    {
        /// <summary>
        /// ボーンを示します。
        /// </summary>
        public class Path
        {
            /// <summary>
            /// 名前
            /// </summary>
            public string Name { get { return ContentMember.ToString(); } set { ContentMember.SetName(value); } }
            /// <summary>
            /// ボーンに含まれる部材です。
            /// </summary>
            public RealObject.Member ContentMember;
            /// <summary>
            /// ボーンの長さです。
            /// </summary>
            public double Length;

            /// <summary>
            /// 距離と半径の組み合わせからパイプによるボーンを作ります。
            /// </summary>
            /// <param name="Distance">距離</param>
            /// <param name="Radius">半径</param>
            /// <returns>ボーン</returns>
            public static Path CreateFromPipeSimple(double[] Distance, double[] Radius)
            {
                return new Path() { ContentMember = new Brep[] { Providers.GetPipeSimple(Distance, Radius) }, Length = Distance[Distance.GetLength(0) - 1] - Distance[0] };
            }

            /// <summary>
            /// 距離と半径の組み合わせから片方が閉じた形状のボーンを作ります。
            /// </summary>
            /// <param name="Distance">距離</param>
            /// <param name="Radius">半径</param>
            /// <returns>ボーン</returns>
            public static Path CreateFromPipeHead(double[] Distance, double[] Radius)
            {
                return new Path() { ContentMember = new Brep[] { Providers.GetPipeHead(Distance, Radius) }, Length = Distance[Distance.GetLength(0) - 1] - Distance[0] };
            }

            /// <summary>
            /// 多角形柱のボーンを作ります。
            /// </summary>
            /// <param name="count">n角形におけるn</param>
            /// <param name="Radius">半径</param>
            /// <param name="Height">高さ</param>
            /// <returns>ボーン</returns>
            public static Path CreateFromRegularPolygonTower(int count, double Radius, double Height)
            {
                Brep bp = Providers.GetRegularPolygonTower(count, Radius, Height, true);
                bp.Transform(Transform.ChangeBasis(Plane.WorldYZ, Plane.WorldXY));
                return new Path() { ContentMember = new Brep[] { bp }, Length = Height };
            }
        }

        /// <summary>
        /// 関節を示します。
        /// </summary>
        public class Node
        {
            /// <summary>
            /// 名前
            /// </summary>
            public string Name { get { return ContentMember.ToString(); } set { ContentMember.SetName(value); } }
            /// <summary>
            /// 関節につながるボーン1
            /// </summary>
            public Path Target1;
            /// <summary>
            /// 関節につながるボーン2
            /// </summary>
            public Path Target2;
            /// <summary>
            /// ボーン1のつながるボーン内の場所
            /// </summary>
            public Point3d Target1ConnectionPoint = new Point3d(0, 0, 0);
            /// <summary>
            /// ボーン2のつながるボーン内の場所
            /// </summary>
            public Point3d Target2ConnectionPoint = new Point3d(0, 0, 0);
            /// <summary>
            /// 関節の半径
            /// </summary>
            public double Radius;
            /// <summary>
            /// 関節に含まれる部材
            /// </summary>
            public RealObject.Member ContentMember;
            
            /// <summary>
            /// X方向の回転角
            /// </summary>
            public double RotationAngleX { get { return _RotationAngleX; } set { _RotationAngleX = Math.Max(RotationAngleXLimitation.Min, Math.Min(RotationAngleXLimitation.Max, value % (Math.PI * 2.0))); } }
            private double _RotationAngleX = 0;
            /// <summary>
            /// X方向の回転可能範囲
            /// </summary>
            public Interval RotationAngleXLimitation = new Interval(0, Math.PI * 2.0);

            /// <summary>
            /// Y方向の回転角
            /// </summary>
            public double RotationAngleY { get { return _RotationAngleY; } set { _RotationAngleY = Math.Max(RotationAngleYLimitation.Min, Math.Min(RotationAngleYLimitation.Max, value % (Math.PI * 2.0))); } }
            private double _RotationAngleY = 0;
            /// <summary>
            /// Y方向の回転可能範囲
            /// </summary>
            public Interval RotationAngleYLimitation = new Interval(0, Math.PI * 2.0);

            /// <summary>
            /// Z方向の回転角
            /// </summary>
            public double RotationAngleZ { get { return _RotationAngleZ; } set { _RotationAngleZ = Math.Max(RotationAngleZLimitation.Min, Math.Min(RotationAngleZLimitation.Max, value % (Math.PI * 2.0))); } }
            private double _RotationAngleZ = 0;
            /// <summary>
            /// Z方向の回転可能範囲
            /// </summary>
            public Interval RotationAngleZLimitation = new Interval(0, Math.PI * 2.0);

            /// <summary>
            /// 二つのボーンの内、指定値と違う方を取得する。
            /// </summary>
            /// <param name="that">指定ボーン</param>
            /// <returns>異なる方のボーン</returns>
            public Path GetPathOtherThan(Path that)
            {
                if (that == Target1) { return Target2; }
                else if (that == Target2) { return Target1; }
                else { return new Path(); }
            }

            /// <summary>
            /// 指定したボーンの接続点を取得します。
            /// </summary>
            /// <param name="that">指定ボーン</param>
            /// <returns>接続点</returns>
            public Point3d GetConnectionPoint(Path that)
            {
                if (that == Target1) { return new Point3d(Target1ConnectionPoint); }
                else if (that == Target2) { return new Point3d(Target2ConnectionPoint); }
                else { return new Point3d(0, 0, 0); }
            }

            /// <summary>
            /// 球から関節を作ります。
            /// </summary>
            /// <param name="rad">半径</param>
            /// <param name="len">長さ</param>
            /// <returns>関節</returns>
            public static Node CreateFromSphere(double rad, double len)
            {
                if (len > 0)
                {
                    return new Node() { Radius = len / 2.0, ContentMember = new Brep[] { Brep.CreateFromSphere(new Sphere(new Point3d(0, 0, 0), rad)) } };
                }
                else
                {
                    return new Node() { Radius = 0, ContentMember = new Brep[0] };
                }
            }
        }

        /// <summary>
        /// IKモデル
        /// </summary>
        public class Graph
        {
            /// <summary>
            /// 基底ボーン。これに長さ0のボーンを設定すると関節を基底として根元から回転できるようになります。
            /// </summary>
            public Path RootPath;//You can set this length to zero to set Node as root.
            private Dictionary<Path, List<Node>> NodesDic = new Dictionary<Path, List<Node>>();
            /// <summary>
            /// 配置場所
            /// </summary>
            public Point3d Position;
            /// <summary>
            /// X方向の回転角
            /// </summary>
            public double RotationAngleX = 0;
            /// <summary>
            /// Y方向の回転角
            /// </summary>
            public double RotationAngleY = 0;
            /// <summary>
            /// Z方向の回転角
            /// </summary>
            public double RotationAngleZ = 0;
            public double AddScale = 1.0;

            /// <summary>
            /// モデルに含まれるボーン
            /// </summary>
            public Path[] Paths
            {
                get
                {
                    List<Path> Result = new List<Path>();
                    Result.Add(RootPath);
                    foreach (KeyValuePair<Path, List<Node>> kvp in NodesDic)
                    {
                        foreach (Node nd in kvp.Value)
                        {
                            Path pathtoadd = nd.GetPathOtherThan(kvp.Key);
                            if (!Result.Contains(pathtoadd)) Result.Add(pathtoadd);
                        }
                    }
                    return Result.ToArray();
                }
            }
            /// <summary>
            /// モデルに含まれる関数
            /// </summary>
            public Node[] Nodes
            {
                get
                {
                    List<Node> Result = new List<Node>();
                    foreach (KeyValuePair<Path, List<Node>> kvp in NodesDic)
                    {
                        foreach (Node nd in kvp.Value)
                        {
                            Result.Add(nd);
                        }
                    }
                    return Result.ToArray();
                }
            }

            /// <summary>
            /// 既存のボーンに新規ボーンを接続します。
            /// </summary>
            /// <param name="PathToConnect">接続するボーン</param>
            /// <param name="PathToAdd">新規ボーン</param>
            /// <returns>接続する関節</returns>
            public Node Add(Path PathToConnect, Path PathToAdd)
            {
                return Add(PathToConnect, new Point3d(PathToConnect.Length / AddScale, 0, 0), PathToAdd, new Node() { ContentMember = new Brep[0], Radius = 0 });
            }
            /// <summary>
            /// 既存のボーンに新規ボーンを接続します。
            /// </summary>
            /// <param name="PathToConnect">接続するボーン</param>
            /// <param name="PathToAdd">新規ボーン</param>
            /// <param name="rad">半径</param>
            /// <param name="len">新規ボーンの長さ</param>
            /// <returns>接続された関節</returns>
            public Node Add(Path PathToConnect, Path PathToAdd, double rad, double len)
            {
                return Add(PathToConnect, new Point3d(PathToConnect.Length / AddScale, 0, 0), PathToAdd, Node.CreateFromSphere(rad, len));
            }

            /// <summary>
            /// 既存の関節に新規ボーンを接続します。
            /// </summary>
            /// <param name="PathToConnect">接続するボーン</param>
            /// <param name="ConnectionPoint">接続する点</param>
            /// <param name="PathToAdd">新規ボーン</param>
            /// <param name="node">接続する関節</param>
            /// <returns>接続された関節</returns>
            public Node Add(Path PathToConnect, Point3d ConnectionPoint, Path PathToAdd, Node node)
            {
                node.Target1 = PathToConnect;
                node.Target1ConnectionPoint = ConnectionPoint * AddScale;
                node.Target2 = PathToAdd;

                Brep[] newbp;
                newbp = new Brep[PathToAdd.ContentMember.Content.GetLength(0)];
                for (int i = 0; i < PathToAdd.ContentMember.Content.GetLength(0); i++)
                {
                    newbp[i] = (Brep)PathToAdd.ContentMember.Content[i].Duplicate();
                    newbp[i].Scale(AddScale);
                }
                PathToAdd.ContentMember.Content = newbp;
                PathToAdd.Length *= AddScale;

                newbp = new Brep[PathToConnect.ContentMember.Content.GetLength(0)];
                for (int i = 0; i < node.ContentMember.Content.GetLength(0); i++)
                {
                    newbp[i] = (Brep)node.ContentMember.Content[i].Duplicate();
                    newbp[i].Scale(AddScale);
                }
                node.ContentMember.Content = newbp;

                node.Radius *= AddScale;

                NodesDic[PathToConnect].Add(node);
                NodesDic.Add(PathToAdd, new List<Node>());
                return node;
            }

            /// <summary>
            /// 関節をランダムに動かす。
            /// </summary>
            public void MoveRandom()
            {
                MoveRandom(new Random());
            }

            /// <summary>
            /// 関節をランダムに動かす。
            /// </summary>
            /// <param name="rd">乱数の種</param>
            public void MoveRandom(Random rd)
            {
                foreach (Node nd in Nodes)
                {
                    nd.RotationAngleX = nd.RotationAngleXLimitation.Min + nd.RotationAngleXLimitation.Length * rd.NextDouble();
                    nd.RotationAngleY = nd.RotationAngleYLimitation.Min + nd.RotationAngleYLimitation.Length * rd.NextDouble();
                    nd.RotationAngleZ = nd.RotationAngleZLimitation.Min + nd.RotationAngleZLimitation.Length * rd.NextDouble();
                }
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="Root">基底のボーン</param>
            /// <param name="Position">座標</param>
            public Graph(Path Root, Point3d Position)
                : this(Root, Position, false)
            {
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="Root">基底のボーン</param>
            /// <param name="ObjectPosition">配置場所</param>
            /// <param name="CanRootMoveFreely">基底ボーンが自由に動けるか</param>
            public Graph(Path Root, Point3d ObjectPosition, bool CanRootMoveFreely)
                : this(Root, ObjectPosition, CanRootMoveFreely, 1.0)
            {
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="Root">基底のボーン</param>
            /// <param name="ObjectPosition">配置場所</param>
            /// <param name="CanRootMoveFreely">基底ボーンが自由に動けるか</param>
            /// <param name="Scale">拡大率</param>
            public Graph(Path Root, Point3d ObjectPosition, bool CanRootMoveFreely, double Scale)
            {
                this.AddScale = Scale;
                this.Position = ObjectPosition * AddScale;

                Brep[] newbp;
                newbp = new Brep[Root.ContentMember.Content.GetLength(0)];
                for (int i = 0; i < Root.ContentMember.Content.GetLength(0); i++)
                {
                    newbp[i] = (Brep)Root.ContentMember.Content[i].Duplicate();
                    newbp[i].Scale(AddScale);
                }
                Root.ContentMember.Content = newbp;
                Root.Length *= AddScale;

                if (CanRootMoveFreely)
                {
                    RootPath = new Path() { Length = 0 };
                    NodesDic.Add(RootPath, new List<Node>() { new Node() { Target1 = RootPath, Target2 = Root, Radius = 0, ContentMember = new Brep[0] } });
                }
                else
                {
                    RootPath = Root;
                    NodesDic.Add(Root, new List<Node>());
                }
            }

            /// <summary>
            /// 3Dモデル(Brep)を取得します。
            /// </summary>
            /// <returns>3Dモデル</returns>
            public Brep[] GetBrep()
            {
                Plane newpl = Plane.WorldXY;
                newpl.Transform(Transform.Rotation(RotationAngleX, newpl.XAxis, newpl.Origin));
                newpl.Transform(Transform.Rotation(RotationAngleY, newpl.YAxis, newpl.Origin));
                newpl.Transform(Transform.Rotation(RotationAngleZ, newpl.ZAxis, newpl.Origin));
                newpl.Origin = this.Position;
                List<Brep[]> ResultPre = GetBrepFromPath(this.RootPath, newpl);
                List<Brep> Result = new List<Brep>();
                foreach (Brep[] bps in ResultPre)
                {
                    Result.AddRange(bps);
                }
                return Result.ToArray();
            }

            [System.Obsolete("This is old version. Use same function with Plane.")]
            private List<Brep[]> GetBrepFromPath(Path p, Point3d crtpos, double crtangx, double crtangy, double crtangz)
            {
                List<Brep[]> Result = new List<Brep[]>();
                Brep[] TempBreps = new Brep[p.ContentMember.Content.GetLength(0)];
                int cnt = 0;
                foreach (Brep bp in p.ContentMember.Content)
                {
                    Brep tempbp = (Brep)bp.Duplicate();
                    tempbp.Rotate(crtangx, new Vector3d(1, 0, 0), new Point3d(0, 0, 0));
                    tempbp.Rotate(crtangy, new Vector3d(0, 1, 0), new Point3d(0, 0, 0));
                    tempbp.Rotate(crtangz, new Vector3d(0, 0, 1), new Point3d(0, 0, 0));
                    tempbp.Translate((Vector3d)crtpos);
                    TempBreps[cnt] = tempbp;
                    cnt++;
                }
                Result.Add(TempBreps);
                foreach (Node nd in this.NodesDic[p])
                {
                    Point3d cntpt = nd.GetConnectionPoint(p) + new Point3d(nd.Radius, 0, 0);
                    cntpt.Transform(Transform.Rotation(crtangx, new Vector3d(1, 0, 0), new Point3d(0, 0, 0)));
                    cntpt.Transform(Transform.Rotation(crtangy, new Vector3d(0, 1, 0), new Point3d(0, 0, 0)));
                    cntpt.Transform(Transform.Rotation(crtangz, new Vector3d(0, 0, 1), new Point3d(0, 0, 0)));

                    Point3d ndpt = new Point3d(nd.Radius, 0, 0);
                    ndpt.Transform(Transform.Rotation(crtangx + nd.RotationAngleX, new Vector3d(1, 0, 0), new Point3d(0, 0, 0)));
                    ndpt.Transform(Transform.Rotation(crtangy + nd.RotationAngleY, new Vector3d(0, 1, 0), new Point3d(0, 0, 0)));
                    ndpt.Transform(Transform.Rotation(crtangz + nd.RotationAngleZ, new Vector3d(0, 0, 1), new Point3d(0, 0, 0)));

                    Brep[] NodeBrep = new Brep[nd.ContentMember.Content.GetLength(0)];
                    for (int i = 0; i < nd.ContentMember.Content.GetLength(0); i++)
                    {
                        if (nd.ContentMember.Content[i] == null) { continue; }
                        Brep tbp = (Brep)nd.ContentMember.Content[i].Duplicate();
                        tbp.Translate((Vector3d)(crtpos + cntpt));
                        NodeBrep[i] = tbp;
                    }
                    Result.Add(NodeBrep);

                    Result.AddRange(GetBrepFromPath(nd.GetPathOtherThan(p), crtpos + cntpt + ndpt, crtangx + nd.RotationAngleX, crtangy + nd.RotationAngleY, crtangz + nd.RotationAngleZ));
                }
                return Result;
            }

            private List<Brep[]> GetBrepFromPath(Path p, Plane pl)
            {
                List<Brep[]> Result = new List<Brep[]>();
                Brep[] TempBreps = new Brep[p.ContentMember.Content.GetLength(0)];
                int cnt = 0;
                foreach (Brep bp in p.ContentMember.Content)
                {
                    Brep tempbp = (Brep)bp.Duplicate();
                    tempbp.Transform(Transform.ChangeBasis(pl, Plane.WorldXY));
                    TempBreps[cnt] = tempbp;
                    cnt++;
                }
                Result.Add(TempBreps);
                foreach (Node nd in this.NodesDic[p])
                {
                    Point3d cntpt = nd.GetConnectionPoint(p) + new Point3d(nd.Radius, 0, 0);
                    cntpt.Transform(Transform.ChangeBasis(pl, Plane.WorldXY));

                    Plane newpl = new Plane(pl);
                    newpl.Transform(Transform.Rotation(nd.RotationAngleX, newpl.XAxis, newpl.Origin));
                    newpl.Transform(Transform.Rotation(nd.RotationAngleY, newpl.YAxis, newpl.Origin));
                    newpl.Transform(Transform.Rotation(nd.RotationAngleZ, newpl.ZAxis, newpl.Origin));
                    newpl.Origin = cntpt;

                    Point3d ndpt = (Point3d)(newpl.XAxis * nd.Radius);

                    Brep[] NodeBrep = new Brep[nd.ContentMember.Content.GetLength(0)];
                    for (int i = 0; i < nd.ContentMember.Content.GetLength(0); i++)
                    {
                        if (nd.ContentMember.Content[i] == null) { continue; }
                        Brep tbp = (Brep)nd.ContentMember.Content[i].Duplicate();
                        tbp.Transform(Transform.ChangeBasis(newpl, Plane.WorldXY));
                        NodeBrep[i] = tbp;
                    }
                    Result.Add(NodeBrep);

                    Result.AddRange(GetBrepFromPath(nd.GetPathOtherThan(p), newpl));
                }
                return Result;
            }

            /// <summary>
            /// 3Dモデル(RealObject.Building)を取得します。
            /// </summary>
            /// <returns>3Dモデル</returns>
            public RealObject.Building GetMember()
            {
                RealObject.Building Result = new RealObject.Building("Graph");
                Plane newpl = Plane.WorldXY;
                newpl.Transform(Transform.Rotation(RotationAngleX, newpl.XAxis, newpl.Origin));
                newpl.Transform(Transform.Rotation(RotationAngleY, newpl.YAxis, newpl.Origin));
                newpl.Transform(Transform.Rotation(RotationAngleZ, newpl.ZAxis, newpl.Origin));
                newpl.Origin = this.Position;
                Result.Add(GetMemberFromPath(this.RootPath, newpl).ToArray());
                return Result;
            }

            private List<RealObject.Member> GetMemberFromPath(Path p, Plane pl)
            {
                List<RealObject.Member> Result = new List<RealObject.Member>();

                int cnt = 0;
                RealObject.Member newmember = p.ContentMember.Duplicate();
                foreach (Brep bp in newmember.Content)
                {
                    bp.Transform(Transform.ChangeBasis(pl, Plane.WorldXY));
                    newmember.Content[cnt] = bp;
                    cnt++;
                }
                Result.Add(newmember);

                foreach (Node nd in this.NodesDic[p])
                {
                    Point3d cntpt = nd.GetConnectionPoint(p) + new Point3d(nd.Radius, 0, 0);
                    cntpt.Transform(Transform.ChangeBasis(pl, Plane.WorldXY));

                    Plane newpl = new Plane(pl);
                    newpl.Transform(Transform.Rotation(nd.RotationAngleX, newpl.XAxis, newpl.Origin));
                    newpl.Transform(Transform.Rotation(nd.RotationAngleY, newpl.YAxis, newpl.Origin));
                    newpl.Transform(Transform.Rotation(nd.RotationAngleZ, newpl.ZAxis, newpl.Origin));
                    newpl.Origin = cntpt;

                    Point3d ndpt = (Point3d)(newpl.XAxis * nd.Radius);

                    RealObject.Member newmember2 = nd.ContentMember.Duplicate();
                    for (int i = 0; i < nd.ContentMember.Content.GetLength(0); i++)
                    {
                        if (nd.ContentMember.Content[i] == null) { continue; }
                        Brep tbp = (Brep)nd.ContentMember.Content[i].Duplicate();
                        tbp.Transform(Transform.ChangeBasis(newpl, Plane.WorldXY));
                        newmember2.Content[i] = tbp;
                    }
                    Result.Add(newmember2);

                    Result.AddRange(GetMemberFromPath(nd.GetPathOtherThan(p), newpl));
                }
                return Result;
            }

            /// <summary>
            /// デッサン人形風のIKモデルを取得します。添景として利用できます。
            /// </summary>
            /// <param name="Height">高さ</param>
            /// <returns>IKモデル</returns>
            public static Graph GetHumanBody(double Height)
            {
                Graph Result = new Graph(Path.CreateFromPipeSimple(new double[] { 0, 15, 30 }, new double[] { 20, 20, 15 }), new Point3d(0, 0, 165), false, Height / 260.0);
                Result.RotationAngleY = -Math.PI / 2.0;
                List<Node> HumanNodes = new List<Node>();

                Path chest = Path.CreateFromPipeSimple(new double[] { 0, 40, 50 }, new double[] { 15, 20, 10 });
                HumanNodes.Add(Result.Add(Result.RootPath, chest, 15, 15));
                HumanNodes[0].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 15, Math.PI / 180.0 * 15);
                HumanNodes[0].RotationAngleYLimitation = new Interval(-Math.PI / 180.0 * 15, Math.PI / 180.0 * 15);
                HumanNodes[0].RotationAngleXLimitation = new Interval(-Math.PI / 180.0 * 15, Math.PI / 180.0 * 15);

                Path head = Path.CreateFromPipeHead(new double[] { 0, 30, 45 }, new double[] { 12, 15 });
                HumanNodes.Add(Result.Add(chest, head, 7.5, 7.5));
                HumanNodes[1].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 80, Math.PI / 180.0 * 80);
                HumanNodes[1].RotationAngleYLimitation = new Interval(-Math.PI / 180.0 * 15, Math.PI / 180.0 * 15);
                HumanNodes[1].RotationAngleXLimitation = new Interval(-Math.PI / 180.0 * 15, Math.PI / 180.0 * 15);

                Path handR1 = Path.CreateFromPipeSimple(new double[] { 0, 15, 30, 45 }, new double[] { 7.5, 9.0, 7, 6 });
                HumanNodes.Add(Result.Add(chest, new Point3d(40, 23, 0), handR1, Node.CreateFromSphere(8, 6)));
                HumanNodes[2].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 40, Math.PI / 180.0 * 40);

                Path handR2 = Path.CreateFromPipeSimple(new double[] { 0, 20, 40 }, new double[] { 7, 8.0, 7 });
                HumanNodes.Add(Result.Add(handR1, handR2, 7.5, 4.0));
                HumanNodes[3].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 40, Math.PI / 180.0 * 40);

                Path handR3 = new Path() { ContentMember = new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(0, 20), new Interval(-6, 6), new Interval(-1.5, 1.5))) } };
                HumanNodes.Add(Result.Add(handR2, handR3, 4, 3));
                HumanNodes[4].RotationAngleZLimitation = new Interval(0, 0);
                HumanNodes[4].RotationAngleYLimitation = new Interval(-Math.PI / 180.0 * 45, Math.PI / 180.0 * 45);
                HumanNodes[4].RotationAngleXLimitation = new Interval(-Math.PI / 180.0 * 45, Math.PI / 180.0 * 45);

                Path handL1 = Path.CreateFromPipeSimple(new double[] { 0, 15, 30, 45 }, new double[] { 7.5, 9.0, 7, 6 });
                HumanNodes.Add(Result.Add(chest, new Point3d(40, -23, 0), handL1, Node.CreateFromSphere(8, 6)));
                HumanNodes[5].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 40, Math.PI / 180.0 * 40);

                Path handL2 = Path.CreateFromPipeSimple(new double[] { 0, 20, 40 }, new double[] { 7, 8.0, 7 });
                HumanNodes.Add(Result.Add(handL1, handL2, 7.5, 4.0));
                HumanNodes[6].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 40, Math.PI / 180.0 * 40);

                Path handL3 = new Path() { ContentMember = new Brep[] { Brep.CreateFromBox(new Box(Plane.WorldXY, new Interval(0, 20), new Interval(-6, 6), new Interval(-1.5, 1.5))) } };
                HumanNodes.Add(Result.Add(handL2, handL3, 4, 3));
                HumanNodes[7].RotationAngleZLimitation = new Interval(0, 0);
                HumanNodes[7].RotationAngleYLimitation = new Interval(-Math.PI / 180.0 * 45, Math.PI / 180.0 * 45);
                HumanNodes[7].RotationAngleXLimitation = new Interval(-Math.PI / 180.0 * 45, Math.PI / 180.0 * 45);

                Path legR1 = Path.CreateFromPipeSimple(new double[] { 0, 15, 40, 60 }, new double[] { 7.5, 10.0, 7.5, 7.0 });
                HumanNodes.Add(Result.Add(Result.RootPath, new Point3d(-1.5, 15, 0), legR1, Node.CreateFromSphere(7.5, 6)));
                HumanNodes[8].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 40, Math.PI / 180.0 * 40);
                HumanNodes[8].RotationAngleYLimitation = new Interval(Math.PI * 0.5, Math.PI * 1.5);
                HumanNodes[8].RotationAngleXLimitation = new Interval(0, 0);

                Path legR2 = Path.CreateFromPipeSimple(new double[] { 0, 15.0, 45, 65 }, new double[] { 7.5, 8.5, 7.5, 6.5 });
                HumanNodes.Add(Result.Add(legR1, legR2, 6, 5));
                HumanNodes[9].RotationAngleZLimitation = new Interval(0, 0);
                HumanNodes[9].RotationAngleYLimitation = new Interval(-Math.PI, 0);
                HumanNodes[9].RotationAngleXLimitation = new Interval(-Math.PI, 0);

                Brep shoes = Providers.GetPipeHalf(new double[] { -10, 20, 30 }, new double[] { 9, 9, 0 });
                shoes.Rotate(-Math.PI / 2.0, new Vector3d(0, 1, 0), new Point3d(0, 0, 0));
                shoes.Translate(11, 0, 0);

                Path legR3 = new Path() { ContentMember = new Brep[] { (Brep)shoes.Duplicate() } };
                HumanNodes.Add(Result.Add(legR2, legR3, 4, 2.5));
                HumanNodes[10].RotationAngleZLimitation = new Interval(0, 0);
                HumanNodes[10].RotationAngleYLimitation = new Interval(-Math.PI / 4.0, Math.PI / 4.0);
                HumanNodes[10].RotationAngleXLimitation = new Interval(-Math.PI / 2.0, Math.PI / 2.0);

                Path legL1 = Path.CreateFromPipeSimple(new double[] { 0, 15, 40, 60 }, new double[] { 7.5, 10.0, 7.5, 7.0 });
                HumanNodes.Add(Result.Add(Result.RootPath, new Point3d(-1.5, -15, 0), legL1, Node.CreateFromSphere(7.5, 6)));
                HumanNodes[11].RotationAngleZLimitation = new Interval(-Math.PI / 180.0 * 40, Math.PI / 180.0 * 40);
                HumanNodes[11].RotationAngleYLimitation = new Interval(Math.PI * 0.5, Math.PI * 1.5);
                HumanNodes[11].RotationAngleXLimitation = new Interval(0, 0);

                Path legL2 = Path.CreateFromPipeSimple(new double[] { 0, 15.0, 45, 65 }, new double[] { 7.5, 8.5, 7.5, 6.5 });
                HumanNodes.Add(Result.Add(legL1, legL2, 6, 5));
                HumanNodes[12].RotationAngleZLimitation = new Interval(0, 0);
                HumanNodes[12].RotationAngleYLimitation = new Interval(-Math.PI, 0);
                HumanNodes[12].RotationAngleXLimitation = new Interval(-Math.PI, 0);

                Path legL3 = new Path() { ContentMember = new Brep[] { (Brep)shoes.Duplicate() } };
                HumanNodes.Add(Result.Add(legL2, legL3, 4, 2.5));
                HumanNodes[13].RotationAngleZLimitation = new Interval(0, 0);
                HumanNodes[13].RotationAngleYLimitation = new Interval(-Math.PI / 4.0, Math.PI / 4.0);
                HumanNodes[13].RotationAngleXLimitation = new Interval(-Math.PI / 2.0, Math.PI / 2.0);

                return Result;

            }
        }
    }
}
