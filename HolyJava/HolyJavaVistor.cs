using Antlr4.Runtime.Misc;
using HolyJava.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HolyJava
{
    internal class Variable
    {
        public string Name { get; set; }
        public object? Value { get; set; }

        public Variable(string name, object? value = null)
        {
            Name = name; Value = value;
        }
    }

    internal class HolyJavaVistor : HolyJavaBaseVisitor<object?>
    {
        private readonly Dictionary<string, Variable> Variables = new();

        public HolyJavaVistor()
        {
            // Init stuff
        }

        public override object? VisitVarDeclaration
            ([NotNull] HolyJavaParser.VarDeclarationContext context)
        {
            string name = context.varParameter().IDENTIFIER().GetText();
            Variable var = new(name);
            Variables.Add(name, var);

            return null;
        }
    }
}
