﻿using Antlr4.Runtime.Misc;
using HolyJava.Content;

namespace HolyJava
{
    #region Definitions

    internal enum Types
    {
        INT, FLOAT, STRING, BOOL, NULL
    }

    internal struct Scope
    {
        public string master;
        public bool dead;

        public Scope(string master, bool dead)
        {
            this.master = master; this.dead = dead;
        }
    }

    internal struct IfStatement
    {
        public int identifier;

        public IfStatement(int identifier)
        {
            this.identifier = identifier;
        }
    }

    internal class Variable
    {
        public string Name { get; set; }
        public object? Value { get; set; }

        public Types Type { get; set; }
        public Scope Scope { get; set; }

        public Variable(string name, Types type, Scope scope, object? value = null)
        {
            Name = name; Type = type; Value = value; Scope = scope;
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
        private readonly Dictionary<int, IfStatement> IfStatements = new();

        private string oneTimeCreator = string.Empty;
        private Function? currentFunction = null;
        #endregion

        public HolyJavaVistor()
        {
            // Init stuff
            Dictionary<string, Variable> printfArgs = new()
            {
                { "msg", new("msg", Types.STRING, new Scope()) }
            };
            Functions.Add("Printf", new("Printf", Types.NULL, printfArgs, null));
        }

        #region Logic

        public override object? VisitIfLogic([NotNull] HolyJavaParser.IfLogicContext context)
        {
            if (Visit(context.expression()) == null)
            {
                throw new Exception("Invalid syntax.");
            }

            if ((bool)Visit(context.expression()))
            {
                string temp = oneTimeCreator;
                int index = IfStatements.Count - 1;
                IfStatements.Add(index, new IfStatement(index));
                oneTimeCreator = index.ToString();
                Visit(context.block());

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master == 
                        IfStatements[int.Parse(oneTimeCreator)].identifier.ToString())
                    {
                        var.Scope = new Scope(oneTimeCreator, true);
                    }
                }

                oneTimeCreator = temp;
            }
            else if (context.elseIfLogic().Length > 0)
            {
                bool succeeded = false;

                foreach (var elseIf in context.elseIfLogic())
                {
                    if (Visit(elseIf.expression()) == null)
                    {
                        throw new Exception("Invalid syntax.");
                    }

                    if ((bool)Visit(elseIf.expression()))
                    {
                        string temp = oneTimeCreator;
                        int index = IfStatements.Count - 1;
                        IfStatements.Add(index, new IfStatement(index));
                        oneTimeCreator = index.ToString();
                        Visit(elseIf.block());

                        foreach (Variable var in Variables.Values)
                        {
                            if (var.Scope.master == 
                                IfStatements[int.Parse(oneTimeCreator)].identifier.ToString())
                            {
                                var.Scope = new Scope(oneTimeCreator, true);
                            }
                        }

                        oneTimeCreator = temp;
                        succeeded = true;
                        break;
                    }
                }

                if (!succeeded)
                    Visit(context.elseLogic().block());
            }
            else if (context.elseLogic() != null)
            {
                string temp = oneTimeCreator;
                int index = IfStatements.Count - 1;
                IfStatements.Add(index, new IfStatement(index));
                oneTimeCreator = index.ToString();
                Visit(context.elseLogic().block());

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master ==
                        IfStatements[int.Parse(oneTimeCreator)].identifier.ToString())
                    {
                        var.Scope = new Scope(oneTimeCreator, true);
                    }
                }

