using Antlr4.Runtime.Misc;
using HolyJava.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace HolyJava
{
    #region Definitions

    internal enum Types
    {
        INT, FLOAT, STRING, BOOL, NULL
    }

    internal class Variable
    {
        public string Name { get; set; }
        public object? Value { get; set; }

        public Types Type { get; set; }

        public Variable(string name, Types type, object? value = null)
        {
            Name = name; Type = type; Value = value;
        }
    }

    internal class Function
    {
        public string Name { get; set; }
        public Types ReturnType { get; set; }
        public object? ReturnValue { get; set; } = null;

        public Dictionary<string, Variable> Arguments { get; set; }

        public HolyJavaParser.BlockContext? BlockContext { get; set; }

        public Function
            (string name, Types returnType, Dictionary<string, Variable> arguments, 
            HolyJavaParser.BlockContext? blockContext)
        {
            Name = name; ReturnType = returnType; Arguments = arguments; BlockContext = blockContext;
        }
    }

    #endregion

    internal class HolyJavaVistor : HolyJavaBaseVisitor<object?>
    {
        #region Data
        private readonly Dictionary<string, Variable> Variables = new();
        private readonly Dictionary<string, Function> Functions = new();

        private Function? currentFunction = null;
        #endregion

        public HolyJavaVistor()
        {
            // Init stuff
            Dictionary<string, Variable> printfArgs = new()
            {
                { "msg", new("msg", Types.STRING) }
            };
            Functions.Add("Printf", new("Printf", Types.NULL, printfArgs, null));
        }

        #region Built-In

        #region Functions

        static object? Printf(object msg)
        {
            Console.WriteLine(msg.ToString());
            return null;
        }

        #endregion

        #endregion

        #region Variables

        public override object? VisitVarDeclaration
            ([NotNull] HolyJavaParser.VarDeclarationContext context)
        {
            string name = context.varParameter().IDENTIFIER().GetText();

            if (Variables.ContainsKey(name))
                return null;

            string typeText = context.varParameter().varType().GetText();

            Types type = typeText switch
            {
                "int" => Types.INT,
                "float" => Types.FLOAT,
                "string" => Types.STRING,
                "bool" => Types.BOOL,
                _ => throw new Exception("Invalid type.")
            };

            Variable var = new(name, type);
            Variables.Add(name, var);

            return null;
        }

        public override object? VisitVarAssignment
            ([NotNull] HolyJavaParser.VarAssignmentContext context)
        {
            string name = context.varParameter().IDENTIFIER().GetText();

            if (Variables.ContainsKey(name))
                return null;

            object? value = Visit(context.expression());

            string typeText = context.varParameter().varType().GetText();

            Types type = typeText switch
            {
                "int" => Types.INT,
                "float" => Types.FLOAT,
                "string" => Types.STRING,
                "bool" => Types.BOOL,
                _ => throw new Exception("Invalid type.")
            };

            Variable var = new(name, type);
            Variables.Add(name, var);

            return null;
        }

        public override object? VisitVarReassignment
            ([NotNull] HolyJavaParser.VarReassignmentContext context)
        {
            string name = context.IDENTIFIER().GetText();
            object? value = Visit(context.expression());

            Variables[name].Value = value;

            return null;
        }

        #endregion

        #region Logic

        public override object? VisitIdentifierExpression
            ([NotNull] HolyJavaParser.IdentifierExpressionContext context)
        {
            string name = context.IDENTIFIER().GetText();

            if (!Variables.ContainsKey(name))
                throw new Exception("No variable found with identifier " + name);

            return Variables[name].Value;
        }

        public override object? VisitConstantExpression
            ([NotNull] HolyJavaParser.ConstantExpressionContext context)
        {
            if (context.constant().INT() is { } i)
                return int.Parse(i.GetText());

            if (context.constant().FLOAT() is { } f)
                return float.Parse(f.GetText());

            if (context.constant().STRING() is { } s)
                return s.GetText()[1..^1];

            if (context.constant().BOOL() is { } b)
                return bool.Parse(b.GetText());

            throw new NotImplementedException();
        }

        #endregion

        #region Functions

        public override object? VisitFuncDefinition
            ([NotNull] HolyJavaParser.FuncDefinitionContext context)
        {
            string name = context.IDENTIFIER().GetText();
            Types type = Types.NULL;

            if (Functions.ContainsKey(name))
                return null;

            if (context.varType() != null)
            {
                string typeText = context.varType().GetText();

                type = typeText switch
                {
                    "int" => Types.INT,
                    "float" => Types.FLOAT,
                    "string" => Types.STRING,
                    "bool" => Types.BOOL,
                    _ => throw new Exception("Invalid return type.")
                };
            }

            Dictionary<string, Variable> args = new();

            foreach (var arg in context.varDeclaration())
            {
                string varName = arg.varParameter().IDENTIFIER().GetText();

                string typeText = arg.varParameter().varType().GetText();

                Types varType = typeText switch
                {
                    "int" => Types.INT,
                    "float" => Types.FLOAT,
                    "string" => Types.STRING,
                    "bool" => Types.BOOL,
                    _ => throw new NotImplementedException()
                };

                args.Add(varName, new(varName, varType));
            }

            Function func = new(name, type, args, context.block());
            Functions.Add(name, func);

            return null;
        }

        public override object? VisitFuncCall([NotNull] HolyJavaParser.FuncCallContext context)
        {
            string name = context.IDENTIFIER().GetText();

            if (!Functions.ContainsKey(name))
                throw new Exception("No function found with identifier " + name);

            Function func = Functions[name];

            List<object?> argValues = new();

            foreach (var arg in context.expression())
            {
                argValues.Add(Visit(arg));
            }

            int index = 0;
            foreach (Variable arg in func.Arguments.Values)
            {
                arg.Value = argValues[index];
                index++;
            }

            currentFunction = func;

            if (func.BlockContext != null)
            {
                Visit(func.BlockContext);

                if (func.ReturnType != Types.NULL)
                {
                    if (func.ReturnValue == null)
                        throw new Exception($"Function \"{name}\" must return a value.");

                    return func.ReturnValue;
                }
            }
            else
            {
                if (name == "Printf")
                {
                    Variable var = func.Arguments["msg"];
                    if (var.Value == null)
                        throw new Exception("Argument cannot be null.");
                    Printf(var.Value);
                }
            }

            currentFunction = null;
            return null;
        }

        #endregion
    }
}
