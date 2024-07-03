// ----------------------------------------------------------------------------
// Convert.cs
//
// Contains the definition of the Convert class.
// Copyright 2009 Steve Guidi.
//
// File created: 2/6/2009 6:03:32 PM
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace XmlDoc2CmdletDoc.Core.Comments;

/// <summary>
/// Provides methods to convert between representations of a type.
/// </summary>
public static class XmlDocCommentNameConverter {
    #region public methods --------------------------------------------------------------------

    /// <summary>
    /// Creates the XML doc comment member reference string
    /// for a given <see cref="System.Type"/>.
    /// </summary>
    ///
    /// <param name="type">
    /// The <see cref="System.Type"/> to convert.
    /// </param>
    ///
    /// <returns>
    /// A string containing the requested member reference.
    /// </returns>
    public static string ToXmlDocCommentMember(Type type) {
        return "T:" + GetXDCFullTypeName(type);
    }

    /// <summary>
    /// Creates the XML doc comment member reference string
    /// for a given <see cref="System.Reflection.EventInfo"/>.
    /// </summary>
    ///
    /// <param name="eventInfo">
    /// The <see cref="System.Reflection.EventInfo"/> to convert.
    /// </param>
    ///
    /// <returns>
    /// A string containing the requested member reference.
    /// </returns>
    public static string ToXmlDocCommentMember(EventInfo eventInfo) {
        var builder = new StringBuilder().Append("E:");
        return ToXmlDocCommentMember(builder, eventInfo, EmptyParameterList, out _).ToString();
    }

    /// <summary>
    /// Creates the XML doc comment member reference string
    /// for a given <see cref="System.Reflection.FieldInfo"/>.
    /// </summary>
    ///
    /// <param name="field">
    /// The <see cref="System.Reflection.FieldInfo"/> to convert.
    /// </param>
    ///
    /// <returns>
    /// A string containing the requested member reference.
    /// </returns>
    public static string ToXmlDocCommentMember(FieldInfo field) {
        var builder = new StringBuilder().Append("F:");
        return ToXmlDocCommentMember(builder, field, EmptyParameterList, out _).ToString();
    }

    /// <summary>
    /// Creates the XML doc comment member reference string
    /// for a given <see cref="System.Reflection.PropertyInfo"/>.
    /// </summary>
    ///
    /// <param name="property">
    /// The <see cref="System.Reflection.PropertyInfo"/> to convert.
    /// </param>
    ///
    /// <returns>
    /// A string containing the requested member reference.
    /// </returns>
    public static string ToXmlDocCommentMember(PropertyInfo property) {
        var builder = new StringBuilder().Append("P:");
        return ToXmlDocCommentMember(builder, property, property.GetIndexParameters(), out _).ToString();
    }

    /// <summary>
    /// Creates the XML doc comment member reference string
    /// for a given <see cref="System.Reflection.ConstructorInfo"/>.
    /// </summary>
    ///
    /// <param name="constructor">
    /// The <see cref="System.Reflection.ConstructorInfo"/> to convert.
    /// </param>
    ///
    /// <returns>
    /// A string containing the requested member reference.
    /// </returns>
    public static string ToXmlDocCommentMember(ConstructorInfo constructor) {
        var builder = new StringBuilder().Append("M:"); // constructor is treated as method
        ToXmlDocCommentMember(builder, constructor, constructor.GetParameters(), out var namePosition);
        builder[namePosition] = '#'; // Replaces . with # in ctor name.
        return builder.ToString();
    }

    /// <summary>
    /// Creates the XML doc comment member reference string
    /// for a given <see cref="System.Reflection.MethodInfo"/>.
    /// </summary>
    ///
    /// <param name="method">
    /// The <see cref="System.Reflection.MethodInfo"/> to convert.
    /// </param>
    ///
    /// <returns>
    /// A string containing the requested member reference.
    /// </returns>
    public static string ToXmlDocCommentMember(MethodInfo method) {
        var builder = new StringBuilder().Append("M:");
        ToXmlDocCommentMember(builder, method, method.GetParameters(), out _);

        if (Array.BinarySearch(["op_Explicit", "op_Implicit"], method.Name) >= 0) {
            builder.Append('~');
            AppendXDCParameterTypesTo(builder, [method.ReturnType]);
        }

        return builder.ToString();
    }

    #endregion

    #region private methods -------------------------------------------------------------------

    /// <summary>
    /// Creates the XML doc comment member reference string
    /// for a given type, indexing the starting position of the
    /// converted member name.
    /// </summary>
    ///
    /// <typeparam name="TMember">
    /// The type of the member to convert.
    /// </typeparam>
    ///
    /// <param name="builder"></param>
    ///
    /// <param name="member">
    /// The member from which the string is created.
    /// </param>
    ///
    /// <param name="memberParameters">
    /// The parameters to the member, if any.
    /// </param>
    ///
    /// <param name="namePosition">
    /// The position in the resulting string that indexes that starting
    /// position of the member name.
    /// </param>
    ///
    /// <returns>
    /// A string containing the requested member reference.
    /// </returns>
    private static StringBuilder ToXmlDocCommentMember<TMember>(StringBuilder builder, TMember member,
            ParameterInfo[] memberParameters, out int namePosition) where TMember : MemberInfo {
        builder.Append(GetXDCFullTypeName(member.DeclaringType));
        builder.Append('.');

        namePosition = builder.Length;
        builder.Append(member.Name);

        var methodInfo = member as MethodInfo;
        if (methodInfo != null && methodInfo.IsGenericMethod) {
            builder.Append("``");
            builder.Append(methodInfo.GetGenericArguments().Length);
        }

        if (memberParameters.Length > 0) {
            builder.Append('(');
            AppendXDCParameterTypesTo(builder, memberParameters.Select(methodParam => methodParam.ParameterType));
            builder.Append(')');
        }

        return builder;
    }

