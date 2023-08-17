using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HolyJava
{
    public class ParserErrors : DefaultErrorStrategy
    {
        public readonly string fileName;

        public ParserErrors(string fileName)
        {
            this.fileName = fileName;
        }

        public override void ReportError(Parser recognizer, RecognitionException e)
        {
            string? message = e.OffendingToken.InputStream.ToString();
            int line = e.OffendingToken.Line;
            int column = e.OffendingToken.Column;
            string token = e.OffendingToken.Text;
            if (message == null)
                Console.WriteLine("Undefined error on line " + line);
            else
            {
                Console.WriteLine($"Unexpected token: {token} on line: {line} of file: {fileName}");
                Console.WriteLine("The error is as follows: ");
                Console.WriteLine("--------------------------------------------------");
                char[] arrows = new char[column + token.Length];
                int index = 0;
                for (int i = 0; i < arrows.Length; i++)
                {
                    if (i >= column)
                    {
                        arrows[i] = '^';
                        index++;
                        if (index > token.Length)
                            break;
                    }
                    else
                        arrows[i] = ' ';
                }
                Console.WriteLine(message);
                foreach (char character in arrows)
                {
                    Console.Write(character);
                }
                Console.WriteLine("\n--------------------------------------------------");
            }
        }

        public override void Recover(Parser recognizer, RecognitionException e)
        {
            Environment.Exit(-1);
        }
    }
}
