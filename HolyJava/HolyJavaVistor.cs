using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using HolyJava.Content;
using HolyJava.Error_Handling;
using System;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HolyJava
{
    #region Definitions

    internal enum Types
    {
        INT, FLOAT, STRING, BOOL, OBJECT, NULL
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

    internal class IncludedFile
    {
        public bool builtIn = false;
        public bool skip = false;
        public bool over = false;

        public Dictionary<string, Function> Functions { get; private set; } = new();
        public Dictionary<string, Variable> Variables { get; private set; } = new();
        public Dictionary<string, ClassVar> ClassVars { get; private set; } = new();
        public Dictionary<string, Class> Classes { get; private set; } = new();
        public Dictionary<string, Array> Arrays { get; private set; } = new();
    }

    internal class Class
    {
        public string Name { get; set; }
        public bool Abstract;
        public bool Static;

        public Thread Thread = null;

        public Function? Constructor { get; set; }

        public string File;

        public Dictionary<string, Variable> Variables { get; private set; } = new();
        public Dictionary<string, Function> Functions { get; private set; } = new();
        public Dictionary<string, Array> Arrays { get; private set; } = new();

        public Class(string name, bool @abstract, bool @static, string file = "", Function? constructor = null)
        {
            Name = name; Abstract = @abstract; Static = @static;
            File = file;
            Constructor = constructor;
        }
    }

    internal class ClassVar
    {
        public string Name;
        public Class ClassRef;

        public string File;

        public ClassVar(string name, Class classRef, string file = "")
        {
            Name = name; ClassRef = classRef;
            File = file;
        }
    }

    internal class Array
    {
        public string Name;
        public Types Type;

        public int Size;

        public string File;

        public Dictionary<int, Variable> Values { get; private set; } = new();

        public Array(string name, Types type, int size, string file = "")
        {
            Name = name; Type = type; Size = size;
            File = file;
        }
    }

    internal class Variable
    {
        public string Name { get; set; }
        public object? Value { get; set; }

        public Types Type { get; set; }
        public Scope Scope { get; set; }

        public string File;

        public Variable(string name, Types type, Scope scope, object? value = null, string file = "")
        {
            Name = name; Type = type; Value = value; Scope = scope;
            File = file;
        }
    }

    internal class Function
    {
        public string Name { get; set; }
        public object? ReturnValue { get; set; } = null;
        public bool Virtual;
        public bool Abstract;

        public string File;

        public Types ReturnType { get; set; }

        public Dictionary<string, Variable> Arguments { get; set; }

        public HolyJavaParser.BlockContext? BlockContext { get; set; }

        public Function
            (string name, Types returnType, Dictionary<string, Variable> arguments,
            HolyJavaParser.BlockContext? blockContext, bool @virtual, bool @abstract, string file = "")
        {
            Name = name; ReturnType = returnType; Arguments = arguments; BlockContext = blockContext;
            Virtual = @virtual; Abstract = @abstract;
            File = file;
        }
    }

    #endregion

    internal class HolyJavaVistor : HolyJavaBaseVisitor<object?>
    {
        private readonly IErrorReporter errorReporter;
        private readonly CommonTokenStream stream;

        #region Data
        private readonly Dictionary<string, Variable> Variables = new();
        private readonly Dictionary<string, Function> Functions = new();
        private readonly Dictionary<string, Class> Classes = new();
        private readonly Dictionary<string, ClassVar> ClassVars = new();
        private readonly Dictionary<string, Array> Arrays = new();
        private readonly Dictionary<string, IncludedFile> Files = new();
        private readonly Dictionary<string, IncludedFile> BuiltInFiles = new();
        private readonly Dictionary<string, IncludedFile> IncludedFiles = new();
        private readonly Dictionary<int, Statement> IfStatements = new();
        private readonly Dictionary<int, Statement> ForLoops = new();

        public readonly List<CommonTokenStream> streams = new();
        public int streamIndex = 0;

        private string currentScope = string.Empty;
        private Class? classIn = null;
        private Class? currentClass = null;
        private Function? currentFunction = null;
        private Function? currentClassFunction = null;
        #endregion

        public HolyJavaVistor(IErrorReporter errorReporter, 
            CommonTokenStream stream)
        {
            this.errorReporter = errorReporter;
            this.stream = stream;
            streams.Add(this.stream);

            // Init stuff
            Dictionary<string, Variable> printfArgs = new()
            {
                { "msg", new("msg", Types.STRING, new Scope()) }
            };
            Functions.Add("Printf", new("Printf", Types.NULL, printfArgs, null,
                false, false));

            AddMathClass();

            AddThreadingFile();
            AddSocketsFile();
        }

        #region Built-In

        #region Functions

        static object? Printf(object msg)
        {
            Console.WriteLine(msg.ToString());
            return null;
        }

        #endregion

        #region Classes

        void AddMathClass()
        {
            Class math = new("Math", false, true);
            math.Variables.Add("PI", new("PI", Types.FLOAT, new Scope(), Math.PI));

            Dictionary<string, Variable> squareArgs = new()
            {
                { "value", new("value", Types.OBJECT, new Scope()) },
                { "power", new("power", Types.OBJECT, new Scope()) }
            };
            math.Functions.Add("Power", new("Power", Types.OBJECT, squareArgs,
                null, false, false));
            Classes.Add(math.Name, math);
        }

        #endregion

        #region Files

        void AddSocketsFile()
        {

        }

        void AddThreadingFile()
        {
            IncludedFile threading = new()
            {
                builtIn = true
            };
            Class threadClass = new("Thread", false, false);
            Dictionary<string, Variable> cArgs = new()
            {
                ["function"] = new("function", Types.STRING, new Scope(), null)
            };
            threadClass.Constructor = new Function("Thread", Types.NULL, cArgs, null, false, false);
            threadClass.Functions.Add("Start", new("Start", Types.NULL, new(), null, false, false));

            threading.Classes.Add("Thread", threadClass);
            BuiltInFiles.Add("Threading", threading);
        }

        #endregion

        #endregion

        #region Include

        public override object? VisitIncludeKeyword
            ([NotNull] HolyJavaParser.IncludeKeywordContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, 
                context.Stop.TokenIndex);

            string fn = context.STRING().GetText()[1..^1];
            if (!File.Exists(fn))
            {
                if (BuiltInFiles.ContainsKey(fn))
                {
                    if (context.IDENTIFIER() != null)
                    {
                        bool over = context.overrideMod() != null;
                        bool skip = context.skipMod() != null;

                        BuiltInFiles[fn].over = over;
                        BuiltInFiles[fn].skip = skip;
                        string name = context.IDENTIFIER().GetText();
                        foreach (Function function in BuiltInFiles[fn].Functions.Values)
                        {
                            function.File = fn;
                        }

                        foreach (ClassVar function in BuiltInFiles[fn].ClassVars.Values)
                        {
                            function.File = fn;
                        }

                        foreach (Class @class in BuiltInFiles[fn].Classes.Values)
                        {
                            @class.File = fn;
                        }

                        foreach (Variable function in BuiltInFiles[fn].Variables.Values)
                        {
                            function.File = fn;
                        }

                        foreach (Array function in BuiltInFiles[fn].Arrays.Values)
                        {
                            function.File = fn;
                        }
                        Files.Add(name, BuiltInFiles[fn]);
                        IncludedFiles.Add(fn, BuiltInFiles[fn]);
                    }
                    else
                    {
                        bool over = context.overrideMod() != null;
                        bool skip = context.skipMod() != null;

                        BuiltInFiles[fn].over = over;
                        BuiltInFiles[fn].skip = skip;

                        foreach (Function function in BuiltInFiles[fn].Functions.Values)
                        {
                            function.File = fn;
                            if (Functions.ContainsKey(function.Name))
                            {
                                if (over)
                                {
                                    Functions[function.Name] = function;
                                }
                                else if (skip)
                                {
                                    continue;
                                }
                                else
                                {
                                    errorReporter.ReportError(context, $"Importing file {fn} " +
                                        $"conflicts with existing functions. If you wish to override " +
                                        $"your existing implementations, use \"!\". " +
                                        $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                                }
                            }
                            Functions.Add(function.Name, function);
                        }

                        foreach (ClassVar function in BuiltInFiles[fn].ClassVars.Values)
                        {
                            function.File = fn;
                            if (ClassVars.ContainsKey(function.Name))
                            {
                                if (over)
                                {
                                    ClassVars[function.Name] = function;
                                }
                                else if (skip)
                                {
                                    continue;
                                }
                                else
                                {
                                    errorReporter.ReportError(context, $"Importing file {fn} " +
                                        $"conflicts with existing functions. If you wish to override " +
                                        $"your existing implementations, use \"!\". " +
                                        $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                                }
                            }
                            ClassVars.Add(function.Name, function);
                        }

                        foreach (Class @class in BuiltInFiles[fn].Classes.Values)
                        {
                            @class.File = fn;
                            if (Classes.ContainsKey(@class.Name))
                            {
                                if (over)
                                {
                                    Classes[@class.Name] = @class;
                                }
                                else if (skip)
                                {
                                    continue;
                                }
                                else
                                {
                                    errorReporter.ReportError(context, $"Importing file {fn} " +
                                        $"conflicts with existing functions. If you wish to override " +
                                        $"your existing implementations, use \"!\". " +
                                        $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                                }
                            }
                            Classes.Add(@class.Name, @class);
                        }

                        foreach (Variable function in BuiltInFiles[fn].Variables.Values)
                        {
                            function.File = fn;
                            if (Variables.ContainsKey(function.Name))
                            {
                                if (over)
                                {
                                    Variables[function.Name] = function;
                                }
                                else if (skip)
                                {
                                    continue;
                                }
                                else
                                {
                                    errorReporter.ReportError(context, $"Importing file {fn} " +
                                        $"conflicts with existing functions. If you wish to override " +
                                        $"your existing implementations, use \"!\". " +
                                        $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                                }
                            }
                            Variables.Add(function.Name, function);
                        }

                        foreach (Array function in BuiltInFiles[fn].Arrays.Values)
                        {
                            function.File = fn;
                            if (Arrays.ContainsKey(function.Name))
                            {
                                if (over)
                                {
                                    Arrays[function.Name] = function;
                                }
                                else if (skip)
                                {
                                    continue;
                                }
                                else
                                {
                                    errorReporter.ReportError(context, $"Importing file {fn} " +
                                        $"conflicts with existing functions. If you wish to override " +
                                        $"your existing implementations, use \"!\". " +
                                        $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                                }
                            }
                            Arrays.Add(function.Name, function);
                        }

                        IncludedFiles.Add(fn, BuiltInFiles[fn]);
                    }
                    return null;
                }
                errorReporter.ReportError(context, $"File \"{fn}\" does not exist", 
                    tokens[1], 
                    streams[streamIndex]);
            }

            // Parse entire file
            AntlrInputStream inputStream = new(File.ReadAllText(fn).ToString());
            HolyJavaLexer holyJavaLexer = new(inputStream);
            CommonTokenStream commonTokenStream = new(holyJavaLexer);
            streams.Add(commonTokenStream);
            ParserErrors errors = new(new FileInfo(fn).Name);
            HolyJavaParser holyJavaParser = new(commonTokenStream)
            {
                ErrorHandler = errors
            };
            var fileContext = holyJavaParser.program();
            IList<IToken> fileTokens = commonTokenStream.GetTokens();
            HolyJavaVistor visitor = new(new ErrorHandler(fn, fileTokens[0]),
                commonTokenStream);
            visitor.Visit(fileContext);

            IncludedFile file = new();

            if (context.IDENTIFIER() != null)
            {
                bool over = context.overrideMod() != null;
                bool skip = context.skipMod() != null;

                file.over = over;
                file.skip = skip;
                string name = context.IDENTIFIER().GetText();
                foreach (Function function in visitor.Functions.Values)
                {
                    function.File = fn;
                    file.Functions.Add(function.Name, function);
                }

                foreach (ClassVar function in visitor.ClassVars.Values)
                {
                    function.File = fn;
                    file.ClassVars.Add(function.Name, function);
                }

                foreach (Class function in visitor.Classes.Values)
                {
                    function.File = fn;
                    file.Classes.Add(function.Name, function);
                }

                foreach (Variable function in visitor.Variables.Values)
                {
                    function.File = fn;
                    file.Variables.Add(function.Name, function);
                }

                foreach (Array function in visitor.Arrays.Values)
                {
                    function.File = fn;
                    file.Arrays.Add(function.Name, function);
                }

                Files.Add(name, file);
                IncludedFiles.Add(fn, file);
            }
            else
            {
                bool over = context.overrideMod() != null;
                bool skip = context.skipMod() != null;

                file.over = over;
                file.skip = skip;

                foreach (Function function in visitor.Functions.Values)
                {
                    function.File = fn;
                    if (Functions.ContainsKey(function.Name))
                    {
                        if (over)
                        {
                            Functions[function.Name] = function;
                        }
                        else if (skip)
                        {
                            continue;
                        }
                        else
                        {
                            errorReporter.ReportError(context, $"Importing file {fn} " +
                                $"conflicts with existing functions. If you wish to override " +
                                $"your existing implementations, use \"!\". " +
                                $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                        }
                    }
                    Functions.Add(function.Name, function);
                    file.Functions.Add(function.Name, function);
                }

                foreach (ClassVar function in visitor.ClassVars.Values)
                {
                    function.File = fn;
                    if (ClassVars.ContainsKey(function.Name))
                    {
                        if (over)
                        {
                            ClassVars[function.Name] = function;
                        }
                        else if (skip)
                        {
                            continue;
                        }
                        else
                        {
                            errorReporter.ReportError(context, $"Importing file {fn} " +
                                $"conflicts with existing functions. If you wish to override " +
                                $"your existing implementations, use \"!\". " +
                                $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                        }
                    }
                    ClassVars.Add(function.Name, function);
                    file.ClassVars.Add(function.Name, function);
                }

                foreach (Class @class in visitor.Classes.Values)
                {
                    @class.File = fn;
                    if (Classes.ContainsKey(@class.Name))
                    {
                        if (over)
                        {
                            Classes[@class.Name] = @class;
                        }
                        else if (skip)
                        {
                            continue;
                        }
                        else
                        {
                            errorReporter.ReportError(context, $"Importing file {fn} " +
                                $"conflicts with existing functions. If you wish to override " +
                                $"your existing implementations, use \"!\". " +
                                $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                        }
                    }
                    Classes.Add(@class.Name, @class);
                    file.Classes.Add(@class.Name, @class);
                }

                foreach (Variable function in visitor.Variables.Values)
                {
                    function.File = fn;
                    if (Variables.ContainsKey(function.Name))
                    {
                        if (over)
                        {
                            Variables[function.Name] = function;
                        }
                        else if (skip)
                        {
                            continue;
                        }
                        else
                        {
                            errorReporter.ReportError(context, $"Importing file {fn} " +
                                $"conflicts with existing functions. If you wish to override " +
                                $"your existing implementations, use \"!\". " +
                                $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                        }
                    }
                    Variables.Add(function.Name, function);
                    file.Variables.Add(function.Name, function);
                }

                foreach (Array function in visitor.Arrays.Values)
                {
                    function.File = fn;
                    if (Arrays.ContainsKey(function.Name))
                    {
                        if (over)
                        {
                            Arrays[function.Name] = function;
                        }
                        else if (skip)
                        {
                            continue;
                        }
                        else
                        {
                            errorReporter.ReportError(context, $"Importing file {fn} " +
                                $"conflicts with existing functions. If you wish to override " +
                                $"your existing implementations, use \"!\". " +
                                $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                        }
                    }
                    Arrays.Add(function.Name, function);
                    file.Arrays.Add(function.Name, function);
                }

                IncludedFiles.Add(fn, file);
            }
            return null;
        }

        public override object? VisitIncludeCall
            ([NotNull] HolyJavaParser.IncludeCallContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string fileName = context.IDENTIFIER(0).GetText();

            if (!Files.ContainsKey(fileName))
            {
                errorReporter.ReportError
                    (context, $"File \"{fileName}\" has not been included", tokens[0], 
                    streams[streamIndex]);
            }
            IncludedFile file = Files[fileName];

            if (context.IDENTIFIER(1) != null)
            {
                if (!file.Variables.ContainsKey(context.IDENTIFIER(1).GetText()))
                    errorReporter.ReportError(context, $"File \"{fileName}\" " +
                        $"does not contain variable \"{context.IDENTIFIER(1).GetText()}\"", tokens[1], streams[streamIndex]);
                currentClass = null;
                return file.Variables[context.IDENTIFIER(1).GetText()].Value;
            }
            else if (context.varReassignment() != null)
            {
                if (!file.Variables.ContainsKey(context.varReassignment().IDENTIFIER().GetText()))
                    errorReporter.ReportError(context, $"File \"{fileName}\" " +
                        $"does not contain variable \"{context.IDENTIFIER(1).GetText()}\"", tokens[2], streams[streamIndex]);
                file.Variables[context.varReassignment().IDENTIFIER().GetText()].Value = 
                    Visit(context.varReassignment().expression());
                currentClass = null;
                return null;
            }
            else if (context.classAccess() != null)
            {
                string name = context.classAccess().IDENTIFIER(0).GetText();
                Class? ourClass = null;

                if (!file.ClassVars.ContainsKey(name))
                {
                    if (!file.Classes.ContainsKey(name))
                        errorReporter.ReportError(context, $"Class variable \"{name}\" does not exist",
                            tokens[2], streams[streamIndex]);
                    else
                    {
                        if (!file.Classes[name].Static)
                            errorReporter.ReportError(context, $"Class {name} does not exist", tokens[1], streams[streamIndex]);
                        else
                            ourClass = file.Classes[name];
                    }
                }
                else
                    ourClass = file.ClassVars[name].ClassRef;

                currentClass = ourClass;
                if (context.classAccess().funcCall() != null)
                {
                    string functionName = context.classAccess().funcCall().IDENTIFIER().GetText();

                    Function func;

                    if (!ourClass.Functions.ContainsKey(functionName))
                    {
                        if (currentClassFunction != null)
                        {
                            func = currentClassFunction;
                        }
                        else
                        {
                            errorReporter.ReportError(context, "No function found with identifier " + functionName, tokens[0], streams[streamIndex]);
                            return null;
                        }
                    }
                    else
                        func = ourClass.Functions[functionName];

                    List<object?> argValues = new();

                    foreach (var arg in context.classAccess().funcCall().expression())
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
                        currentScope = functionName;
                        streamIndex++;
                        Visit(func.BlockContext);
                        streamIndex--;

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
                                errorReporter.ReportError(context, $"Function \"{functionName}\" must return a value", tokens[tokens.Count - 1], streams[streamIndex]);
                            return func.ReturnValue;
                        }
                    }

                    currentClass = null;
                    currentFunction = tempFunc;
                    return null;

                    //if (!ourClass.Functions.ContainsKey(context.classAccess().funcCall().IDENTIFIER().GetText()))
                    //    errorReporter.ReportError(context, $"Function \"{context.classAccess().funcCall().IDENTIFIER().GetText()}\" " +
                    //        "does not exist", tokens[2]);

                    //if (ourClass.Functions[context.classAccess().funcCall().IDENTIFIER().GetText()].Abstract)
                    //{
                    //    if (ourClass.Functions[context.funcCall().IDENTIFIER().GetText()].BlockContext == null)
                    //    {
                    //        errorReporter.ReportError(context, $"Cannot call " +
                    //            $"function \"{context.funcCall().IDENTIFIER().GetText()}\"" +
                    //            $" as it is abstract and has not been overridden yet.", tokens[2]);
                    //    }
                    //}

                    //if (ourClass.Functions[context.classAccess().funcCall().IDENTIFIER().GetText()].BlockContext != null)
                    //{
                    //    currentClassFunction = ourClass.Functions[context.classAccess().funcCall().IDENTIFIER().GetText()];
                    //    object? returnValue = Visit(context.classAccess().funcCall());
                    //    currentClassFunction = null;
                    //    return returnValue;
                    //}
                    //else
                    //{
                    //    classIn = ourClass.Name;
                    //    currentClassFunction = ourClass.Functions[context.classAccess().funcCall().IDENTIFIER().GetText()];
                    //    object? returnValue = Visit(context.classAccess().funcCall());
                    //    currentClassFunction = null;
                    //    classIn = string.Empty;
                    //    return returnValue;
                    //}
                }
                else if (context.classAccess().IDENTIFIER(1) != null)
                {
                    currentClass = null;
                    return ourClass.Variables[context.classAccess().IDENTIFIER(1).GetText()].Value;
                }
                else if (context.classAccess().varReassignment() != null)
                {
                    ourClass.Variables[context.IDENTIFIER(1).GetText()].Value = Visit(context.varReassignment().expression());
                    currentClass = null;
                    return null;
                }
                else if (context.classAccess().varAssignment() != null)
                {
                    ourClass.Variables[context.IDENTIFIER(1).GetText()].Value = Visit(context.varReassignment().expression());
                    currentClass = null;
                    return null;
                }
                else if (context.classAccess().arrayCall() != null)
                {
                    string arrayName = context.arrayCall().IDENTIFIER().GetText();
                    if (!ourClass.Arrays.ContainsKey(arrayName))
                        errorReporter.ReportError(context.arrayCall(), $"{arrayName} does not exist",
                            tokens[0], streams[streamIndex]);

                    int index = Convert.ToInt32(context.arrayCall().INT().GetText());

                    Array array = ourClass.Arrays[arrayName];
                    currentClass = null;
                    return array.Values[index].Value;
                }
                else if (context.classAccess().arraySet() != null)
                {
                    string arrayName = context.arraySet().IDENTIFIER().GetText();
                    if (!ourClass.Arrays.ContainsKey(arrayName))
                        errorReporter.ReportError(context.arraySet(), $"{arrayName} does not exist", tokens[0], streams[streamIndex]);

                    int index = Convert.ToInt32(context.arraySet().INT().GetText());

                    Array array = ourClass.Arrays[arrayName];
                    if (array.Size <= index)
                        errorReporter.ReportError(context.arraySet(), "Index was outside the bounds of the array", tokens[2], streams[streamIndex]);

                    array.Values[index].Value = Visit(context.arraySet().expression());

                    currentClass = null;
                    return null;
                }

                errorReporter.ReportError(context, "Unexpected token; Expected: funcCall | " +
                    "varReassignment | arrayCall | arraySet | varAssignment | IDENTIFIER", tokens[1], streams[streamIndex]);
                return null;
            }
            else if (context.ppMM() != null)    
            {
                var op = context.ppMM().topLevelAddOp().GetText();

                if (!file.Variables.ContainsKey(context.ppMM().IDENTIFIER().GetText()))
                    errorReporter.ReportError(context, $"Variable " +
                        $"\"{context.ppMM().IDENTIFIER().GetText()}\" " +
                        $"does not exist", tokens[0], streams[streamIndex]);

                Variable var = file.Variables[context.ppMM().IDENTIFIER().GetText()];

                if (var.Type != Types.INT)
                    errorReporter.ReportError(context, $"Variables \"{var.Name}\" is not of " +
                        $"type \"INTEGER\"", tokens[0], streams[streamIndex]);

                return op switch
                {
                    "++" => var.Value = Increment(var.Value),
                    "--" => var.Value = Decrement(var.Value),
                    _ => Types.NULL
                };
            }
            else if (context.arraySet() != null)
            {
                if (!file.Arrays.ContainsKey(context.arraySet().IDENTIFIER().GetText()))
                    errorReporter.ReportError(context, $"File \"{fileName}\" " +
                        $"does not contain array \"{context.IDENTIFIER(1).GetText()}\"", tokens[2], streams[streamIndex]);

                string name = context.arraySet().IDENTIFIER().GetText();
                if (!file.Arrays.ContainsKey(name))
                    errorReporter.ReportError(context, $"{name} does not exist", tokens[0], streams[streamIndex]);

                int index = Convert.ToInt32(context.arraySet().INT().GetText());

                Array array = file.Arrays[name];
                if (array.Size <= index)
                    errorReporter.ReportError(context, "Index was outside the bounds of the array", tokens[2], streams[streamIndex]);

                array.Values[index].Value = Visit(context.arraySet().expression());

                return null;
            }
            else if (context.arrayCall() != null)
            {
                string name = context.arrayCall().IDENTIFIER().GetText();
                if (!file.Arrays.ContainsKey(name))
                    errorReporter.ReportError(context, $"{name} does not exist",
                        tokens[0], streams[streamIndex]);

                int index = Convert.ToInt32(context.arrayCall().INT().GetText());

                Array array = file.Arrays[name];
                return array.Values[index].Value;
            }
            else if (context.funcCall() != null)
            {
                string name = context.funcCall().IDENTIFIER().GetText();

                Function func;

                if (!file.Functions.ContainsKey(name))
                {
                    if (currentClassFunction != null)
                    {
                        func = currentClassFunction;
                    }
                    else
                    {
                        errorReporter.ReportError(context, "No function found with identifier " + name, tokens[0], streams[streamIndex]);
                        return null;
                    }
                }
                else
                    func = file.Functions[name];

                List<object?> argValues = new();

                foreach (var arg in context.funcCall().expression())
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
                            errorReporter.ReportError(context, $"Function \"{name}\" must return a value", tokens[tokens.Count - 1], streams[streamIndex]);
                        return func.ReturnValue;
                    }
                }

                currentFunction = tempFunc;
                return null;
            }
            
            errorReporter.ReportError(context, "Couldfnewfwf", tokens[4354543], streams[streamIndex]);
            throw new NotImplementedException();
        }

        #endregion

        #region Arrays

        public override object? VisitArrayDefinition
            ([NotNull] HolyJavaParser.ArrayDefinitionContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string name = context.varParameter().IDENTIFIER().GetText();
            Types type = context.varParameter().varType().GetText() switch
            {
                "int" => Types.INT,
                "float" => Types.FLOAT,
                "string" => Types.STRING,
                "bool" => Types.BOOL,
                "object" => Types.OBJECT,
                _ => Types.NULL,
            };

            if (type == Types.NULL)
                errorReporter.ReportError(context, $"Invalid type",
                    tokens[0], streams[streamIndex]);

            if (Arrays.ContainsKey(name))
            {
                if (Arrays[name].File != string.Empty)
                {
                    if (IncludedFiles[Arrays[name].File].skip)
                    {
                        Arrays.Remove(name);
                    }
                    else if (IncludedFiles[Arrays[name].File].over)
                    {
                        return null;
                    }
                    else
                        errorReporter.ReportError(context, $"Importing file \"{Arrays[name].File}\" " +
                            $"conflicts with existing arrays. If you wish to override " +
                            $"your existing implementations, use \"!\". " +
                            $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                }
                else
                    errorReporter.ReportError(context, $"{name} already exists",
                        tokens[1], streams[streamIndex]);
            }

            int size = Convert.ToInt32(context.INT().GetText());
            Array array = new(name, type, size);

            for (int i = 0; i < size; i++)
            {
                array.Values.Add(i, new Variable(i.ToString(), Types.INT, new Scope()));
            }

            Arrays.Add(name, array);

            return null;
        }

        public override object? VisitArrayCall([NotNull] HolyJavaParser.ArrayCallContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string name = context.IDENTIFIER().GetText();
            if (!Arrays.ContainsKey(name))
                errorReporter.ReportError(context, $"{name} does not exist",
                    tokens[0], streams[streamIndex]);

            int index = Convert.ToInt32(context.INT().GetText());

            Array array = Arrays[name];
            return array.Values[index].Value;
        }

        public override object? VisitArraySet([NotNull] HolyJavaParser.ArraySetContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string name = context.IDENTIFIER().GetText();
            if (!Arrays.ContainsKey(name))
                    errorReporter.ReportError(context, $"{name} does not exist", tokens[0], streams[streamIndex]);

            int index = Convert.ToInt32(context.INT().GetText());

            Array array = Arrays[name];
            if (array.Size <= index)
                errorReporter.ReportError(context, "Index was outside the bounds of the array", tokens[2], streams[streamIndex]);

            array.Values[index].Value = Visit(context.expression());

            return null;
        }

        #endregion

        // Edit Class Var Def to add built-in constructors

        #region Classes

        public override object? VisitClassDef([NotNull] HolyJavaParser.ClassDefContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string name = context.IDENTIFIER(0).GetText();
            bool @abstract = context.abstractKeyword() != null;
            bool @static = context.staticKeyword() != null;
            Class newClass = new(name, @abstract, @static);
            
            if (Classes.ContainsKey(name))
            {
                if (Classes[name].File != string.Empty)
                {
                    if (IncludedFiles[Classes[name].File].skip)
                    {
                        Classes.Remove(name);
                    }
                    else if (IncludedFiles[Classes[name].File].over)
                    {
                        return null;
                    }
                    else
                        errorReporter.ReportError(context, $"Importing file \"{Classes[name].File}\" " +
                            $"conflicts with existing classes. If you wish to override " +
                            $"your existing implementations, use \"!\". " +
                            $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                }
                else
                    errorReporter.ReportError(context, $"Class \"{name}\" has already been defined", 
                        tokens[1], streams[streamIndex]);
            }

            if (context.IDENTIFIER(1) != null)
            {
                if (!Classes.ContainsKey(context.IDENTIFIER(1).GetText()))
                    errorReporter.ReportError(context, $"{context.IDENTIFIER(1).GetText()} does not exist.",
                        tokens[2], streams[streamIndex]);
                Class extendedClass = Classes[context.IDENTIFIER(1).GetText()];

                if (!extendedClass.Abstract)
                    errorReporter.ReportError(context, $"Cannot extend a class that is marked as abstract",
                        tokens[0], streams[streamIndex]);

                foreach (Variable var in extendedClass.Variables.Values)
                {
                    newClass.Variables.Add(var.Name, var);
                }

                foreach (Function func in extendedClass.Functions.Values)
                {
                    newClass.Functions.Add(func.Name, func);
                }
            }

            if (context.constructor() != null)
            {
                string cName = context.constructor().IDENTIFIER().GetText();
                if (cName != name)
                    errorReporter.ReportError(context, $"Constructor Name does not match class name",
                        tokens[0], streams[streamIndex]);
                if (@static)
                    errorReporter.ReportError(context, $"Class \"{name}\" cannot have a constructor as it is marked static",
                        tokens[0], streams[streamIndex]);

                Dictionary<string, Variable> args = new();

                foreach (var arg in context.constructor().varDeclaration())
                {
                    string varName = arg.varParameter().IDENTIFIER().GetText();

                    string typeText = arg.varParameter().varType().GetText();

                    Types varType = typeText switch
                    {
                        "int" => Types.INT,
                        "float" => Types.FLOAT,
                        "string" => Types.STRING,
                        "bool" => Types.BOOL,
                        _ => Types.NULL
                    };

                    args.Add(varName, new(varName, varType, new Scope(currentScope, false)));
                }

                Function constructor = new (cName, Types.NULL, args, 
                    context.constructor().block(), false, false);
                newClass.Constructor = constructor;
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

                    if (!@abstract && funcAbstract)
                        errorReporter.ReportError(context, $"Function {name} cannot be abstract as its class is not abstract", tokens[0], streams[streamIndex]);

                    if (@virtual && funcAbstract)
                        errorReporter.ReportError(context, $"Function {name} cannot be both abstract and virtual",
                            tokens[2], streams[streamIndex]);

                    if ((funcAbstract || @virtual) && @override)
                        errorReporter.ReportError(context, $"Function {name} cannot overide another function because it is marked as either abstract or virtual",
                            tokens[0], streams[streamIndex]);

                    if (funcAbstract)
                    {
                        if (statement.funcDefinition().block() != null)
                            errorReporter.ReportError(context, $"Function {name} cannot have a body because it is marked as abstract",
                                tokens[1], streams[streamIndex]);
                    }
                    else
                    {
                        if (statement.funcDefinition().block() == null)
                            errorReporter.ReportError(context, $"Function {name} must have a body as it is not marked as abstract",
                                tokens[1], streams[streamIndex]);
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
                            _ => Types.NULL
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
                            _ => Types.NULL
                        };

                        args.Add(varName, new(varName, varType, new Scope(currentScope, false)));
                    }

                    if (@override)
                    {
                        if (newClass.Functions.ContainsKey(funcName))
                        {
                            Function function = newClass.Functions[funcName];
                            if (function.Abstract || function.Virtual)
                            {
                                // Verify that the Functions match
                                if (returnType != function.ReturnType)
                                    errorReporter.ReportError(context, $"Function override does not match" +
                                        " abstract or virtual method", tokens[0], streams[streamIndex]);

                                if (args.Count != function.Arguments.Count)
                                    errorReporter.ReportError(context, $"Function override does not match" +
                                        " abstract or virtual method", tokens[0], streams[streamIndex]);

                                foreach (Variable var in args.Values)
                                {
                                    if (!function.Arguments.ContainsKey(var.Name))
                                        errorReporter.ReportError(context, $"Function override does not match" +
                                            " abstract or virtual method", tokens[0], streams[streamIndex]);

                                    Variable funcVar = function.Arguments[var.Name];

                                    if (funcVar.Type != var.Type)
                                        errorReporter.ReportError(context, $"Function override does not match" +
                                            " abstract or virtual method", tokens[0], streams[streamIndex]);
                                }

                                function.BlockContext = statement.funcDefinition().block();
                            }
                            else
                                errorReporter.ReportError(context, $"Cannot override a method that is not marked as either" +
                                    " abstract or virtual", tokens[0], streams[streamIndex]);
                        }
                        else
                            errorReporter.ReportError(context, $"Cannot override a method that is not in the current" +
                                " context", tokens[0], streams[streamIndex]);
                    }
                    else
                    {
                        if (newClass.Functions.ContainsKey(funcName))
                            errorReporter.ReportError(context, $"Function {name} already exists",
                                tokens[3], streams[streamIndex]);

                        Function func = new(funcName, returnType, args,
                            statement.funcDefinition().block(), @virtual, funcAbstract);
                        newClass.Functions.Add(funcName, func);
                    }
                }
                else if (statement.varKeywords().varAssignment() != null)
                {
                    string varName = statement.varKeywords()
                        .varAssignment().varParameter().IDENTIFIER().GetText();

                    if (newClass.Variables.ContainsKey(varName))
                        errorReporter.ReportError(context, $"Variable {name} already exists",
                            tokens[1], streams[streamIndex]);

                    object? value = Visit(statement.varKeywords().varAssignment().expression());

                    string typeText = statement.varKeywords().
                        varAssignment().varParameter().varType().GetText();

                    Types type = typeText switch
                    {
                        "int" => Types.INT,
                        "float" => Types.FLOAT,
                        "string" => Types.STRING,
                        "bool" => Types.BOOL,
                        "object" => Types.OBJECT,
                        _ => Types.NULL
                    };

                    newClass.Variables.Add(varName, new(varName, type, new Scope(), value));
                }
                else if (statement.varKeywords().varDeclaration() != null)
                {
                    string varName = statement.varKeywords().varDeclaration().varParameter().IDENTIFIER().GetText();

                    if (newClass.Variables.ContainsKey(varName))
                        errorReporter.ReportError(context, $"Variable {name} already exists", tokens[1], streams[streamIndex]);

                    string typeText = statement.varKeywords().varDeclaration().varParameter().varType().GetText();

                    Types type = typeText switch
                    {
                        "int" => Types.INT,
                        "float" => Types.FLOAT,
                        "string" => Types.STRING,
                        "bool" => Types.BOOL,
                        "object" => Types.OBJECT,
                        _ => Types.NULL
                    };

                    newClass.Variables.Add(varName, new(varName, type, new Scope()));
                }
                else if (statement.arrayKeywords().arrayDefinition() != null)
                {
                    string arrayName = statement.arrayKeywords().arrayDefinition().varParameter().IDENTIFIER().GetText();
                    Types type = statement.arrayKeywords().arrayDefinition().varParameter().varType().GetText() switch
                    {
                        "int" => Types.INT,
                        "float" => Types.FLOAT,
                        "string" => Types.STRING,
                        "bool" => Types.BOOL,
                        "object" => Types.OBJECT,
                        _ => Types.NULL,
                    };

                    if (type == Types.NULL)
                        errorReporter.ReportError(statement.arrayKeywords().arrayDefinition(), $"Invalid type",
                            tokens[0], streams[streamIndex]);

                    if (newClass.Arrays.ContainsKey(arrayName))
                        errorReporter.ReportError(statement.arrayKeywords().arrayDefinition(), $"{arrayName} already exists",
                            tokens[1], streams[streamIndex]);

                    int size = Convert.ToInt32(statement.arrayKeywords().arrayDefinition().INT().GetText());
                    Array array = new(arrayName, type, size);

                    for (int i = 0; i < size; i++)
                    {
                        array.Values.Add(i, new Variable(i.ToString(), Types.INT, new Scope()));
                    }

                    newClass.Arrays.Add(arrayName, array);
                }
                else if (statement.arrayKeywords().arraySet() != null)
                {
                    string arrayName = statement.arrayKeywords().arraySet().IDENTIFIER().GetText();
                    if (!newClass.Arrays.ContainsKey(arrayName))
                        errorReporter.ReportError(statement.arrayKeywords().arraySet(), $"{arrayName} does not exist", tokens[0], streams[streamIndex]);

                    int index = Convert.ToInt32(statement.arrayKeywords().arraySet().INT().GetText());

                    Array array = newClass.Arrays[arrayName];
                    if (array.Size <= index)
                        errorReporter.ReportError(statement.arrayKeywords().arraySet(), "Index was outside the bounds of the array", tokens[2], streams[streamIndex]);

                    array.Values[index].Value = Visit(statement.arrayKeywords().arraySet().expression());
                }
            }

            Classes.Add(name, newClass);
            return null;
        }

        public override object? VisitClassVar([NotNull] HolyJavaParser.ClassVarContext context) 
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            if (context.includeCall(0) != null)
            {
                string className = context.includeCall(0).IDENTIFIER(1).GetText();
                string name = context.IDENTIFIER(0).GetText();
                string fileName = context.includeCall(0).IDENTIFIER(0).GetText();

                if (!Files.ContainsKey(fileName))
                    errorReporter.ReportError(context, $"File \"{fileName}\" has not been included in this file",
                        tokens[0], streams[streamIndex]);

                if (!Files[fileName].Classes.ContainsKey(className))
                    errorReporter.ReportError(context, $"Class '{name}' does not exist in file '{fileName}'",
                        tokens[0], streams[streamIndex]);

                Class ourClass = Files[fileName].Classes[className]; 

                if (ClassVars.ContainsKey(name))
                {
                    if (ClassVars[name].File != string.Empty)
                    {
                        if (IncludedFiles[ClassVars[name].File].skip)
                        {
                            ClassVars.Remove(name);
                        }
                        else if (IncludedFiles[ClassVars[name].File].over)
                        {
                            return null;
                        }
                    }
                    else
                        errorReporter.ReportError(context, $"Class Variable '{name}' already exist",
                            tokens[1], streams[streamIndex]);
                }

                if (ourClass.Abstract)
                    errorReporter.ReportError(context, $"Cannot make an instance out of an abstract class",
                        tokens[0], streams[streamIndex]);

                if (context.includeCall(1) != null || context.IDENTIFIER(2) != null)
                {
                    if (Classes[className].Constructor == null)
                        errorReporter.ReportError(context, $"Class \"{className}\" does not contain a constructor, therefore, " +
                            $"it cannot be instantiated using one", tokens[1], streams[streamIndex]);

                    Function func = Classes[className].Constructor;

                    // Call it
                    List<object?> argValues = new();

                    foreach (var arg in context.expression())
                    {
                        if (className != "Thread")
                            argValues.Add(Visit(arg));
                        else
                            argValues.Add(arg.GetText());
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
                        foreach (Variable variable in Variables.Values)
                        {
                            if (variable.Scope.master == currentScope)
                                variable.Scope = new Scope(currentScope, true);
                        }

                        string temp = currentScope;
                        currentScope = name;
                        Visit(func.BlockContext);

                        foreach (Variable variable in Variables.Values)
                        {
                            if (variable.Scope.master == currentScope)
                                variable.Scope = new Scope(currentScope, true);

                            if (variable.Scope.master == temp)
                                variable.Scope = new Scope(temp, false);
                        }

                        currentScope = temp;
                    }
                    else
                    {
                        if (className == "Thread")
                        {
                            string value = func.Arguments["function"].Value.ToString();
                            func.Arguments["function"].Value = value;
                        }
                    }

                    currentFunction = tempFunc;
                }
                else
                {
                    if (Classes[className].Constructor != null)
                        errorReporter.ReportError(context, $"Class \"{className}\" contains a constructor, therefore, " +
                            $"it must be instantiated using it", tokens[1], streams[streamIndex]);
                }

                ClassVar var = new(name, ourClass);
                ClassVars.Add(name, var);

                return var.ClassRef;
            }
            else
            {
                string className = context.IDENTIFIER(0).GetText();
                string name = context.IDENTIFIER(1).GetText();

                if (ClassVars.ContainsKey(name))
                {
                    if (ClassVars[name].File != string.Empty)
                    {
                        if (IncludedFiles[ClassVars[name].File].skip)
                        {
                            ClassVars.Remove(name);
                        }
                        else if (IncludedFiles[ClassVars[name].File].over)
                        {
                            return null;
                        }
                    }
                    else
                        errorReporter.ReportError(context, $"Class Variable '{name}' already exist",
                            tokens[1], streams[streamIndex]);
                }

                if (!Classes.ContainsKey(className))
                    errorReporter.ReportError(context, $"Class '{name}' does not exists",
                        tokens[0], streams[streamIndex]);

                if (Classes[className].Abstract)
                    errorReporter.ReportError(context, $"Cannot make an instance out of an abstract class",
                        tokens[0], streams[streamIndex]);

                if (context.includeCall(1) != null || context.IDENTIFIER(2) != null)
                {
                    if (Classes[className].Constructor == null)
                        errorReporter.ReportError(context, $"Class \"{className}\" does not contain a constructor, therefore, " +
                            $"it cannot be instantiated using one", tokens[1], streams[streamIndex]);

                    Function func = Classes[className].Constructor;

                    // Call it
                    List<object?> argValues = new();

                    foreach (var arg in context.expression())
                    {
                        if (className != "Thread")
                            argValues.Add(Visit(arg));
                        else
                            argValues.Add(arg.GetText());
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
                        foreach (Variable variable in Variables.Values)
                        {
                            if (variable.Scope.master == currentScope)
                                variable.Scope = new Scope(currentScope, true);
                        }

                        string temp = currentScope;
                        currentScope = name;
                        Visit(func.BlockContext);

                        foreach (Variable variable in Variables.Values)
                        {
                            if (variable.Scope.master == currentScope)
                                variable.Scope = new Scope(currentScope, true);

                            if (variable.Scope.master == temp)
                                variable.Scope = new Scope(temp, false);
                        }

                        currentScope = temp;
                    }
                    else
                    {
                        if (className == "Thread")
                        {
                            string value = func.Arguments["function"].Value.ToString();
                            func.Arguments["function"].Value = value;
                        }
                    }

                    currentFunction = tempFunc;
                }
                else
                {
                    if (Classes[className].Constructor != null)
                        errorReporter.ReportError(context, $"Class \"{className}\" contains a constructor, therefore, " +
                            $"it must be instantiated using it", tokens[1], streams[streamIndex]);
                }

                ClassVar var = new(name, Classes[className]);
                ClassVars.Add(name, var);

                return var.ClassRef;
            }
        }

        public override object? VisitClassVarReassignment
            ([NotNull] HolyJavaParser.ClassVarReassignmentContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string className = context.IDENTIFIER(1).GetText();
            string name = context.IDENTIFIER(0).GetText();

            if (!Classes.ContainsKey(className))
                errorReporter.ReportError(context, $"{name} does not exists", tokens[1], streams[streamIndex]);

            if (!ClassVars.ContainsKey(name))
                errorReporter.ReportError(context, $"{name} does not exists", tokens[1], streams[streamIndex]);

            ClassVars[name].ClassRef = Classes[className];

            return null;
        }

        public override object? VisitClassAccess
            ([NotNull] HolyJavaParser.ClassAccessContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string name = context.IDENTIFIER(0).GetText();
            Class? ourClass = null;

            if (!ClassVars.ContainsKey(name))
            {
                if (!Classes.ContainsKey(name))
                    errorReporter.ReportError(context, $"Class variable {name} does not exist",
                        tokens[0], streams[streamIndex]);
                else
                {
                    if (!Classes[name].Static)
                        errorReporter.ReportError(context, $"Class {name} does not exist", tokens[1], streams[streamIndex]);
                    else
                        ourClass = Classes[name];
                }
            }
            else
                ourClass = ClassVars[name].ClassRef;

            if (context.funcCall() != null)
            {
                if (!ourClass.Functions.ContainsKey(context.funcCall().IDENTIFIER().GetText()))
                    errorReporter.ReportError(context, $"Function \"{context.funcCall().IDENTIFIER().GetText()}\" " +
                        "does not exist", tokens[2], streams[streamIndex]);

                if (ourClass.Functions[context.funcCall().IDENTIFIER().GetText()].Abstract)
                {
                    if (ourClass.Functions[context.funcCall().IDENTIFIER().GetText()].BlockContext == null)
                    {
                        errorReporter.ReportError(context, $"Cannot call " +
                            $"function \"{context.funcCall().IDENTIFIER().GetText()}\"" +
                            $" as it is abstract and has not been overridden yet.", tokens[2], streams[streamIndex]);
                    }
                }

                if (ourClass.Functions[context.funcCall().IDENTIFIER().GetText()].BlockContext != null)
                {
                    currentClassFunction = ourClass.Functions[context.funcCall().IDENTIFIER().GetText()];
                    object? returnValue = Visit(context.funcCall());
                    currentClassFunction = null;
                    return returnValue;
                }
                else
                {
                    classIn = ourClass;
                    currentClassFunction = ourClass.Functions[context.funcCall().IDENTIFIER().GetText()];
                    object? returnValue = Visit(context.funcCall());
                    currentClassFunction = null;
                    classIn = null;
                    return returnValue;
                }
            }
            else if (context.IDENTIFIER(1) != null)
            {
                if (!ourClass.Variables.ContainsKey(context.IDENTIFIER(1).GetText()))
                    errorReporter.ReportError(context, $"No variable with identifier " +
                        $"\"{context.IDENTIFIER(1).GetText()}\" could be found",
                        tokens[2], streams[streamIndex]);
                return ourClass.Variables[context.IDENTIFIER(1).GetText()].Value;
            }
            else if (context.varReassignment() != null)
            {
                ourClass.Variables[context.IDENTIFIER(1).GetText()].Value = Visit(context.varReassignment().expression());
                return null;
            }
            else if (context.varAssignment() != null)
            {
                ourClass.Variables[context.IDENTIFIER(1).GetText()].Value = Visit(context.varReassignment().expression());
                return null;
            }
            else if (context.arrayCall() != null)
            {
                string arrayName = context.arrayCall().IDENTIFIER().GetText();
                if (!ourClass.Arrays.ContainsKey(arrayName))
                    errorReporter.ReportError(context.arrayCall(), $"{arrayName} does not exist",
                        tokens[0], streams[streamIndex]);

                int index = Convert.ToInt32(context.arrayCall().INT().GetText());

                Array array = ourClass.Arrays[arrayName];
                return array.Values[index].Value;
            }
            else if (context.arraySet() != null)
            {
                string arrayName = context.arraySet().IDENTIFIER().GetText();
                if (!ourClass.Arrays.ContainsKey(arrayName))
                    errorReporter.ReportError(context.arraySet(), $"{arrayName} does not exist", tokens[0], streams[streamIndex]);

                int index = Convert.ToInt32(context.arraySet().INT().GetText());

                Array array = ourClass.Arrays[arrayName];
                if (array.Size <= index)
                    errorReporter.ReportError(context.arraySet(), "Index was outside the bounds of the array", tokens[2], streams[streamIndex]);

                array.Values[index].Value = Visit(context.arraySet().expression());

                return null;
            }

            errorReporter.ReportError(context, "Unexpected token; Expected: funcCall | " +
                "varReassignment | arrayCall | arraySet | varAssignment | IDENTIFIER", tokens[1], streams[streamIndex]);
            return null;
        }

        #endregion

        #region Logic

        public override object? VisitIfLogic([NotNull] HolyJavaParser.IfLogicContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            if (Visit(context.expression()) == null)
            {
                errorReporter.ReportError(context, "Invalid syntax.", tokens[1], streams[streamIndex]);
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
                        errorReporter.ReportError(context, "Invalid syntax.", tokens[1], streams[streamIndex]);
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
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            if (currentFunction == null)
                errorReporter.ReportError(context, "Keyword \"return\" cannot be used outside of a function.", tokens[0], streams[streamIndex]);

            currentFunction.ReturnValue = Visit(context.expression());
            return null;
        }
        #endregion

        #region Variables

        public override object? VisitVarDeclaration
            ([NotNull] HolyJavaParser.VarDeclarationContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string name = context.varParameter().IDENTIFIER().GetText();

            if (Variables.ContainsKey(name))
            {
                if (Variables[name].File != string.Empty)
                {
                    if (IncludedFiles[Variables[name].File].skip)
                    {
                        Variables.Remove(name);
                    }
                    else if (IncludedFiles[Variables[name].File].over)
                    {
                        return null;
                    }
                    else
                        errorReporter.ReportError(context, $"Importing file \"{Variables[name].File}\" " +
                            $"conflicts with existing variables. If you wish to override " +
                            $"your existing implementations, use \"!\". " +
                            $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                }
                else if (!Variables[name].Scope.dead)
                    errorReporter.ReportError(context, $"Variable \"{name}\" already exists.", tokens[1], streams[streamIndex]);
            }

            string typeText = context.varParameter().varType().GetText();

            Types type = typeText switch
            {
                "int" => Types.INT,
                "float" => Types.FLOAT,
                "string" => Types.STRING,
                "bool" => Types.BOOL,
                "object" => Types.OBJECT,
                _ => Types.NULL
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

            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            if (Variables.ContainsKey(name))
            {
                if (!Variables[name].Scope.dead)
                    errorReporter.ReportError(context, $"Variable \"{name}\" already exists.", 
                        tokens[1], streams[streamIndex]);
            }

            object? value = Visit(context.expression());

            string typeText = context.varParameter().varType().GetText();

            Types type = typeText switch
            {
                "int" => Types.INT,
                "float" => Types.FLOAT,
                "string" => Types.STRING,
                "bool" => Types.BOOL,
                "object" => Types.OBJECT,
                _ => Types.NULL
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

            if (left is string lStringD && right is double rDouble)
                return $"{lStringD}{rDouble}";

            if (left is double lDouble && right is string rStringD)
                return $"{lDouble}{rStringD}";

            throw new Exception($"Cannot add the values of types {left.GetType()} and {right.GetType()}");
        }

        public static object Increment(object left)
        {
            if (left is int l)
            {
                l++;
                return l++;
            }

            throw new Exception("Cannot increment non-integer types");
        }

        public static object Decrement(object left)
        {
            if (left is int l)
            {
                l--;
                return l--;
            }

            throw new Exception("Cannot decrement non-integer types");
        }

        public override object VisitComparisonExpression
            ([NotNull] HolyJavaParser.ComparisonExpressionContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));

            var op = context.comparisonOps().GetText();

            if (left == null)
                errorReporter.ReportError(context, "Invalid math operation", tokens[0], streams[streamIndex]);

            if (right == null)
                errorReporter.ReportError(context, "Invalid math operation", tokens[2]  , streams[streamIndex]);

            return op switch
            {

                "==" => EE_Compare(left, right),
                "!=" => NE_Compare(left, right),
                "<" => L_Compare(left, right),
                "<=" => LE_Compare(left, right),
                ">" => G_Compare(left, right),
                ">=" => GE_Compare(left, right),
                _ => Types.NULL

            };

        }

        public override object? VisitConstantExpression
            ([NotNull] HolyJavaParser.ConstantExpressionContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

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

            errorReporter.ReportError(context, "Invalid math operation", tokens[0], streams[streamIndex]);
            return null;
        }

        public override object? VisitIdentifierExpression
            ([NotNull] HolyJavaParser.IdentifierExpressionContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

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
                    errorReporter.ReportError(context, $"Variable \"{name}\" cannot be accessed while out of scope", tokens[0], streams[streamIndex]  );
            }

            if (Variables.ContainsKey(name))
            {
                return Variables[name].Value;
            }

            if (currentClass != null)
            {
                if (currentClass.Variables.ContainsKey(name))
                    return currentClass.Variables[name].Value;
            }

            errorReporter.ReportError(context, "No variable found with identifer " + name, tokens[0], streams[streamIndex]);
            return null;
        }

        public override object VisitPpMM([NotNull] HolyJavaParser.PpMMContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);
            var op = context.topLevelAddOp().GetText();

            if (!Variables.ContainsKey(context.IDENTIFIER().GetText()))
                errorReporter.ReportError(context, $"Variable \"{context.IDENTIFIER().GetText()}\" " +
                    $"does not exist", tokens[0], streams[streamIndex]);

            Variable var = Variables[context.IDENTIFIER().GetText()];

            if (var.Type != Types.INT)
                errorReporter.ReportError(context, $"Variables \"{var.Name}\" is not of " +
                    $"type \"INTEGER\"", tokens[0], streams[streamIndex]);

            return op switch
            {
                "++" => var.Value = Increment(var.Value),
                "--" => var.Value = Decrement(var.Value),
                _ => Types.NULL
            };
        }

        public override object VisitAdditiveExpression
            ([NotNull] HolyJavaParser.AdditiveExpressionContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            var left = Visit(context.expression(0));
            var op = context.addOp().GetText();

            var right = Visit(context.expression(1));

            if (left == null)
                errorReporter.ReportError(context, "Invalid math operation", tokens[0], streams[streamIndex]);

            if (right == null)
                errorReporter.ReportError(context, "Invalid math operation", tokens[2], streams[streamIndex]);

            return op switch
            {

                "++" => Increment(left),
                "--" => Decrement(left),
                "+" => Add(left, right),
                "-" => Sub(left, right),
                _ => Types.NULL

            };

        }

        public override object VisitMultiplicativeExpression
            ([NotNull] HolyJavaParser.MultiplicativeExpressionContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));

            var op = context.multOp().GetText();

            if (left == null)
                errorReporter.ReportError(context, "Invalid math operation", tokens[0], streams[streamIndex]);

            if (right == null)
                errorReporter.ReportError(context, "Invalid math operation", tokens[2], streams[streamIndex]);

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
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, 
                context.Stop.TokenIndex);

            string name = context.IDENTIFIER().GetText();
            Types type = Types.NULL;

            bool @abstract = context.abstractKeyword() != null;
            bool @virtual = context.virtualKeyword() != null;
            bool @override = context.overrideKeyword() != null;

            if (@override)
                errorReporter.ReportError(context, $"Function \"{name}\" " +
                    "cannot override any function because it is not contained within " +
                    "a class", tokens[0], streams[streamIndex]);

            if (@virtual || @abstract)
                errorReporter.ReportError(context, $"Function \"{name}\" " +
                    "cannot be either abstract or virtual because it is not contained within " +
                    "an abstract class", tokens[1], streams[streamIndex]);

            if (Functions.ContainsKey(name))
            {
                if (Functions[name].File != string.Empty)
                {
                    if (IncludedFiles[Functions[name].File].skip)
                    {
                        Functions.Remove(name);
                    }
                    else if (IncludedFiles[Functions[name].File].over)
                    {
                        return null;
                    }
                    else
                        errorReporter.ReportError(context, $"Importing file {Functions[name].File} " +
                            $"conflicts with existing functions. If you wish to override " +
                            $"your existing implementations, use \"!\". " +
                            $"To skip them, use \"#\"", tokens[1], streams[streamIndex]);
                }
                else
                    errorReporter.ReportError(context, $"\"{name}\" is already defined", 
                        tokens[3], streams[streamIndex]);
            }

            if (context.varType() != null)
            {
                string typeText = context.varType().GetText();
                type = typeText switch
                {
                    "int" => Types.INT,
                    "float" => Types.FLOAT,
                    "string" => Types.STRING,
                    "bool" => Types.BOOL,
                    _ => Types.NULL
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
                    _ => Types.NULL
                };

                args.Add(varName, new(varName, varType, new Scope(currentScope, false)));
            }

            Function func = new(name, type, args, context.block(), @virtual, @abstract);
            Functions.Add(name, func);

            return null;
        }

        public override object? VisitFuncCall([NotNull] HolyJavaParser.FuncCallContext context)
        {
            IList<IToken> tokens =
                streams[streamIndex].GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            string name = context.IDENTIFIER().GetText();

            Function func;

            if (!Functions.ContainsKey(name))
            {
                if (currentClassFunction != null)
                {
                    func = currentClassFunction;
                }
                else
                {
                    errorReporter.ReportError(context, "No function found with identifier " + name, tokens[0], streams[streamIndex]);
                    return null;
                }
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
                        errorReporter.ReportError(context, $"Function \"{name}\" must return a value", tokens[tokens.Count-1], streams[streamIndex]);
                    return func.ReturnValue;
                }
            }
            else
            {
                if (name == "Printf")
                {
                    Variable var = func.Arguments["msg"];
                    if (var.Value == null)
                        errorReporter.ReportError(context, "Argument cannot be null", tokens[2], streams[streamIndex]);
                    Printf(var.Value);
                }

                if (classIn != null)
                {
                    if (classIn.Name == "Math")
                    {
                        if (name == "Power")
                        {
                            Variable value = func.Arguments["value"];
                            Variable power = func.Arguments["power"];

                            if (value.Value == null || power.Value == null)
                                errorReporter.ReportError(context, "Argument cannot be null", tokens[2], streams[streamIndex]);
                            return Math.Pow(Convert.ToDouble(value.Value), Convert.ToDouble(power.Value));
                        }
                    }
                    else if (classIn.Name == "Thread")
                    {
                        if (name == "Start")
                        {
                            Class our = classIn;
                            void t()
                            {
                                Visit(Functions[our.Constructor.Arguments["function"].Value.ToString()].BlockContext);
                            }
                            Thread th = new(t);
                            classIn.Thread = th;
                            th.Start();
                        }
                    }
                }
            }

            currentFunction = tempFunc;
            return null;
        }

        #endregion
    }
}