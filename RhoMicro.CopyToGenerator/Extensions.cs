namespace RhoMicro.CopyToGenerator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

static class Extensions
{
    public static Boolean IsCopyable(this ITypeSymbol symbol) =>
        symbol.IsMarked() ||
        symbol.TryGetIEnumerableItemType(out _);
    public static Boolean IsMarked(this ITypeSymbol symbol) =>
        symbol.GetAttributes().Any(a =>
                    a.AttributeClass.Name == CopyToGenerator.AttributeName &&
                    a.AttributeClass.ContainingNamespace.ToDisplayString() == CopyToGenerator.AttributeNamespace);
    public static Boolean TryGetIEnumerableItemType(this ITypeSymbol symbol, out ITypeSymbol itemType)
    {
        var result =
            symbol.TryGetLiteralIEnumerableItemType(out itemType) ||
            symbol.TryGetImplementedIEnumerableItemType(out itemType);

        return result;
    }

    public static Boolean TryGetImplementedIEnumerableItemType(this ITypeSymbol symbol, out ITypeSymbol itemType)
    {
        var pair = symbol.AllInterfaces
            .Select(i => (Success: i.TryGetIEnumerableItemType(out var t), Type: t))
            .FirstOrDefault(t => t.Success);
        var result = pair.Success;
        itemType = pair.Type;

        return result;
    }
    public static Boolean TryGetLiteralIEnumerableItemType(this ITypeSymbol symbol, out ITypeSymbol itemType)
    {
        var result = symbol is INamedTypeSymbol named &&
                named.Name == nameof(IEnumerable) &&
                named.IsGenericType &&
                named.ContainingNamespace.ToDisplayString() == typeof(IEnumerable<>).Namespace &&
                named.TypeArguments.Length == 1 &&
                named.TypeArguments[0].IsCopyable();

        itemType = result ?
            (symbol as INamedTypeSymbol).TypeArguments[0] :
            null;

        return result;
    }
}