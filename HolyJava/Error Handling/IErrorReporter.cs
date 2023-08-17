using Antlr4.Runtime;

namespace HolyJava.Error_Handling
{
    public interface IErrorReporter
    {
        void ReportError(ParserRuleContext context, string error, IToken offendingToken, CommonTokenStream stream);
    }
}
