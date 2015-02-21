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

using System.Xml.Serialization;


namespace kurema
{
    namespace RhinoTools
    {
        public class PanelBuilder
        {
            public Rhino.RhinoDoc Document;
            public Random Rand;
            public string CurrentDirectory;
            public int CurrentLayer = 0;

            public PanelBuilder(RhinoDoc Doc,string currentDirectory)
            {
                Document = Doc;
                Rand = new Random();
                this.CurrentDirectory = currentDirectory;
            }

            public void Add(string name, Color col, params Brep[] content)
            {
                int layerIndex = Document.Layers.Add(name, System.Drawing.Color.Black);
                if (Document.Layers.FindByFullPath(name, true) >= 0) { layerIndex = Document.Layers.FindByFullPath(name, true); }
                int GroupIndex = Document.Groups.Add(name + Rand.Next(100000));

                Material m = new Material();
                m.DiffuseColor = col;
                int matidx = Document.Materials.Add(m);

                foreach (Brep bp in content)
                {
                    ObjectAttributes oba = new ObjectAttributes();
                    oba.LayerIndex = layerIndex;
                    oba.AddToGroup(GroupIndex);
                    oba.MaterialIndex = matidx;
                    oba.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                    Document.Objects.AddBrep(bp, oba);
                }
            }

            public void AddPaperSizeA(int a, bool Landscape)
            {
                AddPaperSizeA(a, Landscape, Color.White);
            }

            public void AddPaperSizeA(int a, bool Landscape,Color col)
            {
                this.Add("Paper:A"+a,col,Providers.GetPaperSizeA(a, Landscape));
            }

            public void Pin(Point2d point)
            {
                RealObject.Building pin = Providers.GetThumbtack();
                pin.Transform(Transform.Rotation(-Math.PI / 2.0, Point3d.Origin));

                double AngleDifference = Math.PI / 12.0;
                Random rd=new Random();
                pin.Transform(Transform.Rotation(-AngleDifference/2.0+Rand.NextDouble()*AngleDifference/2.0,new Vector3d(0,0,1),Point3d.Origin));
                pin.Transform(Transform.Rotation(-AngleDifference/2.0+Rand.NextDouble()*AngleDifference/2.0,new Vector3d(1,0,0),Point3d.Origin));
                pin.Transform(Transform.Translation(point.X, -5.0, point.Y));

                pin.Bake(Document);
            }


            public void AddColorPaper(Color col,Point2d point, double width, double height)
            {
                Brep paper = Brep.CreatePlanarBreps(new Rectangle3d(new Plane(new Point3d(point.X, -0.5, point.Y), Vector3d.XAxis, Vector3d.ZAxis),width,height).ToNurbsCurve())[0];
                Material m = new Material();
                m.DiffuseColor = col;
                int matidx = Document.Materials.Add(m);

                string name = "ColorPaper";
                int layerIndex = Document.Layers.Add(name, System.Drawing.Color.Black);
                if (Document.Layers.FindByFullPath(name, true) >= 0) { layerIndex = Document.Layers.FindByFullPath(name, true); }
                ObjectAttributes oba = new ObjectAttributes();
                oba.LayerIndex = layerIndex;
                oba.MaterialIndex = matidx;
                oba.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                Document.Objects.AddBrep(paper, oba);
            }

            public void AddText3d(string text, double height, double thickness, Point2d point, Color cl)
            {
                Brep[] texts = Providers.GetTextBrep(text, height, thickness);
                texts = GeneralHelper.RotateBreps(texts, Math.PI / 2.0, Vector3d.XAxis);
                texts = GeneralHelper.TranslateBreps(texts, new Point3d(point.X, 0, point.Y));
                this.Add("Text3d", cl, texts);
            }
            public void AddText3d(string text, double height, double thickness, Point2d point, Color cl,string fontname)
            {
                Brep[] texts = Providers.GetTextBrep(text, height, thickness,fontname,false,false,this.Document);
                texts = GeneralHelper.RotateBreps(texts, Math.PI / 2.0, Vector3d.XAxis);
                texts = GeneralHelper.TranslateBreps(texts, new Point3d(point.X, 0, point.Y));
                this.Add("Text3d", cl, texts);
            }

