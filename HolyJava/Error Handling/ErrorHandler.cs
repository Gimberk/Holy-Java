using Antlr4.Runtime;

namespace HolyJava.Error_Handling
{
    internal class ErrorHandler : IErrorReporter
    {
        public readonly string fileName;
        public readonly IToken first;

        public ErrorHandler(string fileName, IToken first)
        {
            this.fileName = fileName;
            this.first = first;
        }

        public void ReportError(ParserRuleContext context, string error, IToken offendingToken, CommonTokenStream stream)
        {
            IList<IToken> messageParts =
                stream.GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);
            string message = string.Empty;
            bool increment = true;
            int tokensBefore = 0;
            foreach (var item in messageParts)
            {
                if (offendingToken.Text == item.Text)
                    increment = false;
                if(increment)
                    tokensBefore++;
                message += item.Text + " ";
            }

            int previousIndices = messageParts[0].StartIndex - first.StartIndex;

            int line = offendingToken.Line;

            int startIndex = (offendingToken.StartIndex - previousIndices) + tokensBefore;
            int arrowCount = offendingToken.StopIndex - offendingToken.StartIndex + 1;

            // Acount for line number
            startIndex += line.ToString().Length + 2;

            if (message == null)
                Console.WriteLine("Undefined error on line " + line);
            else
            {
                Console.WriteLine($"Error: \"{error}\" encountered on line: {line} of file {fileName}.");
                Console.WriteLine("The error is as follows: ");
                Console.WriteLine("--------------------------------------------------");
                char[] arrows = new char[message.Length + arrowCount];
                int index = 0;
                for (int i = 0; i < arrows.Length; i++)
                {
                    if (i >= startIndex)
                    {
                        arrows[i] = '^';
                        index++;
                        if (index >= arrowCount)
                            break;
                    }
                    else
                        arrows[i] = ' ';
                }
                Console.WriteLine($"{line}. {message}");
                foreach (char character in arrows)
                {
                    Console.Write(character);
                }
                Console.WriteLine("\n--------------------------------------------------");
                Environment.Exit(-1);
            }
        }
    }
}