                oneTimeCreator = temp;
            }

            return null;
        }

        public override object? VisitReturnStatement
            ([NotNull] HolyJavaParser.ReturnStatementContext context)
        {
            if (currentFunction == null)
                throw new Exception("Keyword \"return\" cannot be used outside of a function.");

            currentFunction.ReturnValue = Visit(context.expression());
            return null;
        }
        #endregion

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
            {
                if (!Variables[name].Scope.dead)
                    throw new Exception($"Variable \"{name}\" already exists.");
            }

            string typeText = context.varParameter().varType().GetText();

            Types type = typeText switch
            {
                "int" => Types.INT,
                "float" => Types.FLOAT,
                "string" => Types.STRING,
                "bool" => Types.BOOL,
                _ => throw new Exception("Invalid type.")
            };



            Variable var = new(name, type, new Scope(oneTimeCreator, false));
            if (Variables.ContainsKey(name))
                Variables[name] = var;
            else
            {
                Variables.Add(name, var);
            }

            return null;
        }

        public override object? VisitVarAssignment
            ([NotNull] HolyJavaParser.VarAssignmentContext context)
        {
            string name = context.varParameter().IDENTIFIER().GetText();

            if (Variables.ContainsKey(name))
            {
                if (!Variables[name].Scope.dead)
                    throw new Exception($"Variable \"{name}\" already exists.");
            }

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

            Variable var = new(name, type, new Scope(oneTimeCreator, false), value);
            if (Variables.ContainsKey(name))
                Variables[name] = var;
            else
            {
                Variables.Add(name, var);
            }
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

        #region Math

        public static object NE_Compare(object left, object right)
        {

            if (left is int l && right is int r)
            {

                if (l != r)
                    return true;
                return false;

            }

            if (left is float lF && right is float rF)
            {

                if (lF != rF)
                    return true;
                return false;

            }

            if (left is int lI && right is float RF)
            {

                if (lI != RF)
                    return true;
                return false;

            }

            if (left is float LF && right is int rI)
            {

                if (LF != rI)
                    return true;
                return false;

            }

            throw new Exception("Cannot compare the types of " + left.GetType() + " and " + right.GetType());

        }

        public static object EE_Compare(object left, object right)
        {

            if (left is int l && right is int r)
            {

                if (l == r)
                    return true;
                return false;

            }

            if (left is float lF && right is float rF)
            {

                if (lF == rF)
                    return true;
                return false;

            }

            if (left is int lI && right is float RF)
            {

                if (lI == RF)
                    return true;
                return false;

            }

            if (left is float LF && right is int rI)
            {

                if (LF == rI)
                    return true;
                return false;

            }

            throw new Exception("Cannot compare the types of " + left.GetType() + " and " + right.GetType());

        }

        public static object L_Compare(object left, object right)
        {

            if (left is int l && right is int r)
            {

                if (l < r)
                    return true;
                return false;

            }

            if (left is float lF && right is float rF)
            {

                if (lF < rF)
                    return true;
                return false;

            }

            if (left is int lI && right is float RF)
            {

                if (lI < RF)
                    return true;
                return false;

            }

            if (left is float LF && right is int rI)
            {

                if (LF < rI)
                    return true;
                return false;

            }

            throw new Exception("Cannot compare the types of " + left.GetType() + " and " + right.GetType());

        }

        public static object LE_Compare(object left, object right)
        {

            if (left is int l && right is int r)
            {

                if (l <= r)
                    return true;
                return false;

            }

            if (left is float lF && right is float rF)
            {

                if (lF <= rF)
                    return true;
                return false;

            }

            if (left is int lI && right is float RF)
            {

                if (lI <= RF)
                    return true;
                return false;

            }

            if (left is float LF && right is int rI)
            {

                if (LF <= rI)
                    return true;
                return false;

            }

            throw new Exception("Cannot compare the types of " + left.GetType() + " and " + right.GetType());

        }

        public static object G_Compare(object left, object right)
        {

            if (left is int l && right is int r)
            {

                if (l > r)
                    return true;
                return false;

            }

            if (left is float lF && right is float rF)
            {

                if (lF > rF)
                    return true;
                return false;

            }

            if (left is int lI && right is float RF)
            {

                if (lI > RF)
                    return true;
                return false;

            }

            if (left is float LF && right is int rI)
            {

                if (LF > rI)
                    return true;
                return false;

            }

            throw new Exception("Cannot compare the types of " + left.GetType() + " and " + right.GetType());

        }

        public static object GE_Compare(object left, object right)
        {

            if (left is int l && right is int r)
            {

                if (l >= r)
                    return true;
                return false;

            }

            if (left is float lF && right is float rF)
            {

                if (lF >= rF)
                    return true;
                return false;

            }

            if (left is int lI && right is float RF)
            {

                if (lI >= RF)
                    return true;
                return false;

            }

            if (left is float LF && right is int rI)
            {

                if (LF >= rI)
                    return true;
                return false;

            }

            throw new Exception("Cannot compare the types of " + left.GetType() + " and " + right.GetType());

        }

        public static object Mul(object left, object right)
        {

            if (left is int l && right is int r)
                return l * r;

            if (left is float lF && right is float rF)
                return lF * rF;

            if (left is float lFloat && right is int rInt)
                return lFloat * rInt;

            if (left is int lInt && right is float rFloat)
                return lInt * rFloat;

            throw new NotImplementedException();

        }

        public static object Div(object left, object right)
        {

            if (left is int l && right is int r)
                return l / r;

            if (left is float lF && right is float rF)
                return lF / rF;

            if (left is float lFloat && right is int rInt)
                return lFloat / rInt;

            if (left is int lInt && right is float rFloat)
                return lInt / rFloat;

            throw new NotImplementedException();

        }

        public static object Sub(object left, object right)
        {

            if (left is int l && right is int r)
                return l - r;

            if (left is float lF && right is float rF)
                return lF - rF;

            if (left is float lFloat && right is int rInt)
                return lFloat - rInt;

            if (left is int lInt && right is float rFloat)
                return lInt - rFloat;

            throw new NotImplementedException();

        }

        public static object Add(object left, object right)
        {

            if (left is int l && right is int r)
                return l + r;

            if (left is float lF && right is float rF)
                return lF + rF;

            if (left is float lFloat && right is int rInt)
                return lFloat + rInt;

            if (left is int lInt && right is float rFloat)
                return lInt + rFloat;

            if (left is string lString && right is string rString)
                return lString + rString;

            if (left is string lStringS && right is int rIntS)
                return $"{lStringS}{rIntS}";

            if (left is int lIntI && right is string rStringI)
                return $"{lIntI}{rStringI}";

            if (left is string lStringF && right is float rFloatF)
                return $"{lStringF}{rFloatF}";

            if (left is float lFloatF && right is string rStringF)
                return $"{lFloatF}{rStringF}";

            throw new Exception($"Cannot add the values of types {left.GetType()} and {right.GetType()}");

        }

        public override object VisitComparisonExpression
            ([NotNull] HolyJavaParser.ComparisonExpressionContext context)
        {

            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));

            var op = context.comparisonOps().GetText();

            if (left == null || right == null)
                throw new Exception("Invalid math operation.");

            return op switch
            {

                "==" => EE_Compare(left, right),
                "!=" => NE_Compare(left, right),
                "<" => L_Compare(left, right),
                "<=" => LE_Compare(left, right),
                ">" => G_Compare(left, right),
                ">=" => GE_Compare(left, right),
                _ => throw new Exception("Unexpected comparison operator")

            };

        }

        public override object VisitConstantExpression
            ([NotNull] HolyJavaParser.ConstantExpressionContext context)
        {
            if (context.constant().INT() is { } i)
            {
                string text;

                if (i.GetText()[0] == '-')
                {
                    text = i.GetText()[1..];
                }
                else
                    text = i.GetText();

                return int.Parse(text);
            }

            if (context.constant().FLOAT() is { } f)
            {
                string text;

                if (f.GetText()[0] == '-')
                {
                    text = f.GetText()[1..];
                }
                else
                    text = f.GetText();

                return float.Parse(text);
            }

            if (context.constant().STRING() is { } s)
                return s.GetText()[1..^1];

            if (context.constant().BOOL() is { } b)
                return bool.Parse(b.GetText());

            throw new NotImplementedException();

        }

        public override object? VisitIdentifierExpression
            ([NotNull] HolyJavaParser.IdentifierExpressionContext context)
        {

            string name = context.IDENTIFIER().GetText();

            if (currentFunction != null)
            {
                if (currentFunction.Arguments.ContainsKey(name))
                {
                    return currentFunction.Arguments[name].Value;
                }
            }

            if (Variables.ContainsKey(name))
            {
                if (Variables[name].Scope.dead)
                    throw new Exception($"Variable \"{name}\" cannot be accessed while out of scope.");
            }

            if (Variables.ContainsKey(name))
            {
                return Variables[name].Value;
            }

            throw new Exception("No variable found with identifer " + name);
        }

        public override object VisitAdditiveExpression
            ([NotNull] HolyJavaParser.AdditiveExpressionContext context)
        {

            var left = Visit(context.expression(0));
            var op = context.addOp().GetText();

            var right = Visit(context.expression(1));

            if (left == null || right == null)
                throw new Exception("Invalid math operation.");

            return op switch
            {

                "+" => Add(left, right),
                "-" => Sub(left, right),
                _ => throw new NotImplementedException()

            };

        }

        public override object VisitMultiplicativeExpression
            ([NotNull] HolyJavaParser.MultiplicativeExpressionContext context)
        {

            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));

            var op = context.multOp().GetText();

            if (left == null || right == null)
                throw new Exception("Invalid math operation.");

            return op switch
            {

                "*" => Mul(left, right),
                "/" => Div(left, right),
                _ => throw new NotImplementedException()

            };

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

                args.Add(varName, new(varName, varType, new Scope(oneTimeCreator, false)));
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

            Function? tempFunc = currentFunction;
            currentFunction = func;

            if (func.BlockContext != null)
            {
                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master == oneTimeCreator)
                        var.Scope = new Scope(oneTimeCreator, true);
                }

                string temp = oneTimeCreator;
                oneTimeCreator = name;
                Visit(func.BlockContext);

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master == oneTimeCreator)
                        var.Scope = new Scope(oneTimeCreator, true);

                    if (var.Scope.master == temp)
                        var.Scope = new Scope(temp, false);
                }

                oneTimeCreator = temp;

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

            currentFunction = tempFunc;
            return null;
        }

        #endregion
    }
}
