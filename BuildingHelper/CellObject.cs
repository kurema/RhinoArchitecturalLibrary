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
        public class CellObject
        {
            //Magic number
            // 0 : No object.
            // -1: No result. This should not be used in IBoxel.
            // -2: Out of range.
            public interface IBoxel
            {
                void Init(int x,int y,int z);
                NeighborStatus GetNeighbor(int x, int y,int z);
                void Apply(IRule rule);
                IBoxel Duplicate();
            }

            public class Boxel : IBoxel
            {
                private int[,,] Content;
                private int X { get { return Content.GetLength(0)-2; } }
                private int Y { get { return Content.GetLength(1)-2; } }
                private int Z { get { return Content.GetLength(2)-2; } }

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

                public NeighborStatus GetNeighbor(int x, int y,int z)
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

                public int GetValue(int x, int y, int z)
                {
                    return Content[x + 1, y + 1, z + 1];
                }

                public void SetValue(int x, int y, int z, int Value)
                {
                    if (Value != -1)
                    {
                        Content[x + 1, y + 1, z + 1] = Value;
                    }
                }

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

                public Boxel(int x, int y, int z)
                {
                    this.Init(x, y, z);
                }

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



            public class Honeycomb : IBoxel
            {
                private int[, ,] Content;
                public int X { get { return Content.GetLength(0)-2; } }
                public int Y { get { return Content.GetLength(1) - 2; } }
                public int Z { get { return Content.GetLength(2) - 2; } }

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

                public int GetValue(int x, int y, int z)
                {
                    return Content[x + 1, y + 1, z + 1];
                }

                public void SetValue(int x, int y, int z, int Value)
                {
                    if (Value != -1)
                    {
                        Content[x + 1, y + 1, z + 1] = Value;
                    }
                }

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

                public Honeycomb(int x, int y, int z)
                {
                    this.Init(x, y, z);
                }

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

                public RealObject.Building GetBuildingSimple(double BuildingHeight=1.0)
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
                                    RealObject.Member ToAdd=new RealObject.Member("Type"+tmpnum);
                                    Brep[] NewHouse = GeneralHelper.TranslateBreps(HexagonBase, GetCenterPoint(cntx, cnty, BuildingHeight * cntz));
                                    ToAdd.Content = NewHouse;
                                    Result.Add(ToAdd);
                                }
                            }
                        }
                    }
                    return Result;
                }

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

                public static Point2d GetCenterPoint(int x, int y)
                {
                    return new Point2d(Math.Cos(Math.PI / 6.0) * (x * 2 + (y % 2)), (1 + Math.Cos(Math.PI / 3.0)) * y);
                }

                public static Point3d GetCenterPoint(int x, int y,double Height)
                {
                    return new Point3d(Math.Cos(Math.PI / 6.0) * (x * 2 + (y % 2)), (1 + Math.Cos(Math.PI / 3.0)) * y, Height);
                }

                public static Vector2d GetVertex(int cnt)
                {
                    return new Vector2d(Math.Sin(Math.PI / 3.0 * cnt), Math.Cos(Math.PI / 3.0 * cnt));
                }

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


            public class NeighborStatus
            {
                public int[] Neighbor;
                public int Self;
                public int[] UpperNeighbor;
                public int Upper;
                public int[] LowerNeighbor;
                public int Lower;

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

                public int[] GetNeighborByFloor(int cnt)
                {
                    if (cnt == -1) { return LowerNeighbor; }
                    else if (cnt == 1) { return UpperNeighbor; }
                    else { return Neighbor; }
                }

                public int GetSelfByFloor(int cnt)
                {
                    if (cnt == -1) { return Lower; }
                    else if (cnt == 1) { return Upper; }
                    else { return Self; }
                }

                public void SetNeighborByFloor(int cnt,int [] value){
                    if (cnt == -1) { LowerNeighbor=value; }
                    else if (cnt == 1) { UpperNeighbor=value; }
                    else { Neighbor=value; }
                }

                public void SetSelfByFloor(int cnt,int value)
                {
                    if (cnt == -1) { Lower=value; }
                    else if (cnt == 1) { Upper=value; }
                    else {  Self=value; }
                }

                public void SwapFloor(int A, int B)
                {
                    int[] ANb = GetNeighborByFloor(A);
                    int ASf = GetSelfByFloor(A);
                    SetNeighborByFloor(A, GetNeighborByFloor(B));
                    SetSelfByFloor(A, GetSelfByFloor(B));
                    SetNeighborByFloor(B,ANb);
                    SetSelfByFloor(B, ASf);
                }

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

            public interface IRule
            {
                int[] Paramater { get; set; }
                int GetStatus(NeighborStatus neighbor,int x,int y,int z);
            }

            public class Rules
            {
                public class Count:IRule
                {
                    public int[] Paramater { get; set; }

                    public int GetStatus(NeighborStatus neighbor, int x, int y,int z)
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
                    
                    public Count(int TargetNumber, int Result, int Min, int Max)
                    {
                        Paramater = new int[4] { Result, Min, Max, TargetNumber };
                    }
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

                public class CountRange : IRule
                {
                    public int[] Paramater { get; set; }

                    public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                    {
                        int result = 0;
                        for (int i = 1; i < neighbor.Neighbor.GetLength(0); i ++)
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

                    public CountRange(int TargetNumberMin, int TargetNumberMax, int Result, int Min, int Max)
                    {
                        Paramater = new int[5] { TargetNumberMin, TargetNumberMax, Result, Min, Max };
                    }

                }


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

                    public CountOdd(int TargetNumber, int Result, int Min, int Max)
                    {
                        Paramater = new int[4] { Result, Min, Max, TargetNumber };
                    }
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


                public class CountEven : IRule
                {
                    public int[] Paramater { get; set; }

                    public int GetStatus(NeighborStatus neighbor, int x, int y,int z)
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
                    
                    public CountEven(int TargetNumber, int Result, int Min, int Max)
                    {
                        Paramater = new int[4] { Result, Min, Max, TargetNumber };
                    }
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

                    public Max(params IRule[] rules)
                    {
                        this.Rules = rules;
                    }
                }

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

                    public Min(params IRule[] rules)
                    {
                        this.Rules = rules;
                    }
                }

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

                    public Add(params IRule[] rules)
                    {
                        this.Rules = rules;
                    }
                }

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

                    public ReplaceRange(int Min, int Max, int Result)
                    {
                        Paramater = new int[] { Min, Max, Result };
                    }
                }

                public class And : IRule
                {
                    public int[] Paramater { get; set; }
                    public IRule[] Rules;

                    public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                    {
                        int lastResult = -1;
                        foreach (IRule ir in Rules)
                        {
                            if ((lastResult = ir.GetStatus(neighbor, x, y,z)) == -1)
                            {
                                return -1;
                            }
                        }
                        return lastResult;
                    }

                    public And(params IRule[] rules)
                    {
                        this.Rules = rules;
                    }

                }

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
                            tmpResult = ir.GetStatus(neighbor, x, y,z);
                            Result = tmpResult == -1 ? Result : tmpResult;
                        }
                        return Result;
                    }

                    public Or(params IRule[] rules)
                    {
                        this.Rules = rules;
                    }

                }

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

                    public Swap(int A,int B, IRule rule)
                    {
                        this.Paramater = new int[] { A, B };
                        this.Rule = rule;
                    }

                    public Swap(int A, int B)
                    {
                        this.Paramater = new int[] { A, B };
                        this.Rule = null;
                    }
                }

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

                    public BuildCylinder(int result, int x,int y,int height)
                    {
                        Paramater = new int[] { result,x, y, height };
                    }

                }

                public class BuildCylinderRadius : IRule
                {
                    public int[] Paramater { get; set; }

                    public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                    {
                        if (Math.Pow(x - Paramater[1],2)+ Math.Pow(y - Paramater[2],2)<=Math.Pow(Paramater[4],2) && z < Paramater[3])
                        {
                            return Paramater[0];
                        }
                        else
                        {
                            return -1;
                        }
                    }

                    public BuildCylinderRadius(int result, int x, int y, int height, int Radius)
                    {
                        Paramater = new int[] { result, x, y, height ,Radius};
                    }

                }

                public class BuildCylinderRadiusHoneycomb : IRule
                {
                    public int[] Paramater { get; set; }

                    public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                    {
                        Point2d current = Honeycomb.GetCenterPoint(x, y);
                        Point2d target = Honeycomb.GetCenterPoint(Paramater[1], Paramater[2]);

                        if (current.DistanceTo(target) <=Paramater[4] && z < Paramater[3])
                        {
                            return Paramater[0];
                        }
                        else
                        {
                            return -1;
                        }
                    }

                    public BuildCylinderRadiusHoneycomb(int result, int x, int y, int height, int Radius)
                    {
                        Paramater = new int[] { result, x, y, height, Radius };
                    }

                }

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

                    public BuildBox(int result, int x, int y, int z, int xLength,int yLength,int zLength)
                    {
                        Paramater = new int[] { result, x, y, z, x+ xLength, y+yLength, z+zLength };
                    }

                }

                public class Init : IRule
                {
                    public int[] Paramater { get; set; }

                    public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                    {
                        return Paramater[0];
                    }

                    public Init(int result)
                    {
                        Paramater = new int[] { result};
                    }

                }

                public class Const : Init
                {
                    public Const(int Result) : base(Result) { }
                }

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

                    public Self(params int[] result)
                    {
                        Paramater = result;
                    }

                }

                public class Replace : IRule
                {
                    public int[] Paramater { get; set; }

                    public int GetStatus(NeighborStatus neighbor, int x, int y, int z)
                    {
                        if (neighbor.Self == Paramater[0]) { return Paramater[1]; } else { return -1; }
                    }

                    public Replace(int origin,int result)
                    {
                        Paramater = new int[] { origin,result };
                    }

                }

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

                    public Keep(int target)
                    {
                        Paramater = new[] { target };
                        this.Content = null;
                    }

                    public Keep(int target,IRule Content)
                    {
                        Paramater = new[] { target };
                        this.Content = Content;
                    }

                    public Keep(int[] target, IRule Content)
                    {
                        Paramater = target;
                        this.Content = Content;
                    }

                }

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

                    public SwapFloor(int A,int B,IRule Content)
                    {
                        this.Content = Content;
                        Paramater = new[] { A,B };
                    }
                    public SwapFloor(int A, IRule Content):this(A,0,Content)
                    {
                    }
                }

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

                    public TargetFloor(int target, IRule Content)
                    {
                        this.Content = Content;
                        Paramater = new[] { target };
                    }
                }

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

                    public Random(int Probability, IRule rule) : this(Probability, rule, new System.Random()) { }

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
}