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
    public class RealObject
    {
        public class Material
        {
            public String[] Name { get; private set; }

            public Material(string name)
            {
                Name = name.Split('.');
            }

            public override string ToString()
            {
                return String.Join(".", Name);
            }
        }

        public class Color
        {
            public string Name = "";

            public bool IsRandom { get; set; }
            public bool IsUndefined { get { return !IsRandom && _IsUndefined; } }
            private bool _IsUndefined;

            private double[] _ARGBValue;
            public double Alpha { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[0]; } } }
            public double Red { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[1]; } } }
            public double Green { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[2]; } } }
            public double Blue { get { if (IsRandom) { return rd.NextDouble(); } else { return _ARGBValue[3]; } } }

            public Color()
            {
                _IsUndefined = true;
                _ARGBValue = new double[] { 0.0, 0.0, 0.0, 0.0 };
            }

            public Color(string name) : this() { this.Name = name; }

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

            public void SetRGB(double R, double G, double B)
            {
                SetARGB(1.0, R, G, B);
                _ARGBValue[0] = 1.0;
            }

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

        public class Member
        {
            public String[] Name { get; private set; }

            public Brep[] Content = new Brep[0];

            public Color Color = new Color();

            public Material Material = new Material("Undefined");

            public double DeatailLevel;

            public Member(string name)
            {
                SetName(name);
            }

            public Member(string name, params Brep[] content) : this(name)
            {
                this.Content = content;
            }

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

            public Member Duplicate()
            {
                return new Member(this.ToString(), GeneralHelper.DuplicateBreps(this.Content)) { Color = this.Color, Material = this.Material, DeatailLevel = this.DeatailLevel };
            }

            public void Transform(Transform tf)
            {
                if (Content == null) { return; }
                foreach (Brep bp in Content)
                {
                    bp.Transform(tf);
                }
            }

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

        public class Building
        {
            public String[] Name { get; private set; }
            public List<Member> Content;

            public Building(string name)
            {
                Name = name.Split('.');
                Content = new List<Member>();
            }

            public override string ToString()
            {
                return String.Join(".", Name);
            }

            public void Bake(RhinoDoc rd)
            {
                foreach (Member mb in Content)
                {
                    mb.Bake(rd, this.ToString() + ":" + mb.ToString());
                }
            }

            public void Add(params Member[] item)
            {
                Content.AddRange(item);
            }

            public Member Add(string name, params Brep[] brep)
            {
                Member result = new Member(name, brep);
                this.Add(result);
                return result;
            }

            public void Transform(Transform tf)
            {
                foreach (Member m in Content)
                {
                    m.Transform(tf);
                }
            }

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

        public static class Helper
        {
            public static System.Drawing.Color GetRandomColor(Random rd)
            {
                return System.Drawing.Color.FromArgb(rd.Next(256), rd.Next(256), rd.Next(256));
            }

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