using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Dsl.Generators;

namespace Lokad.CodeDsl
{
    public static class GeneratorUtil
    {
        private static readonly List<Type> GeneratorTypeList = typeof (TemplatedGenerator).Assembly.GetTypes().Where(x => x.GetInterfaces().Any(y => y.Name == "IGenerator")).ToList();

        private static readonly Type DefaultGenerator = typeof(TemplatedGenerator);

        public static string Build(string source)
        {
            var builder = new StringBuilder();
            using (var stream = new StringWriter(builder))
            using (var writer = new IndentedTextWriter(stream, "    "))
            {
                var context = GenerateContext(source);
                
                var generatorType = GeneratorTypeList.SingleOrDefault(x => x.Name == context.CurrentGenerator) ?? DefaultGenerator;
                var generator = (IGenerator)Activator.CreateInstance(generatorType);

                generator.Generate(context, writer);
            }
            return builder.ToString();
        }

        public static Context GenerateContext(string source)
        {
            var context = new MessageContractAssembler().From(source);
            return context;
        }


    }
}