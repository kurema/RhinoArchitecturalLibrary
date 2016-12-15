using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace kurema.RhinoTools
{
    public class Expression
    {
        public interface ICommand
        {
            int ArgumentCount { get; }

            double Execute(params double[] arg);

            ICommand Duplicate();
        }

        public class CommandGeneral : ICommand
        {
            public Func<double[], double> Content { get; private set; }

            public CommandGeneral(Func<double[], double> arg, int argCount)
            {
                this.Content = arg;
                this.ArgumentCount = argCount;
            }

            public int ArgumentCount { get; private set; }

            public double Execute(params double[] arg)
            {
                return Content(arg);
            }

            public ICommand Duplicate()
            {
                return new CommandGeneral(Content, ArgumentCount);
            }
        }

        public class Commands
        {
            public class Mathematics
            {
                public class Add : ICommand
                {
                    public int ArgumentCount { get { return 2; } }

                    public ICommand Duplicate()
                    {
                        return new Add();
                    }

                    public double Execute(params double[] arg)
                    {
                        double result = 0;
                        foreach (var item in arg)
                        {
                            result += item;
                        }
                        return result;
                    }
                }

                public class Subtract : ICommand
                {
                    public int ArgumentCount { get { return 2; } }

                    public ICommand Duplicate()
                    {
                        return new Subtract();
                    }

                    public double Execute(params double[] arg)
                    {
                        return arg[0] - arg[1];

                    }
                }

                public class Multiply : ICommand
                {
                    public int ArgumentCount { get { return 2; } }

                    public ICommand Duplicate()
                    {
                        return new Multiply();
                    }

                    public double Execute(params double[] arg)
                    {
                        double result = 1;
                        foreach (var item in arg)
                        {
                            result *= item;
                        }
                        return result;
                    }
                }

                public class Divide : ICommand
                {
                    public int ArgumentCount { get { return 2; } }

                    public ICommand Duplicate()
                    {
                        return new Divide();
                    }

                    public double Execute(params double[] arg) { return arg[0] / arg[1]; }
                }

                public class Minus : ICommand
                {
                    public int ArgumentCount { get { return 1; } }

                    public ICommand Duplicate()
                    {
                        return new Minus();
                    }

                    public double Execute(params double[] arg) { return -arg[0]; }
                }
                public class Sin : ICommand
                {
                    public int ArgumentCount { get { return 1; } }

                    public ICommand Duplicate()
                    {
                        return new Sin();
                    }

                    public double Execute(params double[] arg) { return Math.Sin(arg[0]); }
                }

                public class Cos : ICommand
                {
                    public int ArgumentCount { get { return 1; } }

                    public ICommand Duplicate()
                    {
                        return new Cos();
                    }

                    public double Execute(params double[] arg) { return Math.Cos(arg[0]); }
                }

                public class Tan : ICommand
                {
                    public int ArgumentCount { get { return 1; } }

                    public ICommand Duplicate()
                    {
                        return new Tan();
                    }

                    public double Execute(params double[] arg) { return Math.Tan(arg[0]); }
                }

                public class Power : ICommand
                {
                    public int ArgumentCount { get { return 2; } }

                    public ICommand Duplicate()
                    {
                        return new Power();
                    }

                    public double Execute(params double[] arg) { return Math.Pow(arg[0], arg[1]); }
                }

            }

            public class Combined : ICommand
            {

                public ICommand MainCommand { get; private set; }
                public ICommand[] Arguments { get; private set; }

                public Combined(ICommand Main, params ICommand[] Args)
                {
                    if (Args.Count() != Main.ArgumentCount)
                    {
                        throw new Exception("Argument count does not match.");
                    }
                    this.MainCommand = Main;
                    this.Arguments = Args;
                }


                public int ArgumentCount
                {
                    get
                    {
                        int result = 0;
                        foreach (var item in Arguments)
                        {
                            result += item.ArgumentCount;
                        }
                        return result;
                    }
                }

                public double Execute(params double[] arg)
                {
                    int currentArgCount = 0;
                    double[] argumentsResult = new double[Arguments.Count()];
                    for (int i = 0; i < Arguments.Count(); i++)
                    {
                        int argCnt = Arguments[i].ArgumentCount;
                        double[] tempArg = new double[argCnt];
                        Array.Copy(arg, currentArgCount, tempArg, 0, argCnt);
                        argumentsResult[i] = Arguments[i].Execute(tempArg);

                        currentArgCount += argCnt;
                    }
                    return MainCommand.Execute(argumentsResult);
                }

                public ICommand Duplicate()
                {
                    var args = new ICommand[Arguments.Count()];
                    for (int i = 0; i < Arguments.Count(); i++)
                    {
                        args[i] = Arguments[i].Duplicate();
                    }
                    return new Combined(this.MainCommand.Duplicate(), args);
                }
            }

            public class Argument : ICommand
            {
                public int ArgumentCount
                {
                    get { return 1; }
                }

                public ICommand Duplicate()
                {
                    return new Argument();
                }

                public double Execute(params double[] arg)
                {
                    return arg[0];
                }
            }

            public class ImmediateValue : ICommand
            {
                public double Value { get; set; }

                public int ArgumentCount
                {
                    get { return 0; }
                }

                public ImmediateValue(double Value)
                {
                    this.Value = Value;
                }

                public double Execute(params double[] arg)
                {
                    return Value;
                }

                public ICommand Duplicate()
                {
                    return new ImmediateValue(this.Value);
                }
            }
        }

        public static class Operation
        {
            public static double ExecuteCommandWithUnmatchedArgs(ICommand cmd, params double[] args)
            {
                var cnt = cmd.ArgumentCount;
                if (cnt == args.Count()) { return cmd.Execute(args); }
                if (cnt < args.Count())
                {
                    double[] newArg = new double[cnt];
                    Array.Copy(args, newArg, cnt);
                    return cmd.Execute(newArg);
                }
                var targ = new double[cnt];
                for (int i = args.Count(); i < cnt; i++)
                {
                    targ[i] = args[i % args.Count()];
                }
                return cmd.Execute(targ);
            }


            public static String GetLispLikeText(ICommand command)
            {
                if (command is Commands.Combined)
                {
                    var cm = (Commands.Combined)command;
                    string result = "(" + cm.MainCommand.GetType().Name;
                    foreach (var item in cm.Arguments)
                    {
                        result += " " + GetLispLikeText(item);
                    }
                    return result + ")";
                }
                else if (command is Commands.ImmediateValue)
                {
                    var cm = (Commands.ImmediateValue)command;
                    return cm.Value.ToString();
                }
                else if (command is Commands.Argument)
                {
                    return "*";
                }
                else
                {
                    var temporary = "";
                    for (int i = 0; i < command.ArgumentCount; i++)
                    {
                        temporary += " *";
                    }
                    return "(" + command.GetType().Name + temporary.ToString() + ")";
                }
            }

            public static int CountContainingCommand(ICommand command, bool CountImmediateValue = true, bool CountArgument = true)
            {
                if (command is Commands.Combined)
                {
                    var commandComb = (Commands.Combined)command;
                    int result = 1;
                    for (int i = 0; i < commandComb.Arguments.Count(); i++)
                    {
                        result += CountContainingCommand(commandComb.Arguments[i]);
                    }
                    return result;
                }
                else if (command is Commands.ImmediateValue)
                {
                    return CountImmediateValue ? 1 : 0;
                }
                else if (command is Commands.Argument)
                {
                    return CountArgument ? 1 : 0;
                }
                else
                {
                    return 1;
                }
            }

            public static int CountContainingArgument(ICommand command)
            {
                if (command is Commands.Combined)
                {
                    int result = 0;
                    var commandComb = (Commands.Combined)command;
                    for (int i = 0; i < commandComb.Arguments.Count(); i++)
                    {
                        result += (CountContainingArgument(commandComb.Arguments[i]));
                    }
                    return result;
                }
                else if (command is Commands.Argument)
                {
                    return 1;
                }
                else
                {
                    return command.ArgumentCount;
                }
            }

            public static int CountContainingImmediateValue(ICommand command)
            {
                return GetFixedNumbers(command).Count();
            }

            public static double[] GetFixedNumbers(ICommand command)
            {
                if (command is Commands.Combined)
                {
                    List<double> result = new List<double>();
                    var commandComb = (Commands.Combined)command;
                    for (int i = 0; i < commandComb.Arguments.Count(); i++)
                    {
                        result.AddRange(GetFixedNumbers(commandComb.Arguments[i]));
                    }
                    return result.ToArray();
                }
                else if (command is Commands.ImmediateValue)
                {
                    return new double[] { (command as Commands.ImmediateValue).Value };
                }
                else
                {
                    return new double[0];
                }
            }

            public static ICommand SetFixedNumbers(ICommand arg, params double[] numbers)
            {
                int temp = 0;
                return SetFixedNumbers(arg, ref temp, numbers);
            }

            public static ICommand SetFixedNumbers(ICommand arg, ref int offset, params double[] numbers)
            {
                var command = arg.Duplicate();
                if (command is Commands.Combined)
                {
                    List<double> result = new List<double>();
                    var commandComb = (Commands.Combined)command;
                    for (int i = 0; i < commandComb.Arguments.Count(); i++)
                    {
                        commandComb.Arguments[i] = SetFixedNumbers(commandComb.Arguments[i], ref offset, numbers);
                    }
                    return command;
                }
                else if (command is Commands.ImmediateValue)
                {
                    if (offset < numbers.Count())
                    {
                        (command as Commands.ImmediateValue).Value = numbers[offset];
                        offset++;
                    }
                    else
                    {
                    }
                    return command;
                }
                else
                {
                    return command;
                }
            }

            public static ICommand SwapCommand(ICommand arg, int target, ICommand newCommand, bool CountImmediateValue = true, bool CountArgument = true)
            {
                var command = arg.Duplicate();

                if (target < 0) { return command; }
                if (command is Commands.Combined)
                {
                    if (target == 0)
                    {
                        return newCommand;
                    }
                    var commandComb = (Commands.Combined)command;
                    for (int i = 0; i < commandComb.Arguments.Count(); i++)
                    {
                        var count = CountContainingCommand(commandComb.Arguments[i], CountImmediateValue, CountArgument);
                        if (target < count)
                        {
                            commandComb.Arguments[i] = SwapCommand(commandComb.Arguments[i], target, newCommand, CountImmediateValue, CountArgument);
                        }
                        target -= count;
                    }
                    return command;
                }
                else if (command is Commands.ImmediateValue)
                {
                    if (CountImmediateValue && target == 0)
                    {
                        return newCommand;
                    }
                    return command;
                }
                else if (command is Commands.Argument)
                {
                    if (CountArgument && target == 0)
                    {
                        return newCommand;
                    }
                    return command;
                }
                else
                {
                    if (target == 0)
                    {
                        return newCommand;
                    }
                    return command;
                }
            }

            public static ICommand SwapCommandRandom(ICommand arg, ICommand newCommand, Random rd, bool CountImmediateValue = true, bool CountArgument = true)
            {
                var count = CountContainingCommand(arg, CountImmediateValue, CountArgument);
                return SwapCommand(arg, rd.Next(count), newCommand, CountImmediateValue, CountArgument);
            }
        }
    }
}
