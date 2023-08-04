using Antlr4.Runtime.Misc;
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

    internal struct Statement
    {
        public int identifier;

        public Statement(int identifier)
        {
            this.identifier = identifier;
        }
    }

    internal class Class
    {
        public string Name { get; set; }
        public bool Abstract;

        public Dictionary<string, Variable> Variables { get; private set; } = new();
        public Dictionary<string, Function> Functions { get; private set; } = new();

        public Class(string name, bool @abstract)
        {
            Name = name; Abstract = @abstract;
        }
    }

    internal class ClassVar
    {
        public string Name;
        public Class ClassRef;

        public ClassVar(string name, Class classRef)
        {
            Name = name; ClassRef = classRef;
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
        public object? ReturnValue { get; set; } = null;
        public bool Virtual;
        public bool Abstract;

        public Types ReturnType { get; set; }

        public Dictionary<string, Variable> Arguments { get; set; }

        public HolyJavaParser.BlockContext? BlockContext { get; set; }

        public Function
            (string name, Types returnType, Dictionary<string, Variable> arguments,
            HolyJavaParser.BlockContext? blockContext, bool @virtual, bool @abstract)
        {
            Name = name; ReturnType = returnType; Arguments = arguments; BlockContext = blockContext;
            Virtual = @virtual; Abstract = @abstract;
        }
    }

    #endregion

    internal class HolyJavaVistor : HolyJavaBaseVisitor<object?>
    {
        #region Data
        private readonly Dictionary<string, Variable> Variables = new();
        private readonly Dictionary<string, Function> Functions = new();
        private readonly Dictionary<string, Class> Classes = new();
        private readonly Dictionary<string, ClassVar> ClassVars = new();
        private readonly Dictionary<int, Statement> IfStatements = new();
        private readonly Dictionary<int, Statement> ForLoops = new();

        private string currentScope = string.Empty;
        private Function? currentFunction = null;
        private Function? currentClassFunction = null;
        #endregion

        public HolyJavaVistor()
        {
            // Init stuff
            Dictionary<string, Variable> printfArgs = new()
            {
                { "msg", new("msg", Types.STRING, new Scope()) }
            };
            Functions.Add("Printf", new("Printf", Types.NULL, printfArgs, null,
                false, false));
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

        #region Classes

        public override object? VisitClassDef([NotNull] HolyJavaParser.ClassDefContext context)
        {
            string name = context.IDENTIFIER(0).GetText();
            bool @abstract = context.abstractKeyword() != null;
            Class newClass = new(name, @abstract);

            if (context.IDENTIFIER(1) != null)
            {
                if (!Classes.ContainsKey(context.IDENTIFIER(1).GetText()))
                    throw new Exception("Cannot find class \"" + context.IDENTIFIER(1).GetText() + "\"");
                Class extendedClass = Classes[context.IDENTIFIER(1).GetText()];

                if (!extendedClass.Abstract)
                    throw new Exception("Cannot extend a class that is not marked as abstract.");

                foreach (Variable var in extendedClass.Variables.Values)
                {
                    newClass.Variables.Add(var.Name, var);
                }

                foreach (Function func in extendedClass.Functions.Values)
                {
                    newClass.Functions.Add(func.Name, func);
                }
            }

            foreach (var statement in context.topLevelStatements())
            {
                if (statement.funcDefinition() != null)
                {
                    string funcName = statement.funcDefinition().IDENTIFIER().GetText();
                    Types returnType = Types.NULL;

                    bool funcAbstract = statement.funcDefinition().abstractKeyword() != null;
                    bool @virtual = statement.funcDefinition().virtualKeyword() != null;
                    bool @override = statement.funcDefinition().overrideKeyword() != null;

                    if ((funcAbstract || @virtual) && @override)
                        throw new Exception($"Function \"{funcName}\" cannot override a " +
                                "function because it is marked as either abstract or virtual.");

                    if (funcAbstract)
                    {
                        if (statement.funcDefinition().block() != null)
                            throw new Exception($"Function \"{funcName}\" cannot have a " +
                                "body because it is marked as abstract.");
                    }
                    else
                    {
                        if (statement.funcDefinition().block() == null)
                            throw new Exception($"Function \"{funcName}\" must have a " +
                                "body as it is not marked as abstract.");
                    }

                    if (statement.funcDefinition().varType() != null)
                    {
                        string typeText = statement.funcDefinition().varType().GetText();
                        returnType = typeText switch
                        {
                            "int" => Types.INT,
                            "float" => Types.FLOAT,
                            "string" => Types.STRING,
                            "bool" => Types.BOOL,
                            _ => throw new Exception("Invalid return type.")
                        };
                    }

                    Dictionary<string, Variable> args = new();

                    foreach (var arg in statement.funcDefinition().varDeclaration())
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

                        args.Add(varName, new(varName, varType, new Scope(currentScope, false)));
                    }

                    if (@virtual && funcAbstract)
                        throw new Exception($"Function \"{funcName}\" " +
                            "cannot be both abstract and virtual.");

                    if (@override)
                    {
                        if (newClass.Functions.ContainsKey(funcName))
                        {
                            Function function = newClass.Functions[funcName];
                            if (function.Abstract || function.Virtual)
                            {
                                // Verify that the Functions match
                                if (returnType != function.ReturnType)
                                    throw new Exception("Function override does not match" +
                                        " abstract or virtual method.");

                                if (args.Count != function.Arguments.Count)
                                    throw new Exception("Function override does not match" +
                                        " abstract or virtual method.");

                                foreach (Variable var in args.Values)
                                {
                                    if (!function.Arguments.ContainsKey(var.Name))
                                        throw new Exception("Function override does not match" +
                                            " abstract or virtual method.");

                                    Variable funcVar = function.Arguments[var.Name];

                                    if (funcVar.Type != var.Type)
                                        throw new Exception("Function override does not match" +
                                            " abstract or virtual method.");
                                }

                                function.BlockContext = statement.funcDefinition().block();
                            }
                            else
                                throw new Exception("Cannot override a method that is not marked as " +
                                    "either abstract or virtual.");
                        }
                        else
                            throw new Exception("Cannot override a method that does not exist in the" +
                                " current context.");
                    }
                    else
                    {
                        if (newClass.Functions.ContainsKey(funcName))
                            throw new Exception($"{funcName} already exists.");

                        Function func = new(funcName, returnType, args,
                            statement.funcDefinition().block(), @virtual, funcAbstract);
                        newClass.Functions.Add(funcName, func);
                    }
                }
                else if (statement.varAssignment() != null)
                {
                    string varName = statement.varAssignment().varParameter().IDENTIFIER().GetText();

                    if (newClass.Variables.ContainsKey(varName))
                        throw new Exception($"A variable with the name {varName} already exists.");

                    object? value = Visit(statement.varAssignment().expression());

                    string typeText = statement.varAssignment().varParameter().varType().GetText();

                    Types type = typeText switch
                    {
                        "int" => Types.INT,
                        "float" => Types.FLOAT,
                        "string" => Types.STRING,
                        "bool" => Types.BOOL,
                        _ => throw new Exception("Invalid type.")
                    };

                    newClass.Variables.Add(varName, new(varName, type, new Scope(), value));
                }
                else if (statement.varDeclaration() != null)
                {
                    string varName = statement.varDeclaration().varParameter().IDENTIFIER().GetText();

                    if (newClass.Variables.ContainsKey(varName))
                        throw new Exception($"A variable with the name {varName} already exists.");

                    string typeText = statement.varDeclaration().varParameter().varType().GetText();

                    Types type = typeText switch
                    {
                        "int" => Types.INT,
                        "float" => Types.FLOAT,
                        "string" => Types.STRING,
                        "bool" => Types.BOOL,
                        _ => throw new Exception("Invalid type.")
                    };

                    newClass.Variables.Add(varName, new(varName, type, new Scope()));
                }
            }

            Classes.Add(name, newClass);
            return null;
        }

        public override object VisitClassVar([NotNull] HolyJavaParser.ClassVarContext context)
        {
            string className = context.IDENTIFIER(0).GetText();
            string name = context.IDENTIFIER(1).GetText();

            if (!Classes.ContainsKey(className))
                throw new Exception("No class found with identifier " + className);

            if (Classes[className].Abstract)
                throw new Exception("Cannot make an instance out of an abstract class.");

            ClassVar var = new(name, Classes[className]);
            ClassVars.Add(name, var);

            return var.ClassRef;
        }

        public override object? VisitClassVarReassignment
            ([NotNull] HolyJavaParser.ClassVarReassignmentContext context)
        {
            string className = context.IDENTIFIER(1).GetText();
            string name = context.IDENTIFIER(0).GetText();

            if (!Classes.ContainsKey(className))
                throw new Exception("No class found with identifier " + className);

            if (!ClassVars.ContainsKey(name))
                throw new Exception("No class variable found with identifier " + name);

            ClassVars[name].ClassRef = Classes[className];

            return null;
        }

        public override object? VisitClassAccess
            ([NotNull] HolyJavaParser.ClassAccessContext context)
        {
            string name = context.IDENTIFIER(0).GetText();

            if (!ClassVars.ContainsKey(name))
                throw new Exception("No class variable found with identifier " + name);

            Class ourClass = ClassVars[name].ClassRef;

            if (context.funcCall() != null)
            {
                if (!ourClass.Functions.ContainsKey(context.funcCall().IDENTIFIER().GetText()))
                    throw new Exception($"Function \"{context.funcCall().IDENTIFIER().GetText()}\" " +
                        "does not exist.");

                if (ourClass.Functions[context.funcCall().IDENTIFIER().GetText()].Abstract)
                {
                    if (ourClass.Functions[context.funcCall().IDENTIFIER().GetText()].BlockContext == null)
                    {
                        throw new Exception($"Cannot call " +
                            $"function \"{context.funcCall().IDENTIFIER().GetText()}\"" +
                            $" as it is abstract and has not been overridden yet.");
                    }
                }

                currentClassFunction = ourClass.Functions[context.funcCall().IDENTIFIER().GetText()];
                object? returnValue = Visit(context.funcCall());
                currentClassFunction = null;
                return returnValue;
            }
            else if (context.IDENTIFIER(1) != null)
            {
                return ourClass.Variables[context.IDENTIFIER(1).GetText()].Value;
            }
            else if (context.varReassignment() != null)
            {
                Console.WriteLine("Hi");
                return null;
            }
            else if (context.varAssignment() != null)
            {
                Console.WriteLine("Hifwe");
                return null;
            }

            throw new Exception("Unexpected token; Expected: funcCall | " +
                "varReassignment | varAssignment | IDENTIFIER");
        }

        #endregion

        #region Logic

        public override object? VisitIfLogic([NotNull] HolyJavaParser.IfLogicContext context)
        {
            if (Visit(context.expression()) == null)
            {
                throw new Exception("Invalid syntax.");
            }

            if (Convert.ToBoolean(Visit(context.expression())))
            {
                string temp = currentScope;
                int index = IfStatements.Count - 1;
                IfStatements.Add(index, new Statement(index));
                currentScope = index.ToString();
                Visit(context.block());

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master ==
                        IfStatements[int.Parse(currentScope)].identifier.ToString())
                    {
                        var.Scope = new Scope(currentScope, true);
                    }
                }

                currentScope = temp;
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

                    if (Convert.ToBoolean(Visit(elseIf.expression())))
                    {
                        string temp = currentScope;
                        int index = IfStatements.Count - 1;
                        IfStatements.Add(index, new Statement(index));
                        currentScope = index.ToString();
                        Visit(elseIf.block());

                        foreach (Variable var in Variables.Values)
                        {
                            if (var.Scope.master ==
                                IfStatements[int.Parse(currentScope)].identifier.ToString())
                            {
                                var.Scope = new Scope(currentScope, true);
                            }
                        }

                        currentScope = temp;
                        succeeded = true;
                        break;
                    }
                }

                if (!succeeded)
                    Visit(context.elseLogic().block());
            }
            else if (context.elseLogic() != null)
            {
                string temp = currentScope;
                int index = IfStatements.Count - 1;
                IfStatements.Add(index, new Statement(index));
                currentScope = index.ToString();
                Visit(context.elseLogic().block());

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master ==
                        IfStatements[int.Parse(currentScope)].identifier.ToString())
                    {
                        var.Scope = new Scope(currentScope, true);
                    }
                }

                currentScope = temp;
            }

            return null;
        }

        public override object? VisitWhileLoop([NotNull] HolyJavaParser.WhileLoopContext context)
        {
            string temp = currentScope;
            int index = ForLoops.Count;
            ForLoops.Add(index, new Statement(index));
            currentScope = index.ToString();

            while (Convert.ToBoolean(Visit(context.expression())))
            {
                Visit(context.block());

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master == currentScope)
                        var.Scope = new Scope(currentScope, true);
                }
            }

            currentScope = temp;
            return null;
        }

        public override object? VisitForLoop([NotNull] HolyJavaParser.ForLoopContext context)
        {
            string temp = currentScope;
            int index = ForLoops.Count;
            ForLoops.Add(index, new Statement(index));
            currentScope = index.ToString();

            Visit(context.varAssignment());
            string varName = context.varAssignment().varParameter().IDENTIFIER().GetText();

            while (Convert.ToBoolean(Visit(context.expression(0))))
            {
                Visit(context.block());

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master == currentScope)
                    {
                        if (var.Name != varName)
                            var.Scope = new Scope(currentScope, true);
                    }
                }

                Variables[varName].Value = Visit(context.expression(1));
            }

            Variables[varName].Scope = new Scope(currentScope, true);
            currentScope = temp;
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

            Variable var = new(name, type, new Scope(currentScope, false));
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

            Variable var = new(name, type, new Scope(currentScope, false), value);
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

            bool @abstract = context.abstractKeyword() != null;
            bool @virtual = context.virtualKeyword() != null;
            bool @override = context.overrideKeyword() != null;

            if (@override)
                throw new Exception($"Function \"{name}\" " +
                    "cannot override any function because it is not contained within " +
                    "a class.");

            if (@virtual || @abstract)
                throw new Exception($"Function \"{name}\" " +
                    "cannot be either abstract or virtual because it is not contained within " +
                    "an abstract class.");

            if (Functions.ContainsKey(name))
                throw new Exception($"\"{name}\" is already defined.");

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

                args.Add(varName, new(varName, varType, new Scope(currentScope, false)));
            }

            Function func = new(name, type, args, context.block(), @virtual, @abstract);
            Functions.Add(name, func);

            return null;
        }

        public override object? VisitFuncCall([NotNull] HolyJavaParser.FuncCallContext context)
        {
            string name = context.IDENTIFIER().GetText();

            Function func;

            if (!Functions.ContainsKey(name))
            {
                if (currentClassFunction != null)
                {
                    func = currentClassFunction;
                }
                else
                    throw new Exception("No function found with identifier " + name);
            }
            else
                func = Functions[name];

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
                    if (var.Scope.master == currentScope)
                        var.Scope = new Scope(currentScope, true);
                }

                string temp = currentScope;
                currentScope = name;
                Visit(func.BlockContext);

                foreach (Variable var in Variables.Values)
                {
                    if (var.Scope.master == currentScope)
                        var.Scope = new Scope(currentScope, true);

                    if (var.Scope.master == temp)
                        var.Scope = new Scope(temp, false);
                }

                currentScope = temp;

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