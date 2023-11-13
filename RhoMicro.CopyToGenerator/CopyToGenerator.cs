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
    private const String _attributeName = "GenerateCopyToAttribute";
    private const String _attributeNamespace = "RhoMicro.CopyToGenerator";
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

                var isMarked = symbol.GetAttributes().Any(a =>
                    a.AttributeClass.Name == _attributeName &&
                    a.AttributeClass.ContainingNamespace.ToDisplayString() == _attributeNamespace) &&
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
                    .Append(symbol.Name).Append(" target){");

                var methodSource = props.Select(p => $"target.{p.Name} = {p.Name};")
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
                    _ = classSourceBuilder.Append("namespace ").Append(c.Symbol.ContainingNamespace.Name)
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
        context.RegisterPostInitializationOutput(c => c.AddSource(_attributeName, _attributeSource));
    }
}
