using System.CodeDom.Compiler;

namespace Lokad.CodeDsl
{
    public interface IGenerator
    {
        void Generate(Context context, IndentedTextWriter outer);
    }
}