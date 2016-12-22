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
    /// LSystemに関係する機能を含みます。
    /// </summary>
    public class LSystem
    {
        /// <summary>
        /// 毎ステップ適用するルール。
        /// </summary>
        public Rule[] Rules;
        /// <summary>
        /// 現在のツリー
        /// </summary>
        public Sequence Tree;
        /// <summary>
        /// 初期の状態
        /// </summary>
        public Sequence InitialState;
        /// <summary>
        /// 現在の世代
        /// </summary>
        public int Generation { get; private set; }
        /// <summary>
        /// ツリーを構成する要素
        /// </summary>
        public Dictionary<string, BodyType> BodyTypes { get; private set; }

        /// <summary>
        /// ルールを指定回数適用する。
        /// </summary>
        /// <param name="cnt">適用回数</param>
        public void ApplyRules(int cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                ApplyRules();
            }
        }

        /// <summary>
        /// ルールを一度適用する。
        /// </summary>
        public void ApplyRules()
        {
            Generation++;
            foreach (Rule rl in Rules)
            {
                Tree = Tree.ApplyRule(rl, Generation);
            }
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="Initial">初期の状態</param>
        /// <param name="Rules">ルール</param>
        public LSystem(Sequence Initial, Rule[] Rules)
        {
            InitialState = Initial.Duplicate();
            Tree = Initial.Duplicate();
            this.Rules = Rules;
            Generation = 0;
            BodyTypes = new Dictionary<string, BodyType>();
        }

        /// <summary>
        /// ツリー構成要素を追加する
        /// </summary>
        /// <param name="bts">追加要素</param>
        public void RegisterBodyType(params BodyType[] bts)
        {
            foreach (BodyType bt in bts)
            {
                BodyTypes.Add(bt.Name, bt);
            }
        }

        /// <summary>
        /// ツリーを構成する要素
        /// </summary>
        public class BodyType
        {
            /// <summary>
            /// 名前
            /// </summary>
            public readonly string Name;
            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="name">名前</param>
            public BodyType(string name) { Name = name; }

            public override string ToString() { return Name; }
        }

        /// <summary>
        /// 要素の連続を表します。
        /// </summary>
        public class Sequence
        {
            /// <summary>
            /// 含まれる要素。
            /// </summary>
            public List<Body> Content = new List<Body>();

            /// <summary>
            /// コンストラクタ
            /// </summary>
            public Sequence()
            {
            }

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="BodyTypes">要素</param>
            public Sequence(params BodyType[] BodyTypes)
            {
                for (int i = 0; i < BodyTypes.Count(); i++)
                {
                    Content.Add(new Body(BodyTypes[i]));
                }
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <param name="Gen">設定世代</param>
            /// <returns>結果</returns>
            public Sequence Duplicate(int Gen)
            {
                List<Body> Result = new List<Body>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.Add(Content[i].Duplicate(Gen));
                }
                return new Sequence() { Content = Result };
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <returns>複製結果</returns>
            public Sequence Duplicate()
            {
                List<Body> Result = new List<Body>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.Add(Content[i].Duplicate());
                }
                return new Sequence() { Content = Result };
            }

            /// <summary>
            /// ルールを適用します。
            /// </summary>
            /// <param name="rl">ルール</param>
            /// <param name="CurrentGeneration">現在の世代</param>
            /// <returns>結果</returns>
            public Sequence ApplyRule(Rule rl, int CurrentGeneration)
            {
                List<Body> Result = new List<Body>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    if (Content[i].BodyType == rl.Target)
                    {
                        Result.AddRange(rl.Result.Duplicate(CurrentGeneration).Content);
                    }
                    else
                    {
                        Result.Add(Content[i].ApplyRule(rl, CurrentGeneration));
                    }
                }
                return new Sequence() { Content = Result };
            }

            public override string ToString()
            {
                string result = "";
                for (int i = 0; i + 1 < Content.Count(); i++)
                {
                    result += Content[i].ToString();
                    result += ",";
                }
                result += Content[Content.Count() - 1].ToString();
                return result;
            }

        }

        /// <summary>
        /// 分岐を示します。
        /// </summary>
        public class Body
        {
            /// <summary>
            /// 世代
            /// </summary>
            public int Generation;
            /// <summary>
            /// 子要素
            /// </summary>
            public Sequence[] Child = new Sequence[0];
            /// <summary>
            /// 要素の種類
            /// </summary>
            public BodyType BodyType;

            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="type">要素の種類</param>
            public Body(BodyType type) { this.BodyType = type; }

            /// <summary>
            /// 複製する
            /// </summary>
            /// <returns>複製結果</returns>
            public Body Duplicate()
            {
                return Duplicate(this.Generation);
            }

            /// <summary>
            /// 複製します。
            /// </summary>
            /// <param name="Gen">設定世代</param>
            /// <returns>結果</returns>
            public Body Duplicate(int Gen)
            {
                Sequence[] NewChild = new Sequence[Child.GetLength(0)];
                for (int i = 0; i < Child.GetLength(0); i++)
                {
                    NewChild[i] = Child[i].Duplicate(Gen);
                }
                return new Body(this.BodyType) { Child = NewChild, Generation = Gen };
            }

            /// <summary>
            /// ルールを適用します。
            /// </summary>
            /// <param name="rl">ルール</param>
            /// <param name="CurrentGeneration">現在の世代</param>
            /// <returns>結果</returns>
            public Body ApplyRule(Rule rl, int CurrentGeneration)
            {
                Sequence[] NewChild = new Sequence[Child.GetLength(0)];
                for (int i = 0; i < Child.GetLength(0); i++)
                {
                    NewChild[i] = Child[i].ApplyRule(rl, CurrentGeneration);
                }
                return new Body(this.BodyType) { Child = NewChild, Generation = this.Generation };
            }

            public override string ToString()
            {
                string result = "";
                result += BodyType.ToString();
                for (int i = 0; i < Child.GetLength(0); i++)
                {
                    result += "[";
                    result += Child[i].ToString();
                    result += "]";
                }
                result += "" + this.Generation;
                return result;
            }

        }

        /// <summary>
        /// ルールを示します。
        /// </summary>
        public interface Rule
        {
            /// <summary>
            /// 変換元
            /// </summary>
            BodyType Target { get; set; }
            /// <summary>
            /// 変換結果
            /// </summary>
            Sequence Result { get; }
        }

        /// <summary>
        /// 単純な置き換えルールを示します。
        /// </summary>
        public class RuleSimple : Rule
        {
            public BodyType Target { get; set; }
            public Sequence Result { get; set; }
        }

        /// <summary>
        /// 確率的に変化するルールを示します。
        /// </summary>
        public class RuleProbability : Rule
        {
            public BodyType Target { get; set; }
            public Sequence Result { get { return GetResult(); } }

            private Dictionary<Sequence, double> Rules;
            private Random rand;

            public RuleProbability()
                : this(new Random())
            {
            }

            public RuleProbability(Random rd)
            {
                rand = rd;
                Rules = new Dictionary<Sequence, double>();
            }
            /// <summary>
            /// 変化可能な連続要素を追加します。
            /// </summary>
            /// <param name="Content">追加要素</param>
            /// <param name="Rate">変化確率</param>
            public void AddSequence(Sequence Content, double Rate)
            {
                Rules.Add(Content.Duplicate(), Rate);
            }

            /// <summary>
            /// 結果を取得します。
            /// </summary>
            /// <returns>結果</returns>
            private Sequence GetResult()
            {
                double randResult = rand.NextDouble();
                double PosSum = 0.0;

                foreach (var kvp in Rules)
                {
                    if (PosSum <= randResult && randResult < PosSum + kvp.Value)
                    {
                        return kvp.Key;
                    }
                    PosSum += kvp.Value;
                }
                return new Sequence() { Content = new List<Body>() { new Body(Target) } };
            }
        }
    }
}