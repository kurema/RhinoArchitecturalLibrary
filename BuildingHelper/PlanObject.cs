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
        public class PlanObject
        {
            public class Building
            {
                public String Name;
                public List<Floor> Content = new List<Floor>();

                public Curve[] GetCurve()
                {
                    List<Curve> Result = new List<Curve>();
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        Result.AddRange(Content[i].GetCurve());
                    }
                    return Result.ToArray();
                }

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

                public void Bake(RhinoDoc RhinoDocument, int TargetFloor)
                {
                    Content[TargetFloor].Bake(RhinoDocument, this.Name + ":");
                    
                }

                public Building Duplicate()
                {
                    Building Result = new Building(this.Name);
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        Result.Content.Add(this.Content[i].Duplicate());
                    }
                    return Result;
                }

                public Building(string Name)
                {
                    this.Name = Name;
                }
            }

            public class Floor
            {
                public String Name;
                public double Height;
                public List<Member> Content = new List<Member>();


                public void Bake(RhinoDoc RhinoDocument, string LayerNameHead)
                {
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        Content[i].Bake(RhinoDocument, LayerNameHead+this.Name + ".");
                    }
                }

                public void Transform(Transform Tf)
                {
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        Content[i].Transform(Tf);
                    }
                }
                
                public Curve[] GetCurve()
                {
                    List<Curve> Result = new List<Curve>();
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        Result.AddRange(Content[i].GetCurve());
                    }
                    return Result.ToArray();
                }

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

                public Floor(string Name)
                {
                    this.Name = Name;
                }
            }

            public class Member
            {
                public String Name;

                public Point2d Point;
                public List<Curve> Content = new List<Curve>();
                public RealObject.Color Color = new RealObject.Color();


                public Curve[] GetCurve()
                {
                    Curve[] Result = new Curve[Content.Count()];
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        Result[i] = (this.Content[i].DuplicateCurve());
                    }
                    return Result;
                }

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

                public void Transform(Transform Tf)
                {
                    for (int i = 0; i < Content.Count(); i++)
                    {
                        Content[i].Transform(Tf);
                    }
                }

                public void Bake(RhinoDoc RhinoDocument, String LayerNameHead)
                {
                    string LayerName = LayerNameHead+this.Name;
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

                public Member(string Name)
                {
                    this.Name = Name;
                }
            }
        }
    }
}
