﻿#nullable disable

using System;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Xml.Linq;

namespace XmlDoc2CmdletDoc.Core.Comments;

/// <summary>
/// Implementation of <see cref="ICommentReader"/> that decorates an existing reader, typically <see cref="XmlDocCommentReader"/>,
/// and performs simple modifications to the XML Doc comments. Modifications include expanding &lt;see cref="xxxx"&gt; elements.
/// </summary>
public class RewritingCommentReader : ICommentReader {
    private readonly ICommentReader _proxy;

    /// <summary>
    /// Creates a new instance that decorates the specified <paramref name="proxy"/>.
    /// </summary>
    /// <param name="proxy">The proxy source of comments.</param>
    public RewritingCommentReader(ICommentReader proxy) {
        _proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public XElement GetComments(Type type) => RewriteComment(_proxy.GetComments(type));
    public XElement GetComments(FieldInfo fieldInfo) => RewriteComment(_proxy.GetComments(fieldInfo));
    public XElement GetComments(PropertyInfo propertyInfo) => RewriteComment(_proxy.GetComments(propertyInfo));
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    private static XElement RewriteComment(XElement element) {
        if (element == null) {
            return null;
        }

        foreach (var childElement in element.Elements()) {
            CollapseSeeElements(childElement);
        }
        return element;
    }

    private static void CollapseSeeElements(XElement element) {
        if (!CollapseSeeElement(element)) {
            return;
        }

        foreach (var e in element.Elements()) {
            CollapseSeeElements(e);
        }
    }

    private static bool CollapseSeeElement(XElement element) {
        if (element.Name.LocalName == "see") {
            var attr = element.Attribute("cref");
            if (attr != null) {
                var text = element.Value;
                if (string.IsNullOrWhiteSpace(text)) {
                    text = GetTextForCrefValue(attr.Value);
                }
                if (!string.IsNullOrWhiteSpace(text)) {
                    element.ReplaceWith(text);
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Returns a text representation of a reflection item referenced in a &lt;see cref=&quot;xxx&quot;/&gt; element.
    /// </summary>
    /// <param name="cref">The cref attribute value.</param>
    /// <returns>A text representation of the referenced item.</returns>
    private static string GetTextForCrefValue(string cref) {
        // First try to find the corresponding type.
        if (cref.StartsWith("T:")) {
            var type = GetType(cref.Substring(2));
            if (type != null) {
                // If the referenced type is actually a cmdlet type, return its cmdlet name rather than its type name.
                var cmdletAttribute = GetCustomAttribute<CmdletAttribute>(type);
                if (cmdletAttribute != null) {
                    return $"{cmdletAttribute.VerbName}-{cmdletAttribute.NounName}";
                }

                // Otherwise, return the short name.
                return type.Name;
            }
        }

        // Otherwise, just try to convert the cref value to a short name.
        var lastPeriodIndex = cref.LastIndexOf('.');
        return lastPeriodIndex != -1
                ? cref.Substring(lastPeriodIndex + 1)
                : (cref.Length >= 2 && cref[1] == ':' ? cref.Substring(2) : cref);
    }

    /// <summary>
    /// Obtains a <see cref="Type"/> having the specified <paramref name="typeName"/>. The type is found by
    /// searching the assemblies loaded in the current app domain.
    /// </summary>
    /// <param name="typeName">The name of the type.</param>
    /// <returns>The corresponding type, or <em>null</em> if the type could not be found.</returns>
    private static Type GetType(string typeName) {
        return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(type => type != null);
    }

    /// <summary>
    /// Retrieves a single custom attribute from a type.
    /// </summary>
    /// <typeparam name="T">The type of the required custom attribute.</typeparam>
    /// <param name="type">The target type.</param>
    /// <returns>A single custom attribute of type <typeparamref name="T"/> for the target <paramref name="type"/>,
    /// or <em>null</em> if none could be found.</returns>
    private static T GetCustomAttribute<T>(Type type)
            where T : Attribute {
        return type.GetCustomAttributes(typeof(T))
                .Cast<T>()
                .FirstOrDefault();
    }
}