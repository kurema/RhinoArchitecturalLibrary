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
    /// 3Dモデルを示します。レイヤー付きで出力できます。
    /// </summary>
    public class RealObject
    {
        /// <summary>
        /// 素材を示します。識別以上の意味はありません。
        /// </summary>
        public class Material
        {
            /// <summary>
            /// 素材名
            /// </summary>
            public String[] Name { get; private set; }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="name">素材名</param>
            public Material(string name)
            {
                Name = name.Split('.');
            }

            public override string ToString()
            {
                return String.Join(".", Name);
            }
        }

        /// <summary>
        /// 色を示します。
        /// </summary>
        public class Color
        {
            /// <summary>
            /// 色の名前を示します。
            /// </summary>
            public string Name = "";

            /// <summary>
            /// ランダムであるかを示します。
            /// </summary>
            public bool IsRandom { get; set; }
            /// <summary>
            /// 未設定であるかを示します。
            /// </summary>
            public bool IsUndefined { get { return !IsRandom && _IsUndefined; } }
            private bool _IsUndefined;

            private double[] _ARGBValue;
            /// <summary>
            /// 色の透明度を示します。
            /// </summary>
            public double Alpha { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[0]; } } }
            /// <summary>
            /// 色の赤成分を示します。
            /// </summary>
            public double Red { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[1]; } } }
            /// <summary>
            /// 色の緑成分を示します。
            /// </summary>
            public double Green { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[2]; } } }
            /// <summary>
            /// 色の青成分を示します。
            /// </summary>
            public double Blue { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[3]; } } }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            public Color()
            {
                _IsUndefined = true;
                _ARGBValue = new double[] { 0.0, 0.0, 0.0, 0.0 };
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="name">色名</param>
            public Color(string name) : this() { this.Name = name; }

            /// <summary>
            /// 色を設定します。
            /// </summary>
            /// <param name="A">アルファ</param>
            /// <param name="R">赤</param>
            /// <param name="G">緑</param>
            /// <param name="B">青</param>
            public void SetARGB(double A, double R, double G, double B)
            {
                _IsUndefined = false;
                IsRandom = true;
                if (Math.Max(Math.Max(A, R), Math.Max(G, B)) > 1.0)
                {
                    A /= 255.0; R /= 255.0; G /= 255.0; B /= 255.0;
                }
                _ARGBValue[0] = A;
                _ARGBValue[1] = R;
                _ARGBValue[2] = G;
                _ARGBValue[3] = B;
            }

            /// <summary>
            /// 色を設定します。
            /// </summary>
            /// <param name="R">赤</param>
            /// <param name="G">緑</param>
            /// <param name="B">青</param>
            public void SetRGB(double R, double G, double B)
            {
                SetARGB(1.0, R, G, B);
                _ARGBValue[0] = 1.0;
            }

            /// <summary>
            /// ランダムに色を設定します。
            /// </summary>
            public void SetRandomColor()
            {
                _IsUndefined = false;
                IsRandom = true;
                _ARGBValue[0] = rd.NextDouble();
                _ARGBValue[1] = rd.NextDouble();
                _ARGBValue[2] = rd.NextDouble();
                _ARGBValue[3] = rd.NextDouble();
            }

            private Random rd = new Random();

            public static explicit operator System.Drawing.Color(Color cl)
            {
                return System.Drawing.Color.FromArgb((int)(cl.Alpha * 256), (int)(cl.Red * 256), (int)(cl.Green * 256), (int)(cl.Blue * 256));
            }
            public static implicit operator Color(System.Drawing.Color cl)
            {
                Color ret = new Color();
                ret.SetARGB(cl.A / 255.0, cl.R / 255.0, cl.G / 255.0, cl.B / 255.0);
                if (cl.IsKnownColor) { ret.Name = cl.ToKnownColor().ToString(); }
                return ret;
            }

        }

        /// <summary>
        /// 部材(建築の要素)を示します。
        /// </summary>
        public class Member
        {
            /// <summary>
            /// 部材名
            /// </summary>
            public String[] Name { get; private set; }
            /// <summary>
            /// Brepを示します。
            /// </summary>
            public Brep[] Content = new Brep[0];
            /// <summary>
            /// 部材の色。
            /// </summary>
            public Color Color = new Color();
            /// <summary>
            /// 部材のマテリアル
            /// </summary>
            public Material Material = new Material("Undefined");
            /// <summary>
            /// 詳細レベル。RODを用いる場合を想定しています。
            /// 現時点で余り利用されていません。
            /// </summary>
            public double DeatailLevel;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="name">名前</param>
            public Member(string name)
            {
                SetName(name);
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="name">名前</param>
            /// <param name="content">Brep(3Dモデル)</param>
            public Member(string name, params Brep[] content) : this(name)
            {
                this.Content = content;
            }

            /// <summary>
            /// 名前を設定します。
            /// </summary>
            /// <param name="name">名前</param>
            public void SetName(string name)
            {
                Name = name.Split('.');
            }

            public static implicit operator Brep[] (Member origin)
            {
                return origin.Content;
            }

            public static implicit operator Member(Brep[] origin)
            {
                return new Member("BrepMember", origin);
            }

            public override string ToString()
            {
                return String.Join(".", Name);
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <returns>複製結果</returns>
            public Member Duplicate()
            {
                return new Member(this.ToString(), GeneralHelper.DuplicateBreps(this.Content)) { Color = this.Color, Material = this.Material, DeatailLevel = this.DeatailLevel };
            }

            /// <summary>
            /// Transformを適用します。
            /// </summary>
            /// <param name="Tf">Transform</param>
            public void Transform(Transform tf)
            {
                if (Content == null) { return; }
                foreach (Brep bp in Content)
                {
                    bp.Transform(tf);
                }
            }

            /// <summary>
            /// Bake(Rhino Documentに出力)します。
            /// </summary>
            /// <param name="doc">出力先</param>
            /// <param name="LayerName">レイヤー名</param>
            public void Bake(RhinoDoc doc, string LayerName)
            {
                if (Content == null) { return; }
                int layerIndex = doc.Layers.Add(LayerName, System.Drawing.Color.Black);
                if (doc.Layers.FindByFullPath(LayerName, true) >= 0) { layerIndex = doc.Layers.FindByFullPath(LayerName, true); }
                int GroupIndex = doc.Groups.Add(this.ToString() + Guid.NewGuid().ToString());
                foreach (Brep bp in Content)
                {
                    ObjectAttributes oba = new ObjectAttributes();
                    oba.LayerIndex = layerIndex;

                    oba.ColorSource = this.Color.IsUndefined ? ObjectColorSource.ColorFromLayer : ObjectColorSource.ColorFromObject;
                    oba.ObjectColor = (System.Drawing.Color)this.Color;
                    oba.AddToGroup(GroupIndex);
                    doc.Objects.AddBrep(bp, oba);
                }
            }
        }

        /// <summary>
        /// 建築全体を示します。レイヤー名付きで出力可能です。
        /// </summary>
        public class Building
        {
            /// <summary>
            /// 名前
            /// </summary>
            public String[] Name { get; private set; }
            /// <summary>
            /// 建築に含まれる要素を示します。
            /// </summary>
            public List<Member> Content;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="name">名前</param>
            public Building(string name)
            {
                Name = name.Split('.');
                Content = new List<Member>();
            }

            public override string ToString()
            {
                return String.Join(".", Name);
            }
            /// <summary>
            /// Bake(Rhino Documentに出力)します。
            /// </summary>
            /// <param name="rd">ランダムインスタンス</param>
            public void Bake(RhinoDoc rd)
            {
                foreach (Member mb in Content)
                {
                    mb.Bake(rd, this.ToString() + ":" + mb.ToString());
                }
            }

            /// <summary>
            /// 部材をつかします。
            /// </summary>
            /// <param name="item">追加部材</param>
            public void Add(params Member[] item)
            {
                Content.AddRange(item);
            }

            /// <summary>
            /// 3Dモデル(Brep)から部材(Member)を作ります。
            /// </summary>
            /// <param name="name">部材名</param>
            /// <param name="brep">Brep</param>
            /// <returns></returns>
            public Member Add(string name, params Brep[] brep)
            {
                Member result = new Member(name, brep);
                this.Add(result);
                return result;
            }

            /// <summary>
            /// Transformを適用します。
            /// </summary>
            /// <param name="Tf">Transform</param>
            public void Transform(Transform tf)
            {
                foreach (Member m in Content)
                {
                    m.Transform(tf);
                }
            }

            /// <summary>
            /// 他の建築と結合します。
            /// </summary>
            /// <param name="item">追加する建築</param>
            public void Add(params Building[] item)
            {
                foreach (Building bd in item)
                {
                    foreach (Member mb in bd.Content)
                    {
                        mb.SetName(bd.ToString() + "." + mb.ToString());
                        Content.Add(mb);
                    }
                }
            }
        }

        /// <summary>
        /// 最低限の補助関数を含みます。
        /// </summary>
        public static class Helper
        {
            /// <summary>
            /// ランダムな色を取得します。
            /// </summary>
            /// <param name="rd"></param>
            /// <returns></returns>
            public static System.Drawing.Color GetRandomColor(Random rd)
            {
                return System.Drawing.Color.FromArgb(rd.Next(256), rd.Next(256), rd.Next(256));
            }

            /// <summary>
            /// Bake(Rhino Documentに出力)します。
            /// </summary>
            /// <param name="Content">出力するBrep</param>
            /// <param name="doc">出力先のドキュメント</param>
            /// <param name="LayerName">出力レイヤー名</param>
            /// <param name="color">出力時の色</param>
            public static void Bake(Brep[] Content, RhinoDoc doc, string LayerName, System.Drawing.Color color)
            {
                int LayerIndex = doc.Layers.Add(LayerName, color);
                foreach (Brep bp in Content)
                {
                    ObjectAttributes oba = new ObjectAttributes();
                    oba.LayerIndex = LayerIndex;
                    doc.Objects.AddBrep(bp, oba);
                }
            }
        }
    }
}