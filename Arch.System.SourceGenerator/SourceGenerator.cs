using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Arch.System.SourceGenerator;

[Generator]
public class QueryGenerator : ISourceGenerator
{
    private static Dictionary<ISymbol, List<IMethodSymbol>> _classToMethods { get; set; }

    private static void AddMethodToClass(IMethodSymbol methodSymbol)
    {
        if (!_classToMethods.TryGetValue(methodSymbol.ContainingSymbol, out var list))
        {
            list = new List<IMethodSymbol>();
            _classToMethods[methodSymbol.ContainingSymbol] = list;
        }

        list.Add(methodSymbol);
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        _classToMethods = new(512);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var attributes = $$"""
            namespace Arch.System.SourceGenerator
            {
            #if NET7_0_OR_GREATER
                {{new StringBuilder().AppendGenericAttributes("All", "All", 25)}}
                {{new StringBuilder().AppendGenericAttributes("Any", "Any", 25)}}
                {{new StringBuilder().AppendGenericAttributes("None", "None", 25)}}
                {{new StringBuilder().AppendGenericAttributes("Exclusive", "Exclusive", 25)}}
            #endif
            }
        """;
        context.AddSource("Attributes.g.cs", SourceText.From(attributes, Encoding.UTF8));

        var methodDeclarations = context.Compilation.SyntaxTrees
            .SelectMany(t => t.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
            .Where(m => m.AttributeLists.Count > 0 &&
                        m.AttributeLists.SelectMany(a => a.Attributes)
                            .Any(a => a.Name.ToString() == "QueryAttribute"))
            .Distinct();

        foreach (var methodSyntax in methodDeclarations)
        {
            var semanticModel = context.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
            var methodSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, methodSyntax) as IMethodSymbol;

            AddMethodToClass(methodSymbol);

            var entity = methodSymbol.Parameters.Any(symbol => symbol.Type.Name.Equals("Entity"));
            var sb = new StringBuilder();
            var method = entity ? sb.AppendQueryWithEntity(methodSymbol) : sb.AppendQueryWithoutEntity(methodSymbol);
            context.AddSource($"{methodSymbol.ContainingType.Name}.g.cs", SourceText.From(method.ToString(), Encoding.UTF8));

            foreach (var classToMethod in _classToMethods)
            {

                var classSymbol = classToMethod.Key as INamedTypeSymbol;
                var parentSymbol = classSymbol.BaseType;

                if (!parentSymbol.Name.Equals("BaseSystem")) continue;
                if (classSymbol.MemberNames.Contains("Update")) continue;

                var typeSymbol = parentSymbol.TypeArguments[1];

                var methodCalls = new StringBuilder().CallMethods(classToMethod.Value);
                var template =
                    $$"""
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using {{typeSymbol.ContainingNamespace}};
            namespace {{classSymbol.ContainingNamespace}}{
                public partial class {{classSymbol.Name}}{
                        
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public override void Update(in {{typeSymbol.Name}} {{typeSymbol.Name.ToLower()}}){
                        {{methodCalls}}
                    }
                }
            }
            """;

                context.AddSource($"{classSymbol.Name}.g.cs", CSharpSyntaxTree.ParseText(template).GetRoot().NormalizeWhitespace().ToFullString());
            }
        }
    }
}