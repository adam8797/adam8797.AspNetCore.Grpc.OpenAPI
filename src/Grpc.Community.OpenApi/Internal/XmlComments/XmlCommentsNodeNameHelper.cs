// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;

namespace Grpc.Community.OpenApi.Internal.XmlComments;

/// <summary>
/// Helpers for building the member-name strings that
/// <see href="https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/">C# XML documentation files</see>
/// use to identify types and methods.
/// </summary>
internal static class XmlCommentsNodeNameHelper
{
    public static string GetMemberNameForType(Type type)
    {
        var builder = new StringBuilder("T:");
        AppendTypeName(builder, type, expandGenericArgs: false);
        return builder.ToString();
    }

    public static string GetMemberNameForMethod(MethodInfo methodInfo)
    {
        var builder = new StringBuilder("M:");
        AppendTypeName(builder, methodInfo.DeclaringType!, expandGenericArgs: false);
        builder.Append('.');
        builder.Append(methodInfo.Name);

        if (methodInfo.IsGenericMethod)
        {
            builder.Append("``").Append(methodInfo.GetGenericArguments().Length);
        }

        var parameters = methodInfo.GetParameters();
        if (parameters.Length > 0)
        {
            builder.Append('(');
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }
                AppendTypeName(builder, parameters[i].ParameterType, expandGenericArgs: true);
            }
            builder.Append(')');
        }

        return builder.ToString();
    }

    public static string GetMemberNameForProperty(PropertyInfo propertyInfo)
    {
        var builder = new StringBuilder("P:");
        AppendTypeName(builder, propertyInfo.DeclaringType!, expandGenericArgs: false);
        builder.Append('.');
        builder.Append(propertyInfo.Name);
        return builder.ToString();
    }

    private static void AppendTypeName(StringBuilder builder, Type type, bool expandGenericArgs)
    {
        if (type.IsGenericParameter)
        {
            builder.Append('`').Append(type.GenericParameterPosition);
            return;
        }

        if (type.IsByRef)
        {
            AppendTypeName(builder, type.GetElementType()!, expandGenericArgs);
            builder.Append('@');
            return;
        }

        if (type.IsArray)
        {
            AppendTypeName(builder, type.GetElementType()!, expandGenericArgs);
            builder.Append('[').Append(',', type.GetArrayRank() - 1).Append(']');
            return;
        }

        if (type.DeclaringType != null)
        {
            AppendTypeName(builder, type.DeclaringType, expandGenericArgs);
            builder.Append('.');
        }
        else if (!string.IsNullOrEmpty(type.Namespace))
        {
            builder.Append(type.Namespace).Append('.');
        }

        if (type.IsGenericType)
        {
            var backtickIndex = type.Name.IndexOf('`');
            builder.Append(backtickIndex >= 0 ? type.Name.Substring(0, backtickIndex) : type.Name);

            if (expandGenericArgs)
            {
                var args = type.GetGenericArguments();
                builder.Append('{');
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }
                    AppendTypeName(builder, args[i], expandGenericArgs);
                }
                builder.Append('}');
            }
            else
            {
                builder.Append('`').Append(type.GetGenericArguments().Length);
            }
        }
        else
        {
            builder.Append(type.Name);
        }
    }
}
