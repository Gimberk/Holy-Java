using Antlr4.Runtime;
using HolyJava;
using HolyJava.Content;
using HolyJava.Error_Handling;

try
{
    //List<string> hjmFiles = new List<string>();

    //string[] files =
    //    Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "Application Data"));

    //foreach (string file in files)
    //{
    //    FileInfo info = new FileInfo(file);

    //    if (info.Extension == ".hjm")
    //        hjmFiles.Add(file);
    //}

    //if (hjmFiles.Count > 1 || hjmFiles.Count < 1)
    //    throw new Exception
    //        ("Too many or too little HJM files found in directory. There can only be one.");

    //string text = hjmFiles[0];
    //string input = File.ReadAllText(text);
    //string fileName = new FileInfo(text).Name;

    string fileName = "Main.hjm";
    string input = File.ReadAllText("Content/Main.hjm");

    AntlrInputStream inputStream = new(input.ToString());
    HolyJavaLexer holyJavaLexer = new(inputStream);
    CommonTokenStream commonTokenStream = new(holyJavaLexer);
    ParserErrors errors = new(fileName);
    HolyJavaParser holyJavaParser = new(commonTokenStream)
    {
        ErrorHandler = errors
    };
    var context = holyJavaParser.program();
    IList<IToken> tokens = commonTokenStream.GetTokens();
    ErrorHandler error = new(fileName, tokens[0]);
    HolyJavaVistor visitor = new(error, commonTokenStream);
    visitor.Visit(context);
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex);
}