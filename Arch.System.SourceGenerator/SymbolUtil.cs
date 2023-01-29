using Microsoft.CodeAnalysis;

namespace Arch.System.SourceGenerator
{
    public static class SymbolUtil
    {
        public static string ToFullString(this ISymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat).Replace("global::", "")
                .Replace("<", "_")
                .Replace(">", "_");
        }
    }
}