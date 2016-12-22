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
    /// セル・オートマトンに関連するオブジェクトを含みます。
    /// </summary>
    public class CellObject
    {
        //Magic number
        // 0 : No object.
        // -1: No result. This should not be used in IBoxel.
        // -2: Out of range.

        /// <summary>
        /// 3次元グリッドを示します。
        /// </summary>
        public interface IBoxel
        {
            /// <summary>
            /// 指定した大きさで初期化します。
            /// </summary>
            /// <param name="x">x方向の個数</param>
            /// <param name="y">y方向の個数</param>
            /// <param name="z">z方向の個数</param>
            void Init(int x, int y, int z);
            /// <summary>
            /// 指定した座標の周囲のセルに関する情報を取得します。
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <returns>周囲のセルに関する情報</returns>
            NeighborStatus GetNeighbor(int x, int y, int z);
            /// <summary>
            /// セル・オートマトンに関するルールを適用します。
            /// </summary>
            /// <param name="rule">ルール</param>
            void Apply(IRule rule);
            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            IBoxel Duplicate();
        }

        /// <summary>
        /// 通常の3次元グリッドを示します。
        /// </summary>
        public class Boxel : IBoxel
        {
            private int[,,] Content;
            /// <summary>
            /// グリッドのX方向の大きさ(幅)を示します。
            /// </summary>
            public int X { get { return Content.GetLength(0) - 2; } }
            /// <summary>
            /// グリッドのY方向の大きさ(奥行)を示します。
            /// </summary>
            public int Y { get { return Content.GetLength(1) - 2; } }
            /// <summary>
            /// グリッドのZ方向の大きさ(高さ)を示します。
            /// </summary>
            public int Z { get { return Content.GetLength(2) - 2; } }

            /// <summary>
            /// 指定した大きさで初期化します。
            /// </summary>
            /// <param name="x">グリッドのX方向の大きさ(幅)</param>
            /// <param name="y">グリッドのY方向の大きさ(奥行)</param>
            /// <param name="z">グリッドのZ方向の大きさ(高さ)</param>
            public void Init(int x, int y, int z)
            {
                Content = new int[x + 2, y + 2, z + 2];
                for (int cntx = 0; cntx < x + 2; cntx++)
                {
                    for (int cnty = 0; cnty < y + 2; cnty++)
                    {
                        for (int cntz = 0; cntz < z + 2; cntz++)
                        {
                            if (cntx >= 1 && cnty >= 1 && cntz >= 1 && cntx < 1 + x && cnty < 1 + y && cntz < 1 + z)
                            {
                                Content[cntx, cnty, cntz] = 0;
                            }
                            else
                            {
                                Content[cntx, cnty, cntz] = -2;
                            }

                        }
                    }
                }
            }

            /// <summary>
            /// 指定した座標の周囲のセルに関する情報を取得します。
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <returns>周囲のセルに関する情報</returns>
            public NeighborStatus GetNeighbor(int x, int y, int z)
            {
                NeighborStatus result = new NeighborStatus();
                int floor = z;
                result.LowerNeighbor = new int[] { Content[x, y, floor], Content[x + 1, y, floor], Content[x + 2, y, floor]
                        , Content[x, y + 1, floor], Content[x + 2, y + 1, floor]
                        , Content[x, y + 2, floor], Content[x + 1, y + 2, floor], Content[x + 2, y + 2, floor] };
                result.Lower = Content[x + 1, y + 1, floor];
                floor++;
                result.Neighbor = new int[] { Content[x, y, floor], Content[x + 1, y, floor], Content[x + 2, y, floor]
                        , Content[x, y + 1, floor], Content[x + 2, y + 1, floor]
                        , Content[x, y + 2, floor], Content[x + 1, y + 2, floor], Content[x + 2, y + 2, floor] };
                result.Self = Content[x + 1, y + 1, floor];
                floor++;
                result.UpperNeighbor = new int[] { Content[x, y, floor], Content[x + 1, y, floor], Content[x + 2, y, floor]
                        , Content[x, y + 1, floor], Content[x + 2, y + 1, floor]
                        , Content[x, y + 2, floor], Content[x + 1, y + 2, floor], Content[x + 2, y + 2, floor] };
                result.Upper = Content[x + 1, y + 1, floor];
                return result;
            }

            /// <summary>
            /// 指定した座標の値を取得します。
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <returns>値</returns>
            public int GetValue(int x, int y, int z)
            {
                return Content[x + 1, y + 1, z + 1];
            }
            /// <summary>
            /// 指定した座標の値を設定します。
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <param name="Value">値</param>
            public void SetValue(int x, int y, int z, int Value)
            {
                if (Value != -1)
                {
                    Content[x + 1, y + 1, z + 1] = Value;
                }
            }

            /// <summary>
            /// セル・オートマトンに関するルールを適用します。
            /// </summary>
            /// <param name="rule">ルール</param>
            public void Apply(IRule rule)
            {
                Boxel Origin = (Boxel)this.Duplicate();
                for (int cntx = 0; cntx < X; cntx++)
                {
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntz = 0; cntz < Z; cntz++)
                        {
                            this.SetValue(cntx, cnty, cntz, rule.GetStatus(Origin.GetNeighbor(cntx, cnty, cntz), cntx, cnty, cntz));
                        }
                    }
                }
            }

            /// <summary>
            /// 指定した大きさで初期化します。
            /// </summary>
            /// <param name="x">グリッドのX方向の大きさ(幅)</param>
            /// <param name="y">グリッドのY方向の大きさ(奥行)</param>
            /// <param name="z">グリッドのZ方向の大きさ(高さ)</param>
            public Boxel(int x, int y, int z)
            {
                this.Init(x, y, z);
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <returns>複製結果</returns>
            public IBoxel Duplicate()
            {
                Boxel Result = new Boxel(X, Y, Z);
                for (int cntx = 0; cntx < X; cntx++)
                {
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntz = 0; cntz < Z; cntz++)
                        {
                            Result.SetValue(cntx, cnty, cntz, this.GetValue(cntx, cnty, cntz));
                        }
                    }
                }
                return Result;
            }
        }

        /// <summary>
        /// ハニカムグリッドを示します。
        /// </summary>
        public class Honeycomb : IBoxel
        {
            private int[,,] Content;
            /// <summary>
            /// グリッドのX方向の大きさ(幅)を示します。
            /// </summary>
            public int X { get { return Content.GetLength(0) - 2; } }
            /// <summary>
            /// グリッドのY方向の大きさ(奥行)を示します。
            /// </summary>
            public int Y { get { return Content.GetLength(1) - 2; } }
            /// <summary>
            /// グリッドのZ方向の大きさ(高さ)を示します。
            /// </summary>
            public int Z { get { return Content.GetLength(2) - 2; } }

            /// <summary>
            /// 指定した大きさで初期化します。
            /// </summary>
            /// <param name="x">グリッドのX方向の大きさ(幅)</param>
            /// <param name="y">グリッドのY方向の大きさ(奥行)</param>
            /// <param name="z">グリッドのZ方向の大きさ(高さ)</param>
            public void Init(int x, int y, int z)
            {
                Content = new int[x + 2, y + 2, z + 2];
                for (int cntx = 0; cntx < x + 2; cntx++)
                {
                    for (int cnty = 0; cnty < y + 2; cnty++)
                    {
                        for (int cntz = 0; cntz < z + 2; cntz++)
                        {
                            if (cntx >= 1 && cnty >= 1 && cntz >= 1 && cntx < 1 + x && cnty < 1 + y && cntz < 1 + z)
                            {
                                Content[cntx, cnty, cntz] = 0;
                            }
                            else
                            {
                                Content[cntx, cnty, cntz] = -2;
                            }

                        }
                    }
                }
            }

            /// <summary>
            /// 指定した座標の周囲のセルに関する情報を取得します。
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <returns>周囲のセルに関する情報</returns>
            public NeighborStatus GetNeighbor(int x, int y, int z)
            {
                NeighborStatus result = new NeighborStatus();
                int shift = y % 2;
                int floor = z;
                result.LowerNeighbor = new int[] {Content[x + 1+shift, y + 2, floor],Content[x + 2, y + 1, floor],Content[x + 1+shift, y, floor]
                        , Content[x+shift, y, floor], Content[x, y + 1, floor], Content[x+shift, y + 2, floor]
                          };
                result.Lower = Content[x + 1, y + 1, floor];
                floor++;
                result.Neighbor = new int[] { Content[x + 1+shift, y + 2, floor],Content[x + 2, y + 1, floor],Content[x + 1+shift, y, floor]
                        , Content[x+shift, y, floor], Content[x, y + 1, floor], Content[x+shift, y + 2, floor] };
                result.Self = Content[x + 1, y + 1, floor];
                floor++;
                result.UpperNeighbor = new int[] { Content[x + 1+shift, y + 2, floor],Content[x + 2, y + 1, floor],Content[x + 1+shift, y, floor]
                        , Content[x+shift, y, floor], Content[x, y + 1, floor], Content[x+shift, y + 2, floor] };
                result.Upper = Content[x + 1, y + 1, floor];
                return result;
            }

            /// <summary>
            /// 指定した座標の値を取得します。
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <returns>値</returns>
            public int GetValue(int x, int y, int z)
            {
                return Content[x + 1, y + 1, z + 1];
            }

            /// <summary>
            /// 指定した座標の値を設定します。
            /// </summary>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <param name="Value">値</param>
            public void SetValue(int x, int y, int z, int Value)
            {
                if (Value != -1)
                {
                    Content[x + 1, y + 1, z + 1] = Value;
                }
            }

            /// <summary>
            /// セル・オートマトンに関するルールを適用します。
            /// </summary>
            /// <param name="rule">ルール</param>
            public void Apply(IRule rule)
            {
                Honeycomb Origin = (Honeycomb)this.Duplicate();
                for (int cntx = 0; cntx < X; cntx++)
                {
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntz = 0; cntz < Z; cntz++)
                        {
                            this.SetValue(cntx, cnty, cntz, rule.GetStatus(Origin.GetNeighbor(cntx, cnty, cntz), cntx, cnty, cntz));
                        }
                    }
                }
            }

            /// <summary>
            /// 指定した大きさで初期化します。
            /// </summary>
            /// <param name="x">グリッドのX方向の大きさ(幅)</param>
            /// <param name="y">グリッドのY方向の大きさ(奥行)</param>
            /// <param name="z">グリッドのZ方向の大きさ(高さ)</param>
            public Honeycomb(int x, int y, int z)
            {
                this.Init(x, y, z);
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public IBoxel Duplicate()
            {
                Honeycomb Result = new Honeycomb(X, Y, Z);
                for (int cntx = 0; cntx < X; cntx++)
                {
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntz = 0; cntz < Z; cntz++)
                        {
                            Result.SetValue(cntx, cnty, cntz, this.GetValue(cntx, cnty, cntz));
                        }
                    }
                }
                return Result;
            }

            /// <summary>
            /// 簡単に形状を3Dモデルで取得します。
            /// </summary>
            /// <param name="BuildingHeight">セルの高さ</param>
            /// <returns>3Dモデル</returns>
            public RealObject.Building GetBuildingSimple(double BuildingHeight = 1.0)
            {
                RealObject.Building Result = new RealObject.Building("HoneycombBuilding");

                Brep[] HexagonBase = GeneralHelper.CreateExtrusionCaped(new[] { GetHexagon().ToNurbsCurve() }, new Vector3d(0, 0, BuildingHeight));

                for (int cntx = 0; cntx < X; cntx++)
                {
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntz = 0; cntz < Z; cntz++)
                        {
                            int tmpnum = GetValue(cntx, cnty, cntz);
                            if (tmpnum > 0)
                            {
                                RealObject.Member ToAdd = new RealObject.Member("Type" + tmpnum);
                                Brep[] NewHouse = GeneralHelper.TranslateBreps(HexagonBase, GetCenterPoint(cntx, cnty, BuildingHeight * cntz));
                                ToAdd.Content = NewHouse;
                                Result.Add(ToAdd);
                            }
                        }
                    }
                }
                return Result;
            }

            /// <summary>
            /// 簡単な図を取得します。
            /// </summary>
            /// <returns>図</returns>
            public PlanObject.Building GetPlanSimple()
            {
                PlanObject.Building Result = new PlanObject.Building("HoneycombBuilding");
                Curve HexagonBase = GetHexagon().ToNurbsCurve();
                for (int cntz = 0; cntz < Z; cntz++)
                {
                    PlanObject.Floor FloorTmp = new PlanObject.Floor("F" + cntz);
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntx = 0; cntx < X; cntx++)
                        {
                            int tmpnum = GetValue(cntx, cnty, cntz);
                            if (tmpnum > 0)
                            {
                                PlanObject.Member item = new PlanObject.Member("Type" + tmpnum);
                                Curve cv = (Curve)HexagonBase.Duplicate();
                                cv.Translate((Vector3d)GetCenterPoint(cntx, cnty, 0));
                                item.Content.Add(cv);
                                FloorTmp.Content.Add(item);
                            }
                        }
                    }
                    Result.Content.Add(FloorTmp);
                }
                return Result;
            }

            /// <summary>
            /// 特定の値を数えます。
            /// </summary>
            /// <param name="target">値</param>
            /// <returns>値を持つセルの数</returns>
            public int CountCellType(int target)
            {
                int Result = 0;
                for (int cntz = 0; cntz < Z; cntz++)
                {
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntx = 0; cntx < X; cntx++)
                        {
                            if (GetValue(cntx, cnty, cntz) == target) Result++;
                        }
                    }
                }
                return Result;

            }

            /// <summary>
            /// 統計情報を文字を示します。
            /// </summary>
            /// <returns>統計情報</returns>
            public string GetStatistics()
            {
                String Result = "";
                for (int cntz = 0; cntz < Z; cntz++)
                {
                    int[] FloorResult = new int[5];
                    for (int i = 0; i < FloorResult.GetLength(0); i++)
                    {
                        FloorResult[i] = 0;
                    }
                    for (int cnty = 0; cnty < Y; cnty++)
                    {
                        for (int cntx = 0; cntx < X; cntx++)
                        {
                            FloorResult[GetValue(cntx, cnty, cntz)]++;
                        }
                    }
                    for (int i = 0; i < FloorResult.GetLength(0); i++)
                    {
                        Result += FloorResult[i] + ",";
                    }
                    Result += "\n\r";
                }
                return Result;
            }

            /// <summary>
            /// 指定したグリッドの中心が位置する座標を取得します。
            /// </summary>
            /// <param name="x">グリッドのX方向の番号</param>
            /// <param name="y">グリッドのY方向の番号</param>
            /// <returns>座標</returns>
            public static Point2d GetCenterPoint(int x, int y)
            {
                return new Point2d(Math.Cos(Math.PI / 6.0) * (x * 2 + (y % 2)), (1 + Math.Cos(Math.PI / 3.0)) * y);
            }

            /// <summary>
            /// 指定したグリッドの中心が位置する座標に高さを含めて取得します。
            /// </summary>
            /// <param name="x">グリッドのX方向の番号</param>
            /// <param name="y">グリッドのY方向の番号</param>
            /// <param name="Height">高さ</param>
            /// <returns>座標</returns>
            public static Point3d GetCenterPoint(int x, int y, double Height)
            {
                return new Point3d(Math.Cos(Math.PI / 6.0) * (x * 2 + (y % 2)), (1 + Math.Cos(Math.PI / 3.0)) * y, Height);
            }

            /// <summary>
            /// グリッドの中心から見た頂点の座標を示します。
            /// </summary>
            /// <param name="cnt">頂点の番号</param>
            /// <returns>座標</returns>
            public static Vector2d GetVertex(int cnt)
            {
                return new Vector2d(Math.Sin(Math.PI / 3.0 * cnt), Math.Cos(Math.PI / 3.0 * cnt));
            }

            /// <summary>
            /// 六角形を得ます。
            /// </summary>
            /// <returns>六角形を示すポリライン</returns>
            public static Polyline GetHexagon()
            {
                Polyline result = new Polyline();
                for (int i = 0; i <= 6; i++)
                {
                    Vector2d vrx = GetVertex(i);
                    result.Add(vrx.X, vrx.Y, 0);
                }
                return result;
            }
        }

        /// <summary>
        /// セル周囲の状態を示します。
        /// </summary>
        public class NeighborStatus
        {
            /// <summary>
            /// セル近隣の状態
            /// </summary>
            public int[] Neighbor;
            /// <summary>
            /// セル自身の状態
            /// </summary>
            public int Self;
            /// <summary>
            /// 上層におけるセル近隣の状態
            /// </summary>
            public int[] UpperNeighbor;
            /// <summary>
            /// 上層にあるセルの状態
            /// </summary>
            public int Upper;
            /// <summary>
            /// 下層におけるセル近隣の状態
            /// </summary>
            public int[] LowerNeighbor;
            /// <summary>
            /// 下層にあるセルの状態
            /// </summary>
            public int Lower;

            /// <summary>
            /// 周囲のセルの中で該当する値がいくつあるかを計測します。
            /// </summary>
            /// <param name="target">値</param>
            /// <returns>個数</returns>
            public int CountNeighbor(int target)
            {
                int result = 0;
                for (int i = 0; i < Neighbor.GetLength(0); i++)
                {
                    if (Neighbor[i] == target)
                    {
                        result++;
                    }
                }
                return result;
            }

            /// <summary>
            /// 周囲のセルを数値による指定で取得します。-1～1までの値を指定してください。
            /// それ以外の場合には0とみなします。
            /// </summary>
            /// <param name="cnt">相対階層</param>
            /// <returns>周囲の値</returns>
            public int[] GetNeighborByFloor(int cnt)
            {
                if (cnt == -1) { return LowerNeighbor; }
                else if (cnt == 1) { return UpperNeighbor; }
                else { return Neighbor; }
            }

            /// <summary>
            /// 自分または上下のセルの値を取得します。
            /// -1～1までの値で指定してください。
            /// それ以外の場合には0とみなします。
            /// </summary>
            /// <param name="cnt">相対階層</param>
            /// <returns>値</returns>
            public int GetSelfByFloor(int cnt)
            {
                if (cnt == -1) { return Lower; }
                else if (cnt == 1) { return Upper; }
                else { return Self; }
            }

            /// <summary>
            /// 階層を指定し、周囲のセルを設定します。
            /// -1～1までの値で指定してください。
            /// それ以外の場合には0とみなします。
            /// </summary>
            /// <param name="cnt">相対階層</param>
            /// <param name="value">周囲の値</param>
            public void SetNeighborByFloor(int cnt, int[] value)
            {
                if (cnt == -1) { LowerNeighbor = value; }
                else if (cnt == 1) { UpperNeighbor = value; }
                else { Neighbor = value; }
            }

            /// <summary>
            /// 自分または上下のセルの値を設定します。
            /// -1～1までの値で指定してください。
            /// それ以外の場合には0とみなします。
            /// </summary>
            /// <param name="cnt">相対階層</param>
            /// <param name="value">値</param>
            public void SetSelfByFloor(int cnt, int value)
            {
                if (cnt == -1) { Lower = value; }
                else if (cnt == 1) { Upper = value; }
                else { Self = value; }
            }

            /// <summary>
            ///  二つの階層の値を入れ替えます。
            /// -1～1までの値で指定してください。
            /// それ以外の場合には0とみなします。
            /// </summary>
            /// <param name="A">相対階層A</param>
            /// <param name="B">相対階層B</param>
            public void SwapFloor(int A, int B)
            {
                int[] ANb = GetNeighborByFloor(A);
                int ASf = GetSelfByFloor(A);
                SetNeighborByFloor(A, GetNeighborByFloor(B));
                SetSelfByFloor(A, GetSelfByFloor(B));
                SetNeighborByFloor(B, ANb);
                SetSelfByFloor(B, ASf);
            }

            /// <summary>
            /// 複製します
            /// </summary>
            /// <returns>複製結果</returns>
            public NeighborStatus Duplicate()
            {
                NeighborStatus Result = new NeighborStatus();
                Result.Neighbor = this.Neighbor;
                Result.Self = this.Self;
                Result.UpperNeighbor = this.UpperNeighbor;
                Result.Upper = this.Upper;
                Result.LowerNeighbor = this.LowerNeighbor;
                Result.Lower = this.Lower;

                return Result;
            }
        }

        /// <summary>
        /// ルールを示します。
        /// </summary>
        public interface IRule
        {
            /// <summary>
            /// ルールを設定するパラメーター
            /// </summary>
            int[] Paramater { get; set; }
            /// <summary>
            /// 周囲の状態と座標に応じて、ルールに基づく値を取得します。
            /// </summary>
            /// <param name="neighbor">周囲の状態</param>
            /// <param name="x">X座標</param>
            /// <param name="y">Y座標</param>
            /// <param name="z">Z座標</param>
            /// <returns>値</returns>
            int GetStatus(NeighborStatus neighbor, int x, int y, int z);
        }

        /// <summary>
        /// 基本的なルールを含みます。
        /// </summary>
        public class Rules
        {
            /// <summary>
            /// 周囲のセルで指定した値が数え、指定範囲内にある場合に値を変化させます。
            /// 数える値は複数指定できます。
            /// </summary>
            public class Count : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int tmpnc = 0;
                    for (int i = 3; i < this.Paramater.GetLength(0); i++)
                    {
                        tmpnc += neighbor.CountNeighbor(this.Paramater[i]);
                    }
                    if (tmpnc >= Paramater[1] && tmpnc <= this.Paramater[2])
                    {
                        return Paramater[0];
                    }
                    else
                    {
                        return -1;
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="TargetNumber">数える値</param>
                /// <param name="Result">条件に合致した場合の値</param>
                /// <param name="Min">範囲の最小値</param>
                /// <param name="Max">範囲の最大値</param>
                public Count(int TargetNumber, int Result, int Min, int Max)
                {
                    Paramater = new int[4] { Result, Min, Max, TargetNumber };
                }
                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="TargetNumber">数える値</param>
                /// <param name="Result">条件に合致した場合の値</param>
                /// <param name="Min">範囲の最小値</param>
                /// <param name="Max">範囲の最大値</param>
                public Count(int[] TargetNumbers, int Result, int Min, int Max)
                {
                    List<int> Results = new List<int>() { Result, Min, Max };
                    foreach (int num in TargetNumbers)
                    {
                        Results.Add(num);
                    }
                    Paramater = Results.ToArray();

                }

            }

            /// <summary>
            /// 周囲のセルで指定した範囲の値を数え、指定範囲内にある場合に値を変化させます。
            /// 数える値は複数指定できます。
            /// </summary>
            public class CountRange : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int result = 0;
                    for (int i = 1; i < neighbor.Neighbor.GetLength(0); i++)
                    {
                        if (neighbor.Neighbor[i] >= Paramater[0] && neighbor.Neighbor[i] <= Paramater[0])
                        {
                            result++;
                        }
                    }
                    if (result >= Paramater[2] && result <= this.Paramater[3])
                    {
                        return Paramater[1];
                    }
                    else
                    {
                        return -1;
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="TargetNumberMin">数える値の最小値</param>
                /// <param name="TargetNumberMax">数える値の最大値</param>
                /// <param name="Result">結果</param>
                /// <param name="Min">指定した値の個数の最小値</param>
                /// <param name="Max">指定した値の個数の最大値</param>
                public CountRange(int TargetNumberMin, int TargetNumberMax, int Result, int Min, int Max)
                {
                    Paramater = new int[5] { TargetNumberMin, TargetNumberMax, Result, Min, Max };
                }

            }

            /// <summary>
            /// 周囲のセルの内、奇数番のみに対して指定した値を数えます。
            /// 数えた結果が指定した範囲の場合値を変化させます。
            /// </summary>
            public class CountOdd : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int tmpnc = 0;

                    for (int i = 3; i < this.Paramater.GetLength(0); i++)
                    {
                        int result = 0;
                        for (int j = 1; j < neighbor.Neighbor.GetLength(0); j += 2)
                        {
                            if (neighbor.Neighbor[j] == this.Paramater[i])
                            {
                                result++;
                            }
                        }

                        tmpnc += result;
                    }
                    if (tmpnc >= Paramater[1] && tmpnc <= this.Paramater[2])
                    {
                        return Paramater[0];
                    }
                    else
                    {
                        return -1;
                    }
                }
                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="TargetNumber">数える番号</param>
                /// <param name="Result">出力値</param>
                /// <param name="Min">該当セルの最小個数</param>
                /// <param name="Max">該当セルの最大個数</param>
                public CountOdd(int TargetNumber, int Result, int Min, int Max)
                {
                    Paramater = new int[4] { Result, Min, Max, TargetNumber };
                }
                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="TargetNumbers">数える番号</param>
                /// <param name="Result">出力値</param>
                /// <param name="Min">該当セルの最小個数</param>
                /// <param name="Max">該当セルの最大個数</param>
                public CountOdd(int[] TargetNumbers, int Result, int Min, int Max)
                {
                    List<int> Results = new List<int>() { Result, Min, Max };
                    foreach (int num in TargetNumbers)
                    {
                        Results.Add(num);
                    }
                    Paramater = Results.ToArray();

                }
            }

            /// <summary>
            /// 周囲のセルの内、偶数番のみに対して指定した値を数えます。
            /// 数えた結果が指定した範囲の場合値を変化させます。
            /// </summary>
            public class CountEven : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int tmpnc = 0;

                    for (int i = 3; i < this.Paramater.GetLength(0); i++)
                    {
                        int result = 0;
                        for (int j = 0; j < neighbor.Neighbor.GetLength(0); j += 2)
                        {
                            if (neighbor.Neighbor[j] == this.Paramater[i])
                            {
                                result++;
                            }
                        }

                        tmpnc += result;
                    }
                    if (tmpnc >= Paramater[1] && tmpnc <= this.Paramater[2])
                    {
                        return Paramater[0];
                    }
                    else
                    {
                        return -1;
                    }
                }
                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="TargetNumber">数える番号</param>
                /// <param name="Result">出力値</param>
                /// <param name="Min">該当セルの最小個数</param>
                /// <param name="Max">該当セルの最大個数</param>
                public CountEven(int TargetNumber, int Result, int Min, int Max)
                {
                    Paramater = new int[4] { Result, Min, Max, TargetNumber };
                }
                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="TargetNumbers">数える番号</param>
                /// <param name="Result">出力値</param>
                /// <param name="Min">該当セルの最小個数</param>
                /// <param name="Max">該当セルの最大個数</param>
                public CountEven(int[] TargetNumbers, int Result, int Min, int Max)
                {
                    List<int> Results = new List<int>() { Result, Min, Max };
                    foreach (int num in TargetNumbers)
                    {
                        Results.Add(num);
                    }
                    Paramater = Results.ToArray();

                }

            }

            /// <summary>
            /// 他のルールを適用し、その最大値を採用します。
            /// </summary>
            public class Max : IRule
            {
                public int[] Paramater { get; set; }
                public IRule[] Rules;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int lastResult = -1;
                    foreach (IRule ir in Rules)
                    {
                        lastResult = Math.Max(lastResult, ir.GetStatus(neighbor, x, y, z));

                    }
                    return lastResult;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="rules">ルール</param>
                public Max(params IRule[] rules)
                {
                    this.Rules = rules;
                }
            }

            /// <summary>
            /// 他のルールを適用し、その最小値を採用します。
            /// </summary>
            public class Min : IRule
            {
                public int[] Paramater { get; set; }
                public IRule[] Rules;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int lastResult = int.MaxValue;
                    foreach (IRule ir in Rules)
                    {
                        lastResult = Math.Min(lastResult, ir.GetStatus(neighbor, x, y, z));

                    }
                    return lastResult;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="rules">他のルール</param>
                public Min(params IRule[] rules)
                {
                    this.Rules = rules;
                }
            }

            /// <summary>
            /// 他のルールを適用し、その合計を採用します。
            /// </summary>
            public class Add : IRule
            {
                public int[] Paramater { get; set; }
                public IRule[] Rules;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int lastResult = 0;
                    foreach (IRule ir in Rules)
                    {
                        lastResult = (lastResult + ir.GetStatus(neighbor, x, y, z));

                    }
                    return lastResult;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="rules">ルール</param>
                public Add(params IRule[] rules)
                {
                    this.Rules = rules;
                }
            }

            /// <summary>
            /// 一定範囲の値を置き換えます。
            /// </summary>
            public class ReplaceRange : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    if (neighbor.Self >= Paramater[0] && neighbor.Self <= Paramater[1])
                    {
                        return Paramater[2];
                    }
                    else
                    {
                        return -1;
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="Min">対象となる最小値</param>
                /// <param name="Max">対象となる最大値</param>
                /// <param name="Result">変化後の値</param>
                public ReplaceRange(int Min, int Max, int Result)
                {
                    Paramater = new int[] { Min, Max, Result };
                }
            }

            /// <summary>
            /// 他のルールを適用し、全てが変化するなら、指定された最後のルールを適用します。
            /// </summary>
            public class And : IRule
            {
                public int[] Paramater { get; set; }
                public IRule[] Rules;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int lastResult = -1;
                    foreach (IRule ir in Rules)
                    {
                        if ((lastResult = ir.GetStatus(neighbor, x, y, z)) == -1)
                        {
                            return -1;
                        }
                    }
                    return lastResult;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="rules">ルール</param>
                public And(params IRule[] rules)
                {
                    this.Rules = rules;
                }

            }

            /// <summary>
            /// 他のルールを適用し、一つでも変化するなら、指定された最後に変化するルールを適用します。
            /// </summary>
            public class Or : IRule
            {
                public int[] Paramater { get; set; }
                public IRule[] Rules;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int tmpResult = -1;
                    int Result = -1;
                    foreach (IRule ir in Rules)
                    {
                        tmpResult = ir.GetStatus(neighbor, x, y, z);
                        Result = tmpResult == -1 ? Result : tmpResult;
                    }
                    return Result;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="rules">ルール</param>
                public Or(params IRule[] rules)
                {
                    this.Rules = rules;
                }

            }

            /// <summary>
            /// 他のルールの適用結果、または元の値に対して二つの値を交換します。
            /// </summary>
            public class Swap : IRule
            {
                public int[] Paramater { get; set; }
                public IRule Rule;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    int Result = -1;
                    if (Rule == null)
                    {
                        Result = neighbor.Self;
                    }
                    else
                    {
                        Result = Rule.GetStatus(neighbor, x, y, z);
                    }

                    if (Result == Paramater[0])
                    {
                        return Paramater[1];
                    }
                    else if (Result == Paramater[1])
                    {
                        return Paramater[0];
                    }

                    return Result;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="A">交換する値1</param>
                /// <param name="B">交換する値2</param>
                /// <param name="rule">ルール</param>
                public Swap(int A, int B, IRule rule)
                {
                    this.Paramater = new int[] { A, B };
                    this.Rule = rule;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="A">交換する値1</param>
                /// <param name="B">交換する値2</param>
                public Swap(int A, int B)
                {
                    this.Paramater = new int[] { A, B };
                    this.Rule = null;
                }
            }

            /// <summary>
            /// 下層の値をそのまま用います。
            /// </summary>
            public class CopyLowerFloor : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    return neighbor.Lower == -2 ? -1 : neighbor.Lower;
                }

                public CopyLowerFloor()
                {
                }
            }

            /// <summary>
            /// 上層の値をそのまま用います。
            /// </summary>
            public class CopyUpperFloor : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    return neighbor.Upper == -2 ? -1 : neighbor.Upper;
                }

                public CopyUpperFloor()
                {
                }

            }

            /// <summary>
            ///平面上の座標が指定値の場合に値を変化させます。
            /// </summary>
            public class BuildCylinder : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    if (x == Paramater[1] && y == Paramater[2] && z < Paramater[3])
                    {
                        return Paramater[0];
                    }
                    else
                    {
                        return -1;
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="result">変化後の値</param>
                /// <param name="x">x座標</param>
                /// <param name="y">y座標</param>
                /// <param name="height">高さ</param>
                public BuildCylinder(int result, int x, int y, int height)
                {
                    Paramater = new int[] { result, x, y, height };
                }

            }

            /// <summary>
            /// 該当セルが円柱に含まれる場合に値を変化させます。
            /// </summary>
            public class BuildCylinderRadius : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    if (Math.Pow(x - Paramater[1], 2) + Math.Pow(y - Paramater[2], 2) <= Math.Pow(Paramater[4], 2) && z < Paramater[3])
                    {
                        return Paramater[0];
                    }
                    else
                    {
                        return -1;
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="result">結果</param>
                /// <param name="x">中心のX座標</param>
                /// <param name="y">中心のY座標</param>
                /// <param name="height">高さ</param>
                /// <param name="Radius">半径</param>
                public BuildCylinderRadius(int result, int x, int y, int height, int Radius)
                {
                    Paramater = new int[] { result, x, y, height, Radius };
                }
            }

            /// <summary>
            /// ハニカムグリッドにおいて、該当セルが円柱に含まれる場合に値を変化させます。
            /// </summary>
            public class BuildCylinderRadiusHoneycomb : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    Point2d current = Honeycomb.GetCenterPoint(x, y);
                    Point2d target = Honeycomb.GetCenterPoint(Paramater[1], Paramater[2]);

                    if (current.DistanceTo(target) <= Paramater[4] && z < Paramater[3])
                    {
                        return Paramater[0];
                    }
                    else
                    {
                        return -1;
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="result">結果</param>
                /// <param name="x">X座標</param>
                /// <param name="y">Y座標</param>
                /// <param name="height">高さ</param>
                /// <param name="Radius">半径</param>
                public BuildCylinderRadiusHoneycomb(int result, int x, int y, int height, int Radius)
                {
                    Paramater = new int[] { result, x, y, height, Radius };
                }

            }

            /// <summary>
            /// 該当セルが直方体に含まれる場合に値を変化させます。
            /// </summary>
            public class BuildBox : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    if (x >= Paramater[1] && y >= Paramater[2] && z >= Paramater[3] &&
                        x < Paramater[4] && y < Paramater[5] && z < Paramater[6])
                    {
                        return Paramater[0];
                    }
                    else
                    {
                        return -1;
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="result">結果</param>
                /// <param name="x">X座標</param>
                /// <param name="y">Y座標</param>
                /// <param name="z">Z座標</param>
                /// <param name="xLength">幅(X方向)</param>
                /// <param name="yLength">奥行(Y方向)</param>
                /// <param name="zLength">高さ(Z方向)</param>
                public BuildBox(int result, int x, int y, int z, int xLength, int yLength, int zLength)
                {
                    Paramater = new int[] { result, x, y, z, x + xLength, y + yLength, z + zLength };
                }

            }

            /// <summary>
            /// 条件によらず指定した値にします。
            /// </summary>
            public class Init : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    return Paramater[0];
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="result">値</param>
                public Init(int result)
                {
                    Paramater = new int[] { result };
                }

            }

            /// <summary>
            /// 条件によらず指定した値にします。(Initと同じ)
            /// </summary>
            public class Const : Init
            {
                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="result">値</param>
                public Const(int Result) : base(Result) { }
            }

            /// <summary>
            /// セルが指定値の場合、指定値の最初の値を返します。
            /// </summary>
            public class Self : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    foreach (int num in this.Paramater)
                    {
                        if (neighbor.Self == num) { return Paramater[0]; }
                    }
                    return -1;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="result">指定値</param>
                public Self(params int[] result)
                {
                    Paramater = result;
                }

            }

            /// <summary>
            /// 値を置き換えます。
            /// </summary>
            public class Replace : IRule
            {
                public int[] Paramater { get; set; }

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    if (neighbor.Self == Paramater[0]) { return Paramater[1]; } else { return -1; }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="origin">置き換え元</param>
                /// <param name="result">置き換え先</param>
                public Replace(int origin, int result)
                {
                    Paramater = new int[] { origin, result };
                }

            }

            /// <summary>
            /// 他のルールの適用結果、または元の値が指定値の場合は-1(変化しない)を返し、そうでない場合は結果をそのまま返します。
            /// </summary>
            public class Keep : IRule
            {
                public int[] Paramater { get; set; }
                public IRule Content;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    foreach (int num in Paramater)
                    {
                        if (neighbor.Self == num)
                        {
                            return -1;
                        }
                    }

                    {
                        if (Content != null)
                        {
                            return Content.GetStatus(neighbor, x, y, z);
                        }
                        else
                        {
                            return neighbor.Self;
                        }
                    }
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="target">変化させない値</param>
                public Keep(params int[] target)
                {
                    Paramater = target;
                    this.Content = null;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="target">変化させない値</param>
                /// <param name="Content">ルール</param>
                public Keep(int target, IRule Content)
                {
                    Paramater = new[] { target };
                    this.Content = Content;
                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="target">変化させない値</param>
                /// <param name="Content">ルール</param>
                public Keep(int[] target, IRule Content)
                {
                    Paramater = target;
                    this.Content = Content;
                }
            }

            /// <summary>
            /// 周囲のセルにおける二つの階層を入れ替えた物としてルールを適用します。
            /// </summary>
            public class SwapFloor : IRule
            {
                public int[] Paramater { get; set; }
                public IRule Content;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    NeighborStatus newNeighbor = neighbor.Duplicate();
                    newNeighbor.SwapFloor(Paramater[0], Paramater[1]);
                    return Content.GetStatus(newNeighbor, x, y, z);
                }
                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="A">相対階層1</param>
                /// <param name="B">相対階層2</param>
                /// <param name="Content">適用ルール</param>
                public SwapFloor(int A, int B, IRule Content)
                {
                    this.Content = Content;
                    Paramater = new[] { A, B };
                }

                /// <summary>
                /// コンストラクタ。交換する階層の片方は自分の階層とします。
                /// </summary>
                /// <param name="A">相対階層1</param>
                /// <param name="Content">適用ルール</param>
                public SwapFloor(int A, IRule Content) : this(A, 0, Content)
                {
                }
            }

            /// <summary>
            /// 指定した階層の場合のみルールを適用します。
            /// </summary>
            public class TargetFloor : IRule
            {
                public int[] Paramater { get; set; }
                public IRule Content;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    if (z == Paramater[0])
                    {
                        return Content.GetStatus(neighbor, x, y, z);
                    }
                    else
                    {
                        return -1;
                    }

                }

                /// <summary>
                /// コンストラクタ
                /// </summary>
                /// <param name="target">指定階層</param>
                /// <param name="Content">適用ルール</param>
                public TargetFloor(int target, IRule Content)
                {
                    this.Content = Content;
                    Paramater = new[] { target };
                }
            }

            /// <summary>
            /// 一定確率でルールを適用します。
            /// </summary>
            public class Random : IRule
            {
                public int[] Paramater { get; set; }
                public IRule Content;
                public System.Random Rand;

                public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                {
                    if (Rand.Next(100) < Paramater[0])
                    {
                        return Content.GetStatus(neighbor, x, y, z);
                    }
                    else
                    {
                        return -1;
                    }

                }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="Probability">変化確率(百分率)</param>
                /// <param name="rule">適用ルール</param>
                public Random(int Probability, IRule rule) : this(Probability, rule, new System.Random()) { }

                /// <summary>
                /// コンストラクタ。
                /// </summary>
                /// <param name="Probability">変化確率(百分率)</param>
                /// <param name="rule">適用ルール</param>
                /// <param name="rd">利用するRandomインスタンス</param>
                public Random(int Probability, IRule rule, System.Random rd)
                {
                    Paramater = new int[] { Probability };
                    Content = rule;
                    Rand = new System.Random(rd.Next());
                }

            }
        }
    }
}