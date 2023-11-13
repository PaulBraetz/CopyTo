namespace RhoMicro.CopyToGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

[Generator]
public class CopyToGenerator : IIncrementalGenerator
{
    public const String AttributeName = "GenerateCopyToAttribute";
    public const String AttributeNamespace = "RhoMicro.CopyToGenerator";
    private const String _attributeSource =
"""
namespace RhoMicro.CopyToGenerator;

[global::System.AttributeUsage(AttributeTargets.Class)]
internal sealed class GenerateCopyToAttribute : global::System.Attribute {}
""";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider.CreateSyntaxProvider(
            (n, t) => n is ClassDeclarationSyntax,
            (c, t) =>
            {
                var symbol = c.SemanticModel.GetDeclaredSymbol(c.Node) as ITypeSymbol;

                var isMarked = symbol.IsMarked() &&
                    (c.Node as ClassDeclarationSyntax).Modifiers.Any(SyntaxKind.PartialKeyword);

                return (IsMarked: isMarked, Symbol: symbol);
            })
            .Where(c => c.IsMarked)
            .Select((c, t) => c.Symbol)
            .Select((symbol, t) =>
            {
                var props = symbol.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => p.DeclaredAccessibility == Accessibility.Public &&
                              p.SetMethod != null &&
                              p.GetMethod != null &&
                              p.SetMethod.DeclaredAccessibility == Accessibility.Public &&
                              p.GetMethod.DeclaredAccessibility == Accessibility.Public);

                var methodSourceBuilder = new StringBuilder()
                    .Append("public void CopyTo(")
                    .Append(symbol.Name).Append(" target){if(this == target){ return;}");

                var methodSource = props.Select(GetCopyStatement)
                    .Aggregate(methodSourceBuilder, (b, a) => b.Append(a))
                    .Append('}')
                    .ToString();

                return (MethodSource: methodSource, Symbol: symbol);
            })
            .Select((c, t) =>
            {
                var classSourceBuilder = new StringBuilder()
                    .AppendLine("#pragma warning disable");

                if(!c.Symbol.ContainingNamespace.IsGlobalNamespace)
                {
                    _ = classSourceBuilder.Append("namespace ")
                        .Append(c.Symbol.ContainingNamespace.ToDisplayString())
                        .Append('{');
                }

                _ = classSourceBuilder
                    .Append("partial class ").AppendLine(c.Symbol.Name)
                    .Append('{')
                    .Append(c.MethodSource)
                    .Append('}');

                if(!c.Symbol.ContainingNamespace.IsGlobalNamespace)
                {
                    _ = classSourceBuilder.Append('}');
                }

                var source = classSourceBuilder.ToString();
                var formattedSource = CSharpSyntaxTree.ParseText(source)
                    .GetRoot()
                    .NormalizeWhitespace()
                    .SyntaxTree
                    .GetText()
                    .ToString();

                return (Source: formattedSource, HintName: c.Symbol.Name);
            });

        context.RegisterSourceOutput(provider, (c, output) => c.AddSource(output.HintName, output.Source));
        context.RegisterPostInitializationOutput(c => c.AddSource(AttributeName, _attributeSource));
    }

    private static String GetCopyStatement(IPropertySymbol p)
    {
        var result = p.Type.TryGetIEnumerableItemType(out var itemType) ?
            $"if(target.{p.Name} == default || {p.Name} == default)" +
            $"{{target.{p.Name} = {p.Name};}} " +
            $"else" +
            $"{{var targetElements = new global::System.Collections.Generic.HashSet<{itemType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(target.{p.Name});" +
            $"var copyPairs = {p.Name}.Select(targetValue => (Success: targetElements.TryGetValue(targetValue, out var thisValue), TargetValue: targetValue, ThisValue: thisValue)).Where(t => t.ThisValue != null);" +
            $"foreach(var (_, thisValue, targetValue) in copyPairs)" +
            $"{{thisValue.CopyTo(targetValue);}}}}" :
            p.Type.IsMarked() ?
            $"if(target.{p.Name} == null || {p.Name}==null){{target.{p.Name} = {p.Name};}}else{{{p.Name}.CopyTo(target.{p.Name});}}" :
            $"target.{p.Name} = {p.Name};";

        return result;
    }
}
