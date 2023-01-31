using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Arch.System.SourceGenerator;

[Generator(LanguageNames.CSharp)]
public class QueryGenerator : ISourceGenerator
{
    private static Logger mLogger;
    public void Initialize(GeneratorInitializationContext context)
    {
        mLogger = new Logger();
        context.RegisterForSyntaxNotifications(SyntaxContextReceiver.Create);
        /*if (!Debugger.IsAttached)
        {
            Debugger.Launch();
        }*/
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver || receiver.MethodDeclarations.Count == 0)
        {
            return;
        }

        try
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
            context.AddSource("Attributes.g.cs", CSharpSyntaxTree.ParseText(attributes).GetRoot().NormalizeWhitespace().ToFullString());
            foreach (var methodSyntax in receiver.MethodDeclarations)
            {
                var semanticModel = context.Compilation.GetSemanticModel(methodSyntax.SyntaxTree);
                var methodSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, methodSyntax) as IMethodSymbol;

                bool entity = false;
                if (methodSymbol?.Parameters != null)
                {
                    entity = methodSymbol.Parameters.Any(symbol => symbol.Type.Name.Equals("Entity"));
                }

                var sb = new StringBuilder();
                if (methodSymbol != null)
                {
                    var method = entity ? sb.AppendQueryWithEntity(methodSymbol) : sb.AppendQueryWithoutEntity(methodSymbol);
                    context.AddSource($"{methodSymbol.ToFullString()}.g.cs", CSharpSyntaxTree.ParseText(method.ToString()).GetRoot().NormalizeWhitespace().ToFullString());
                }
            }

            foreach (var classToMethod in receiver.ClassDeclarations)
            {
                var classSymbol = classToMethod.Key;
                var parentSymbol = classSymbol.BaseType;

                if (parentSymbol != null && !parentSymbol.Name.Equals("BaseSystem")) continue;
                if (classSymbol.MemberNames.Contains("Update")) continue;

                var typeSymbol = parentSymbol?.TypeArguments[1];

                var methodCalls = new StringBuilder().CallMethods(classToMethod.Value);

                var template =
                    $$"""
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            {{(!classSymbol.ContainingNamespace.IsGlobalNamespace ? $"namespace {classSymbol.ContainingNamespace} {{" : "")}}
                public partial class {{classSymbol.Name}}{
                        
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public override void Update(in {{typeSymbol?.ToDisplayString()}} data){
                        {{methodCalls}}
                    }
                }
            {{(!classSymbol.ContainingNamespace.IsGlobalNamespace ? "}" : "")}}
            """;
                context.AddSource($"{classSymbol.ToFullString()}.g.cs", CSharpSyntaxTree.ParseText(template).GetRoot().NormalizeWhitespace().ToFullString());
            }
        }
        catch (Exception e)
        {
            mLogger.Log(e.ToString());
        }
        finally
        {
            mLogger.Save("./Debug.txt");
        }
    }
    
    class SyntaxContextReceiver : ISyntaxContextReceiver
    {
        internal static ISyntaxContextReceiver Create()
        {
            return new SyntaxContextReceiver();
        }

        public HashSet<MethodDeclarationSyntax> MethodDeclarations { get; } = new();
        public Dictionary<INamedTypeSymbol,List<IMethodSymbol>> ClassDeclarations { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            var node = context.Node;
            var list = new List<IMethodSymbol>();
            if (node is ClassDeclarationSyntax
                or StructDeclarationSyntax
                or RecordDeclarationSyntax
                or InterfaceDeclarationSyntax)
            {
                var allMethod = node.DescendantNodes().OfType<MethodDeclarationSyntax>();
                list.Clear();
                foreach (var method in allMethod)
                {
                    if (method.AttributeLists.Count > 0)
                    {
                        var attr = method.AttributeLists.SelectMany(x => x.Attributes)
                            .FirstOrDefault(x => x.Name.ToString() is "Query");
                        if (attr != null)
                        {
                            MethodDeclarations.Add(method);
                            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
                            if (methodSymbol != null)
                                list.Add(methodSymbol);
                        }
                    }
                }

                if (list.Count > 0)
                {
                    if(context.SemanticModel.GetDeclaredSymbol(node) is INamedTypeSymbol named)
                        ClassDeclarations.Add(named, list);
                }
            }
        }
    }
}