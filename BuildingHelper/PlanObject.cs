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
    /// 図面を示します。レイヤー設定付きでBake可能です。
    /// </summary>
    public class PlanObject
    {
        /// <summary>
        /// 建築全体の図面を示します。
        /// </summary>
        public class Building
        {
            /// <summary>
            /// 名前
            /// </summary>
            public String Name;
            /// <summary>
            /// 建築に含まれる階層。
            /// </summary>
            public List<Floor> Content = new List<Floor>();

            /// <summary>
            /// 図面を曲線として取得します。
            /// </summary>
            /// <returns></returns>
            public Curve[] GetCurve()
            {
                List<Curve> Result = new List<Curve>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.AddRange(Content[i].GetCurve());
                }
                return Result.ToArray();
            }

            /// <summary>
            /// Transformを適用します。
            /// </summary>
            /// <param name="Tf">Transform</param>
            public void Transform(Transform Tf)
            {
                List<Floor> Operated = new List<Floor>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    if (!Operated.Contains(Content[i]))
                    {
                        Content[i].Transform(Tf);
                        Operated.Add(Content[i]);
                    }
                }
            }

            /// <summary>
            /// Bake(Rhino Documentに出力)します。
            /// </summary>
            /// <param name="RhinoDocument">出力先</param>
            /// <param name="TargetFloor">対象階層</param>
            public void Bake(RhinoDoc RhinoDocument, int TargetFloor)
            {
                Content[TargetFloor].Bake(RhinoDocument, this.Name + ":");

            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public Building Duplicate()
            {
                Building Result = new Building(this.Name);
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.Content.Add(this.Content[i].Duplicate());
                }
                return Result;
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="Name">名前</param>
            public Building(string Name)
            {
                this.Name = Name;
            }
        }

        /// <summary>
        /// 一階層分の平面図を示します。
        /// </summary>
        public class Floor
        {
            /// <summary>
            /// 階層の名前
            /// </summary>
            public String Name;
            /// <summary>
            /// 階高
            /// </summary>
            public double Height;
            /// <summary>
            /// 階層に含まれる部材の平面図を示します。
            /// </summary>
            public List<Member> Content = new List<Member>();

            /// <summary>
            /// Bake(Rhino Documentに出力)します。
            /// </summary>
            /// <param name="RhinoDocument">出力先</param>
            /// <param name="LayerNameHead">出力時のレイヤー名の先端部分</param>
            public void Bake(RhinoDoc RhinoDocument, string LayerNameHead)
            {
                for (int i = 0; i < Content.Count(); i++)
                {
                    Content[i].Bake(RhinoDocument, LayerNameHead + this.Name + ".");
                }
            }

            /// <summary>
            /// Transformを適用します。
            /// </summary>
            /// <param name="Tf">Transform</param>
            public void Transform(Transform Tf)
            {
                for (int i = 0; i < Content.Count(); i++)
                {
                    Content[i].Transform(Tf);
                }
            }

            /// <summary>
            /// 平面図を曲線の集合として取得します。
            /// </summary>
            /// <returns>曲線</returns>
            public Curve[] GetCurve()
            {
                List<Curve> Result = new List<Curve>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.AddRange(Content[i].GetCurve());
                }
                return Result.ToArray();
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <returns>複製結果</returns>
            public Floor Duplicate()
            {
                Floor Result = new Floor(this.Name);
                Result.Height = this.Height;
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.Content.Add(this.Content[i].Duplicate());
                }
                return Result;
            }

            /// <summary>
            /// コンストラクタ。
            /// </summary>
            /// <param name="Name">名前</param>
            public Floor(string Name)
            {
                this.Name = Name;
            }
        }

        /// <summary>
        /// 部材の平面図を示します。
        /// ここでの部材は平面図に描かれる建築の要素全てを含みます。
        /// </summary>
        public class Member
        {
            /// <summary>
            /// 部材名
            /// </summary>
            public String Name;

            /// <summary>
            /// 場所を示します。
            /// </summary>
            public Point2d Point;
            /// <summary>
            /// 平面図の線を含みます。
            /// </summary>
            public List<Curve> Content = new List<Curve>();
            /// <summary>
            /// 出力時の色を設定します。
            /// </summary>
            public RealObject.Color Color = new RealObject.Color();

            /// <summary>
            /// 平面図を出力します。
            /// </summary>
            /// <returns>平面図</returns>
            public Curve[] GetCurve()
            {
                Curve[] Result = new Curve[Content.Count()];
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result[i] = (this.Content[i].DuplicateCurve());
                }
                return Result;
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <returns>複製結果</returns>
            public Member Duplicate()
            {
                Member Result = new Member(this.Name);
                Result.Point = this.Point;
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.Content.Add(this.Content[i].DuplicateCurve());
                }

                return Result;
            }

            /// <summary>
            /// Transformを適用します。
            /// </summary>
            /// <param name="Tf">Transform</param>
            public void Transform(Transform Tf)
            {
                for (int i = 0; i < Content.Count(); i++)
                {
                    Content[i].Transform(Tf);
                }
            }

            /// <summary>
            /// Bake(Rhino Documentに出力)します。
            /// </summary>
            /// <param name="RhinoDocument">出力先</param>
            /// <param name="LayerNameHead">レイヤー名の</param>
            public void Bake(RhinoDoc RhinoDocument, String LayerNameHead)
            {
                string LayerName = LayerNameHead + this.Name;
                int layerIndex = RhinoDocument.Layers.Add(LayerName, System.Drawing.Color.Black);
                if (RhinoDocument.Layers.FindByFullPath(LayerName, true) >= 0) { layerIndex = RhinoDocument.Layers.FindByFullPath(LayerName, true); }
                int GroupIndex = RhinoDocument.Groups.Add(this.ToString() + Guid.NewGuid().ToString());
                foreach (Curve cv in Content)
                {
                    ObjectAttributes oba = new ObjectAttributes();
                    oba.LayerIndex = layerIndex;

                    oba.ColorSource = this.Color.IsUndefined ? ObjectColorSource.ColorFromLayer : ObjectColorSource.ColorFromObject;
                    oba.ObjectColor = (System.Drawing.Color)this.Color;
                    oba.AddToGroup(GroupIndex);
                    RhinoDocument.Objects.AddCurve(cv, oba);
                }
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="Name">部材名</param>
            public Member(string Name)
            {
                this.Name = Name;
            }
        }
    }
}
