using System;
using System.CodeDom.Compiler;

namespace Lokad.CodeDsl
{
    public sealed class CodeWriter
    {
        private readonly IndentedTextWriter _writer;

        public CodeWriter(IndentedTextWriter writer)
        {
            _writer = writer;
        }

        public int Indent { get { return _writer.Indent; } set { _writer.Indent = value; } }
        public void Write(string format, params object[] args)
        {
            var txt = string.Format(format, args);
            var lines = txt.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            

            for (int i = 0; i < lines.Length; i++)
            {
                bool thisIsLast = i == (lines.Length - 1);
                if (thisIsLast)
                    _writer.Write(lines[i]);
                else
                    _writer.WriteLine(lines[i]);

            }
        }

        public void WriteLine()
        {
            _writer.WriteLine();
        }

        public void WriteLine(string format, params object[] args)
        {
            
            var txt = args.Length == 0 ? format : string.Format(format, args);
            var lines = txt.Split(new[] {Environment.NewLine}, StringSplitOptions.None);
            

            foreach (string t in lines)
            {
                _writer.WriteLine(t);
            }
        }
    }
}