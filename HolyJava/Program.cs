using Antlr4.Runtime;
using HolyJava;
using HolyJava.Content;

try
{
    string input = File.ReadAllText("Content/Main.hjm");

    AntlrInputStream inputStream = new(input.ToString());
    HolyJavaLexer holyJavaLexer = new(inputStream);
    CommonTokenStream commonTokenStream = new(holyJavaLexer);
    HolyJavaParser holyJavaParser = new(commonTokenStream);
    var context = holyJavaParser.program();
    HolyJavaVistor visitor = new();
    visitor.Visit(context);
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex);
}