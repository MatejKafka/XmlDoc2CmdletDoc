using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace XmlDoc2CmdletDoc.Core.Comments;

// Represents a factory method for creating types that implement
// the IXmlDocCommentReadPolicy interface.  The string parameter
// contains the full path of the XML doc comment file that is to
// be read by the policy.
/// <summary>
/// Provides methods to retrieve the XML Documentation Comments for an
/// object having a metadata type from the System.Reflection namespace.
/// </summary>
public sealed class XmlDocCommentReader : ICommentReader {
    private readonly Dictionary<string, XElement> _docComments;

    /// <summary>
    /// Creates a new instance of the <see cref="XmlDocCommentReader"/> class
    /// with a given path to the XML doc comments, and configures the reader
    /// to use a user-defined read policy.
    /// </summary>
    ///
    /// <param name="docCommentsFullPath">
    /// The full path of the XML doc comments.
    /// </param>
    /// <remarks>
    /// Used internally by test code to override file IO operations.
    /// </remarks>
    ///
    /// <exception cref="System.IO.FileNotFoundException">
    /// <paramref name="docCommentsFullPath"/> could does not exist or is inaccessible.
    /// </exception>
    public XmlDocCommentReader(string docCommentsFullPath) {
        if (!File.Exists(docCommentsFullPath)) {
            throw new FileNotFoundException(
                    $"The given XML doc comments file was not found: {docCommentsFullPath}",
                    docCommentsFullPath);
        }

        using var schema = typeof(XmlDocCommentReader).Assembly.GetManifestResourceStream("DocComments.xsd")!;

        var readerSettings = new XmlReaderSettings {ValidationType = ValidationType.Schema};
        readerSettings.Schemas.Add(XmlSchema.Read(schema, null)!);

        using var reader = XmlReader.Create(File.OpenText(docCommentsFullPath), readerSettings);
        _docComments = XDocument.Load(reader).Element("doc")!.Element("members")!.Elements("member")
                .ToDictionary(e => e.Attribute("name")!.Value, e => e);
    }

    /// <summary>
    /// Retrieves the xml doc comments for a given <see cref="System.Type"/>.
    /// </summary>
    ///
    /// <param name="type">
    /// The <see cref="System.Type"/> for which the doc comments are retrieved.
    /// </param>
    ///
    /// <returns>
    /// An <see cref="XElement"/> containing the requested XML doc comments,
    /// or NULL if none were found.
    /// </returns>
    public XElement GetComments(Type type) {
        return ReadMember(XmlDocCommentNameConverter.ToXmlDocCommentMember(type));
    }

    /// <summary>
    /// Retrieves the xml doc comments for a given <see cref="System.Reflection.EventInfo"/>.
    /// </summary>
    ///
    /// <param name="eventInfo">
    /// The <see cref="System.Reflection.EventInfo"/> for which the doc comments are retrieved.
    /// </param>
    ///
    /// <returns>
    /// An <see cref="XElement"/> containing the requested XML doc comments,
    /// or NULL if none were found.
    /// </returns>
    public XElement GetComments(EventInfo eventInfo) {
        return ReadMember(XmlDocCommentNameConverter.ToXmlDocCommentMember(eventInfo));
    }

    /// <summary>
    /// Retrieves the xml doc comments for a given <see cref="System.Reflection.FieldInfo"/>.
    /// </summary>
    ///
    /// <param name="field">
    /// The <see cref="System.Reflection.FieldInfo"/> for which the doc comments are retrieved.
    /// </param>
    ///
    /// <returns>
    /// An <see cref="XElement"/> containing the requested XML doc comments,
    /// or NULL if none were found.
    /// </returns>
    public XElement GetComments(FieldInfo field) {
        return ReadMember(XmlDocCommentNameConverter.ToXmlDocCommentMember(field));
    }

    /// <summary>
    /// Retrieves the xml doc comments for a given <see cref="System.Reflection.PropertyInfo"/>.
    /// </summary>
    ///
    /// <param name="property">
    /// The <see cref="System.Reflection.PropertyInfo"/> for which the doc comments are retrieved.
    /// </param>
    ///
    /// <returns>
    /// An <see cref="XElement"/> containing the requested XML doc comments,
    /// or NULL if none were found.
    /// </returns>
    public XElement GetComments(PropertyInfo property) {
        return ReadMember(XmlDocCommentNameConverter.ToXmlDocCommentMember(property));
    }

    /// <summary>
    /// Retrieves the xml doc comments for a given <see cref="System.Reflection.ConstructorInfo"/>.
    /// </summary>
    ///
    /// <param name="constructor">
    /// The <see cref="System.Reflection.ConstructorInfo"/> for which the doc comments are retrieved.
    /// </param>
    ///
    /// <returns>
    /// An <see cref="XElement"/> containing the requested XML doc comments,
    /// or NULL if none were found.
    /// </returns>
    public XElement GetComments(ConstructorInfo constructor) {
        return ReadMember(XmlDocCommentNameConverter.ToXmlDocCommentMember(constructor));
    }

    /// <summary>
    /// Retrieves the xml doc comments for a given <see cref="System.Reflection.MethodInfo"/>.
    /// </summary>
    ///
    /// <param name="method">
    /// The <see cref="System.Reflection.MethodInfo"/> for which the doc comments are retrieved.
    /// </param>
    ///
    /// <returns>
    /// An <see cref="XElement"/> containing the requested XML doc comments,
    /// or NULL if none were found.
    /// </returns>
    public XElement GetComments(MethodInfo method) {
        return ReadMember(XmlDocCommentNameConverter.ToXmlDocCommentMember(method));
    }

    private XElement ReadMember(string memberName) {
        return !_docComments.TryGetValue(memberName, out var elem)
                ? null
                : XElement.Load(elem.CreateReader()); // clone the XML element
    }
}