    /// <remarks>
    /// Includes the appropriate symbol when the given type is generic.
    /// </remarks>
    private static string GetXDCFullTypeName(Type type) {
        if (type.IsGenericType) {
            type = type.GetGenericTypeDefinition();
        }
        return NormalizeXDCTypeName(type);
    }

    /// <summary>
    /// Appends the XML doc comment representation of the abbreviated name of the given
    /// <see cref="System.Type"/> to a given <see cref="System.Text.StringBuilder"/>.
    /// </summary>
    ///
    /// <param name="builder">
    /// The <see cref="System.Text.StringBuilder"/> to which the type name is appended.
    /// </param>
    ///
    /// <param name="type">
    /// The type whose name is appended.
    /// </param>
    ///
    /// <returns>
    /// The <see cref="System.Text.StringBuilder"/> parameter, modified by the appended name.
    /// </returns>
    ///
    /// <remarks>
    /// Does not include the appropriate symbol when the given type is generic.
    /// </remarks>
    private static StringBuilder AppendXDCTypeNameTo(StringBuilder builder, Type type) {
        var typeNameBuilder = new StringBuilder();
        typeNameBuilder.Append(NormalizeXDCTypeName(type));

        if (type.IsGenericType) {
            typeNameBuilder.Length = type.FullName.IndexOf('`');
        }

        return builder.Append(typeNameBuilder);
    }

    /// <remarks>
    /// Handles both top-level and nested type types.
    /// </remarks>
    private static string NormalizeXDCTypeName(Type type) {
        return type.IsNested ? type.FullName!.Replace('+', '.') : type.FullName;
    }

    /// <summary>
    /// Appends the XML doc comment representation of the name of each given
    /// <see cref="System.Type"/> to a given <see cref="System.Text.StringBuilder"/>.
    /// </summary>
    ///
    /// <param name="builder">
    /// The <see cref="System.Text.StringBuilder"/> to which the type names are appended.
    /// </param>
    ///
    /// <param name="parameterTypes">
    /// The types whose names are appended.
    /// </param>
    ///
    /// <returns>
    /// The <see cref="System.Text.StringBuilder"/> parameter, modified by the appended names.
    /// </returns>
    private static StringBuilder AppendXDCParameterTypesTo(StringBuilder builder, IEnumerable<Type> parameterTypes) {
        foreach (var origType in parameterTypes) {
            var t = origType;
            var parameterModifier = ReduceToElementType(ref t);
            if (t.IsGenericType) {
                AppendXDCTypeNameTo(builder, t.GetGenericTypeDefinition()).Append('{');
                AppendXDCParameterTypesTo(builder, t.GetGenericArguments()).Append('}');
            } else if (t.IsGenericParameter) {
                builder.Append('`');
                if (t.DeclaringMethod != null) {
                    builder.Append('`');
                }

                builder.Append(t.GenericParameterPosition);
            } else {
                AppendXDCTypeNameTo(builder, t);
            }

            builder.Append(parameterModifier).Append(',');
        }

        builder.Length -= 1;
        return builder;
    }

    /// <summary>
    /// Modifies the given <see cref="System.Type"/> by reducing it to
    /// its inner-most element type (removes any array or pointer decorations),
    /// and creates the type's XML doc comment reprentation.
    /// </summary>
    ///
    /// <param name="type">
    /// The <see cref="System.Type"/> to reduce.
    /// </param>
    ///
    /// <returns>
    /// The XML doc comment representation of the given type's name.
    /// </returns>
    ///
    /// <remarks>
    /// The element type is the type of the given type, excluding
    /// any array, pointer or by-ref modifiers.
    /// </remarks>
    private static string ReduceToElementType(ref Type type) {
        var builder = new StringBuilder();
        while (type.IsByRef) {
            // ELEMENT_TYPE_BYREF
            builder.Append('@');
            type = type.GetElementType();
        }

        while (type.IsArray) {
            int rank = type.GetArrayRank();
            if (rank == 1) {
                // ELEMENT_TYPE_SZARRAY
                builder.Insert(0, "[]");
            } else {
                // ELEMENT_TYPE_ARRAY
                builder.Insert(0, ']')
                        .Insert(0, "0:")
                        .Insert(0, "0:,", rank - 1)
                        .Insert(0, '[');
            }

            type = type.GetElementType();
        }

        while (type.IsPointer) {
            // ELEMENT_TYPE_PTR
            builder.Insert(0, '*');
            type = type.GetElementType();
        }

        return builder.ToString();
    }

    #endregion

    #region private fields --------------------------------------------------------------------

    private static readonly ParameterInfo[] EmptyParameterList = [];

    #endregion
}