            public void AddPhoto(string url, double dpi, Point2d point)
            {
                System.Drawing.Bitmap bmp = new Bitmap(url);
                double inchToMm = 25.4;

                Brep paper = Brep.CreatePlanarBreps(new Rectangle3d(new Plane(new Point3d(0, 0, 0), Vector3d.XAxis, Vector3d.ZAxis), new Point3d(-bmp.Width / dpi * inchToMm / 2.0, 0, 0), new Point3d(bmp.Width / dpi * inchToMm / 2.0, 0, -bmp.Height / dpi * inchToMm)).ToNurbsCurve())[0];

                Material m=new Material();
                m.SetBitmapTexture(url);
                int matidx= Document.Materials.Add(m);

                paper.Translate(point.X, -2.5 + CurrentLayer*0.1, point.Y);

                string name = "Photo";
                int layerIndex = Document.Layers.Add(name, System.Drawing.Color.Black);
                if (Document.Layers.FindByFullPath(name, true) >= 0) { layerIndex = Document.Layers.FindByFullPath(name, true); }
                ObjectAttributes oba = new ObjectAttributes();
                oba.LayerIndex = layerIndex;
                oba.MaterialIndex = matidx;
                oba.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                Document.Objects.AddBrep(paper, oba);

                this.Pin(point + new Vector2d(0, -10));

                CurrentLayer++;
            }

            public void AddTextXml(string url, Point2d point)
            {
                AddTextXml(Generator.ReadTextXml(url), System.IO.Path.GetFileNameWithoutExtension(url), point);
            }

