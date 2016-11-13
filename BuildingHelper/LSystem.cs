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
    public class LSystem
    {
        public Rule[] Rules;
        public Sequence Tree;
        public Sequence InitialState;
        public int Generation { get; private set; }
        public Dictionary<string, BodyType> BodyTypes { get; private set; }

        public void ApplyRules(int cnt)
        {
            for (int i = 0; i < cnt; i++)
            {
                ApplyRules();
            }
        }

        public void ApplyRules()
        {
            Generation++;
            foreach (Rule rl in Rules)
            {
                Tree = Tree.ApplyRule(rl, Generation);
            }
        }

        public LSystem(Sequence Initial, Rule[] Rules)
        {
            InitialState = Initial.Duplicate();
            Tree = Initial.Duplicate();
            this.Rules = Rules;
            Generation = 0;
            BodyTypes = new Dictionary<string, BodyType>();
        }

        public void RegisterBodyType(params BodyType[] bts)
        {
            foreach (BodyType bt in bts)
            {
                BodyTypes.Add(bt.Name, bt);
            }
        }

        public class BodyType
        {
            public readonly string Name;
            public BodyType(string name) { Name = name; }
            public override string ToString() { return Name; }
        }

        public class Sequence
        {
            public List<Body> Content = new List<Body>();

            public Sequence()
            {
            }

            public Sequence(params BodyType[] BodyTypes)
            {
                for (int i = 0; i < BodyTypes.Count(); i++)
                {
                    Content.Add(new Body(BodyTypes[i]));
                }
            }

            public Sequence Duplicate(int Gen)
            {
                List<Body> Result = new List<Body>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.Add(Content[i].Duplicate(Gen));
                }
                return new Sequence() { Content = Result };
            }

            public Sequence Duplicate()
            {
                List<Body> Result = new List<Body>();
                for (int i = 0; i < Content.Count(); i++)
                {
                    Result.Add(Content[i].Duplicate());
                }
                return new Sequence() { Content = Result };
            }

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

        public class Body
        {
            public int Generation;
            public Sequence[] Child = new Sequence[0];
            public BodyType BodyType;

            public Body(BodyType type) { this.BodyType = type; }

            public Body Duplicate()
            {
                return Duplicate(this.Generation);
            }


            public Body Duplicate(int Gen)
            {
                Sequence[] NewChild = new Sequence[Child.GetLength(0)];
                for (int i = 0; i < Child.GetLength(0); i++)
                {
                    NewChild[i] = Child[i].Duplicate(Gen);
                }
                return new Body(this.BodyType) { Child = NewChild, Generation = Gen };
            }

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

        public interface Rule
        {
            BodyType Target { get; set; }
            Sequence Result { get; }
        }

        public class RuleSimple : Rule
        {
            public BodyType Target { get; set; }
            public Sequence Result { get; set; }
        }

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
            public void AddSequence(Sequence Content, double Rate)
            {
                Rules.Add(Content.Duplicate(), Rate);
            }

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