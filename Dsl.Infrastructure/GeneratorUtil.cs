namespace Lokad.CodeDsl
{
    public class GeneratorUtil
    {
        public static string ParameterCase(string s)
        {
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        public static string MemberCase(string s)
        {
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}