            public void AddTextXml(Schemas.text xmlobj,string filename, Point2d point)
            {
                double scale = 5.0;
                Bitmap bp = Generator.CreateTextBitmap(xmlobj, scale);
                string fn = GetImageSavePath(filename);
                if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(fn))) {System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fn)); }
                bp.Save(fn);

                this.AddPhoto(fn, 72 * scale, point);
            }

            public string GetImageSavePath(string filename)
            {
                return CurrentDirectory + @"\Texture\" + filename + ".png";
            }

            public void AddText(string head, string Content, double width,double height, Point2d point)
            {
                Schemas.text txt = new Schemas.text();
                txt.width = width;
                txt.height = height;
                string[] SeparetedContent = Content.Split('\n');
                List<Object> Body = new List<object>();
                Body.Add(head);

                foreach (string st in SeparetedContent)
                {
                    Schemas.textParagraph1 tp1 = new Schemas.textParagraph1();
                    tp1.Items = new string[] { st };
                    tp1.ItemsElementName = new Schemas.ItemsChoiceType[]{ Schemas.ItemsChoiceType.plain };
                    Body.Add(tp1);
                }
                txt.body = Body.ToArray();

                AddTextXml(txt,Rand.Next(0,(int)1e5).ToString(), point);
            }

            public void AddTextSimple(string text, string fontname, float fontsize, Point2d point, double marginh, double marginv)
            {
                AddTextSimple(text, fontname, fontsize, new FontStyle(), point, marginh, marginv, Color.White, Color.Black);
            }

            public void AddTextSimple(string text, string fontname, float fontsize, FontStyle fs, Point2d point, double marginh, double marginv, Color Background, Color ForeGround)
            {
                float scale = 5;
                Bitmap bp = new Bitmap(640, 480);
                System.Drawing.Font f = new System.Drawing.Font(fontname, fontsize * scale, fs);
                Graphics g = Graphics.FromImage(bp);
                SizeF size = g.MeasureString(text, f);
                int pwidth = (int)((size.Width + marginh * 2 * scale));
                int pheight = (int)((size.Height + marginv * 2 * scale));
                bp = new Bitmap(pwidth, pheight);
                g = Graphics.FromImage(bp);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString(text, f, new SolidBrush(ForeGround), new PointF((int)(marginh * scale), (int)(marginv * scale)));
                string filename = GetImageSavePath("TextSimple" + text);
                bp.Save(filename);

                Brep paper = Brep.CreatePlanarBreps(new Rectangle3d(new Plane(new Point3d(point.X - pwidth / scale / 2.0, -1.0, point.Y - pheight / scale / 2.0), Vector3d.XAxis, Vector3d.ZAxis), pwidth / scale, pheight / scale).ToNurbsCurve())[0];

                Material m = new Material();
                m.SetBitmapTexture(filename);
                m.DiffuseColor = Background;
                int matidx = Document.Materials.Add(m);

                string name = "Tag";
                int layerIndex = Document.Layers.Add(name, System.Drawing.Color.Black);
                if (Document.Layers.FindByFullPath(name, true) >= 0) { layerIndex = Document.Layers.FindByFullPath(name, true); }
                ObjectAttributes oba = new ObjectAttributes();
                oba.LayerIndex = layerIndex;
                oba.MaterialIndex = matidx;
                oba.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                Document.Objects.AddBrep(paper, oba);
            }

            public void AddTag(string text, string fontname, double fontSize,double width,double height,Color cl,Point2d point,double yvalue)
            {
                Curve cv = Curve.JoinCurves(new Curve[] { new Line(0, 0, 0, width / 2.5, 0, 0).ToNurbsCurve() ,new Arc(new Point3d(width / 2.5, 0, 0),Vector3d.XAxis, new Point3d(width, -10, 0)).ToNurbsCurve()})[0];
                //Curve cv = Curve.CreateInterpolatedCurve(new Point3d[] { new Point3d(0, 0, 0), new Point3d(width / 2.5, 0, 0), }, 5);
                cv = cv.Rebuild(10, 5, true);
                Surface ExtSrf = Surface.CreateExtrusion(cv, new Vector3d(0, 0, height));
                ExtSrf = ExtSrf.Transpose();
                ExtSrf.Translate(point.X, -yvalue, point.Y);

                double scale = 3.0;
                Bitmap bp = new Bitmap((int)(scale * cv.GetLength()), (int)(scale * height));
                Graphics g = Graphics.FromImage(bp);
                g.FillRectangle(new SolidBrush(cl), new Rectangle(0, 0, (int)(height*scale), (int)(height*scale)));
                System.Drawing.Font font=new System.Drawing.Font(fontname, (float)(fontSize*scale));
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString(text, font, Brushes.Black, new PointF((float)(scale * height), (float)((scale * height - font.GetHeight(72)) / 2.0)));

                string filenamebody = text;
                char[] invalidchar = Path.GetInvalidFileNameChars();
                foreach (char c in invalidchar)
                {
                    filenamebody = filenamebody.Replace(c, '_');
                }
                string filename = GetImageSavePath("Tag" + filenamebody);
                bp.Save(filename);

                Material m = new Material();
                m.SetBitmapTexture(filename);
                m.DiffuseColor = Color.White;
                int matidx = Document.Materials.Add(m);

                string name = "Tag";
                int layerIndex = Document.Layers.Add(name, System.Drawing.Color.Black);
                if (Document.Layers.FindByFullPath(name, true) >= 0) { layerIndex = Document.Layers.FindByFullPath(name, true); }
                ObjectAttributes oba = new ObjectAttributes();
                oba.LayerIndex = layerIndex;
                oba.MaterialIndex = matidx;
                oba.MaterialSource = ObjectMaterialSource.MaterialFromObject;
                Document.Objects.AddBrep(Brep.CreateFromSurface(ExtSrf), oba);
            }



            public class Schemas
            {
                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Diagnostics.DebuggerStepThroughAttribute()]
                [System.ComponentModel.DesignerCategoryAttribute("code")]
                [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "kurema/PanelLayout/0.0")]
                [System.Xml.Serialization.XmlRootAttribute(Namespace = "kurema/PanelLayout/0.0", IsNullable = false)]
                public partial class text
                {

                    private textHeading headingField;

                    private textParagraph paragraphField;

                    private object[] bodyField;

                    private double widthField;

                    private double heightField;

                    public text()
                    {
                        this.widthField = 640D;
                        this.heightField = 480D;
                    }

                    /// <remarks/>
                    public textHeading heading
                    {
                        get
                        {
                            return this.headingField;
                        }
                        set
                        {
                            this.headingField = value;
                        }
                    }

                    /// <remarks/>
                    public textParagraph paragraph
                    {
                        get
                        {
                            return this.paragraphField;
                        }
                        set
                        {
                            this.paragraphField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlArrayItemAttribute("heading", typeof(string), IsNullable = false)]
                    [System.Xml.Serialization.XmlArrayItemAttribute("paragraph", typeof(textParagraph1), IsNullable = false)]
                    public object[] body
                    {
                        get
                        {
                            return this.bodyField;
                        }
                        set
                        {
                            this.bodyField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    [System.ComponentModel.DefaultValueAttribute(640D)]
                    public double width
                    {
                        get
                        {
                            return this.widthField;
                        }
                        set
                        {
                            this.widthField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    [System.ComponentModel.DefaultValueAttribute(480D)]
                    public double height
                    {
                        get
                        {
                            return this.heightField;
                        }
                        set
                        {
                            this.heightField = value;
                        }
                    }
                }

                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Diagnostics.DebuggerStepThroughAttribute()]
                [System.ComponentModel.DesignerCategoryAttribute("code")]
                [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "kurema/PanelLayout/0.0")]
                public partial class textHeading
                {

                    private style styleField;

                    /// <remarks/>
                    public style style
                    {
                        get
                        {
                            return this.styleField;
                        }
                        set
                        {
                            this.styleField = value;
                        }
                    }
                }

                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Diagnostics.DebuggerStepThroughAttribute()]
                [System.ComponentModel.DesignerCategoryAttribute("code")]
                [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "kurema/PanelLayout/0.0")]
                public partial class style
                {

                    private styleFont fontField;

                    private styleMargin marginField;

                    /// <remarks/>
                    public styleFont font
                    {
                        get
                        {
                            return this.fontField;
                        }
                        set
                        {
                            this.fontField = value;
                        }
                    }

                    /// <remarks/>
                    public styleMargin margin
                    {
                        get
                        {
                            return this.marginField;
                        }
                        set
                        {
                            this.marginField = value;
                        }
                    }
                }

                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Diagnostics.DebuggerStepThroughAttribute()]
                [System.ComponentModel.DesignerCategoryAttribute("code")]
                [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "kurema/PanelLayout/0.0")]
                public partial class styleFont
                {

                    private string nameField;

                    private double sizeField;

                    private bool sizeFieldSpecified;

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    public string name
                    {
                        get
                        {
                            return this.nameField;
                        }
                        set
                        {
                            this.nameField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    public double size
                    {
                        get
                        {
                            return this.sizeField;
                        }
                        set
                        {
                            this.sizeField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlIgnoreAttribute()]
                    public bool sizeSpecified
                    {
                        get
                        {
                            return this.sizeFieldSpecified;
                        }
                        set
                        {
                            this.sizeFieldSpecified = value;
                        }
                    }
                }

                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Diagnostics.DebuggerStepThroughAttribute()]
                [System.ComponentModel.DesignerCategoryAttribute("code")]
                [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "kurema/PanelLayout/0.0")]
                public partial class styleMargin
                {

                    private double leftField;

                    private double rightField;

                    private double topField;

                    private double bottomField;

                    public styleMargin()
                    {
                        this.leftField = 0D;
                        this.rightField = 0D;
                        this.topField = 0D;
                        this.bottomField = 0D;
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    [System.ComponentModel.DefaultValueAttribute(0D)]
                    public double left
                    {
                        get
                        {
                            return this.leftField;
                        }
                        set
                        {
                            this.leftField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    [System.ComponentModel.DefaultValueAttribute(0D)]
                    public double right
                    {
                        get
                        {
                            return this.rightField;
                        }
                        set
                        {
                            this.rightField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    [System.ComponentModel.DefaultValueAttribute(0D)]
                    public double top
                    {
                        get
                        {
                            return this.topField;
                        }
                        set
                        {
                            this.topField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlAttributeAttribute()]
                    [System.ComponentModel.DefaultValueAttribute(0D)]
                    public double bottom
                    {
                        get
                        {
                            return this.bottomField;
                        }
                        set
                        {
                            this.bottomField = value;
                        }
                    }
                }

                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Diagnostics.DebuggerStepThroughAttribute()]
                [System.ComponentModel.DesignerCategoryAttribute("code")]
                [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "kurema/PanelLayout/0.0")]
                public partial class textParagraph
                {

                    private style styleField;

                    /// <remarks/>
                    public style style
                    {
                        get
                        {
                            return this.styleField;
                        }
                        set
                        {
                            this.styleField = value;
                        }
                    }
                }

                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Diagnostics.DebuggerStepThroughAttribute()]
                [System.ComponentModel.DesignerCategoryAttribute("code")]
                [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "kurema/PanelLayout/0.0")]
                public partial class textParagraph1
                {

                    private string[] itemsField;

                    private ItemsChoiceType[] itemsElementNameField;

                    /// <remarks/>
                    [System.Xml.Serialization.XmlElementAttribute("bold", typeof(string))]
                    [System.Xml.Serialization.XmlElementAttribute("plain", typeof(string))]
                    [System.Xml.Serialization.XmlElementAttribute("underline", typeof(string))]
                    [System.Xml.Serialization.XmlChoiceIdentifierAttribute("ItemsElementName")]
                    public string[] Items
                    {
                        get
                        {
                            return this.itemsField;
                        }
                        set
                        {
                            this.itemsField = value;
                        }
                    }

                    /// <remarks/>
                    [System.Xml.Serialization.XmlElementAttribute("ItemsElementName")]
                    [System.Xml.Serialization.XmlIgnoreAttribute()]
                    public ItemsChoiceType[] ItemsElementName
                    {
                        get
                        {
                            return this.itemsElementNameField;
                        }
                        set
                        {
                            this.itemsElementNameField = value;
                        }
                    }
                }

                /// <remarks/>
                [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
                [System.SerializableAttribute()]
                [System.Xml.Serialization.XmlTypeAttribute(Namespace = "kurema/PanelLayout/0.0", IncludeInSchema = false)]
                public enum ItemsChoiceType
                {

                    /// <remarks/>
                    bold,

                    /// <remarks/>
                    plain,

                    /// <remarks/>
                    underline,
                }


            }

            public class Generator
            {
                public static Schemas.text ReadTextXml(string fileName)
                {
                    System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(Schemas.text));
                    System.IO.StreamReader sr = new System.IO.StreamReader(fileName, new System.Text.UTF8Encoding(false));
                    Schemas.text text = (Schemas.text)serializer.Deserialize(sr);
                    sr.Close();
                    return text;
                }

                public static Bitmap CreateTextBitmap(Schemas.text text, double zoom = 1.0)
                {

                    int x = (int)(text.width * zoom);
                    int y = (int)(text.height * zoom);

                    double mainMarginT = 0, mainMarginL = 0, mainMarginR = 0, mainMarginB = 0;
                    double headMarginT = 0, headMarginL = 0, headMarginR = 0, headMarginB = 0;

                    System.Drawing.Font HeadingFont;
                    System.Drawing.Font MainFont;
                    System.Drawing.Font MainFontBold;
                    System.Drawing.Font MainFontUnderline;

                    string fontnamedefault = "游明朝";
                    {
                        float fontsizedefault = (float)(12);
                        string fontname = "";
                        float fontsize = fontsizedefault;
                        if (text.paragraph == null)
                        {
                            fontname = fontnamedefault;
                            fontsize = fontsizedefault;

                            mainMarginT = mainMarginL = mainMarginR = mainMarginT = 0.0;
                        }
                        else
                        {
                            fontname = text.paragraph.style.font.name == "" ? fontnamedefault : text.paragraph.style.font.name;
                            fontsize = text.paragraph.style.font.size <= 0 ? fontsizedefault : (float)text.paragraph.style.font.size;

                            mainMarginT = text.paragraph.style.margin.top * zoom;
                            mainMarginL = text.paragraph.style.margin.left * zoom;
                            mainMarginR = text.paragraph.style.margin.right * zoom;
                            mainMarginB = text.paragraph.style.margin.bottom * zoom;
                        }
                        MainFont = new System.Drawing.Font(fontname, fontsize * (float)zoom);
                        MainFontBold = new System.Drawing.Font(fontname, fontsize * (float)zoom, FontStyle.Bold);
                        MainFontUnderline = new System.Drawing.Font(fontname, fontsize * (float)zoom, FontStyle.Underline);
                    }
                    {
                        float fontsizedefault = 25;
                        string fontname = "";
                        float fontsize = fontsizedefault;
                        if (text.heading == null)
                        {
                            fontname = fontnamedefault;
                            fontsize = fontsizedefault;

                            headMarginT = headMarginL = headMarginR = headMarginT = 0.0;
                        }
                        else
                        {
                            fontname = text.heading.style.font.name == "" ? fontnamedefault : text.heading.style.font.name;
                            fontsize = text.heading.style.font.size <= 0 ? fontsizedefault : (float)text.heading.style.font.size;

                            headMarginT = text.heading.style.margin.top * zoom;
                            headMarginL = text.heading.style.margin.left * zoom;
                            headMarginR = text.heading.style.margin.right * zoom;
                            headMarginB = text.heading.style.margin.bottom * zoom;
                        }
                        HeadingFont = new System.Drawing.Font(fontname, fontsize * (float)zoom);
                    }

                    Bitmap img = new Bitmap(x, y);
                    Graphics g = Graphics.FromImage(img);
                    g.FillRectangle(Brushes.White, 0, 0, x, y);
                    double CurrentY = 0;
                    double CurrentX = 0;

                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    for (int i = 0; i < text.body.GetLength(0); i++)
                    {
                        if (text.body[i] is string)
                        {
                            CurrentX = headMarginL;
                            CurrentY += headMarginT;

                            g.DrawString((string)text.body[i], HeadingFont, Brushes.Black, new PointF((int)CurrentX, (int)CurrentY));
                            CurrentY += HeadingFont.Height;

                            CurrentY += headMarginB;
                        }
                        else
                        {
                            CurrentX = mainMarginL;
                            CurrentY += mainMarginT;
                            for (int j = 0; j < ((Schemas.textParagraph1)text.body[i]).Items.GetLength(0); j++)
                            {
                                System.Drawing.Font font;
                                if (((Schemas.textParagraph1)text.body[i]).ItemsElementName[j] == Schemas.ItemsChoiceType.bold)
                                {
                                    font = MainFontBold;
                                }
                                else if (((Schemas.textParagraph1)text.body[i]).ItemsElementName[j] == Schemas.ItemsChoiceType.underline)
                                {
                                    font = MainFontUnderline;
                                }
                                else
                                {
                                    font = MainFont;
                                }

                                string baseText = ((Schemas.textParagraph1)text.body[i]).Items[j];
                                while (CurrentX + g.MeasureString(baseText, font).Width > x - mainMarginR && baseText.Length > 0)
                                {
                                    string currentText = baseText;
                                    int SubTxtCount = 0;
                                    while (CurrentX + g.MeasureString(currentText, font).Width > x - mainMarginR && currentText.Length > 0)
                                    {
                                        SubTxtCount++;
                                        currentText = baseText.Substring(0, baseText.Length - SubTxtCount);
                                    }
                                    if (SubTxtCount > 0)
                                    {
                                        g.DrawString(currentText, font, Brushes.Black, new PointF((int)CurrentX, (int)CurrentY));
                                        CurrentY += MainFont.Height;
                                        CurrentX = mainMarginR;
                                        baseText = baseText.Substring(baseText.Length - SubTxtCount);
                                        //g.DrawString(baseText.Substring(SubTxtCount+2), font, Brushes.Black, new PointF((int)CurrentX, (int)CurrentY));

                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                g.DrawString(baseText, font, Brushes.Black, new PointF((int)CurrentX, (int)CurrentY));
                                CurrentX += g.MeasureString(baseText, font).Width;
                            }
                            CurrentY += MainFont.Height;
                            CurrentY += mainMarginB;
                        }
                    }
                    g.Dispose();
                    return img;
                }
            }

        }
    }
}
