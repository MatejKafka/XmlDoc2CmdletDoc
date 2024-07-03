#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using XmlDoc2CmdletDoc.Core.Comments;
using XmlDoc2CmdletDoc.Core.Domain;

namespace XmlDoc2CmdletDoc.Core;

internal class HelpGenerator(ICommentReader reader, ReportWarning reportWarning) {
    private static readonly XNamespace MshNs = XNamespace.Get("http://msh");
    private static readonly XNamespace MamlNs = XNamespace.Get("http://schemas.microsoft.com/maml/2004/10");
    private static readonly XNamespace CommandNs = XNamespace.Get("http://schemas.microsoft.com/maml/dev/command/2004/10");
    private static readonly XNamespace DevNs = XNamespace.Get("http://schemas.microsoft.com/maml/dev/2004/10");

    private static readonly IXmlNamespaceResolver Resolver;

    static HelpGenerator() {
        var manager = new XmlNamespaceManager(new NameTable());
        manager.AddNamespace("", MshNs.NamespaceName);
        manager.AddNamespace("maml", MamlNs.NamespaceName);
        manager.AddNamespace("command", CommandNs.NamespaceName);
        manager.AddNamespace("dev", DevNs.NamespaceName);
        Resolver = manager;
    }

    /// <summary>
    /// Generates the root-level <em>&lt;helpItems&gt;</em> element.
    /// </summary>
    /// <param name="commands">All the commands in the module being documented.</param>
    /// <param name="isExcludedParameterSetName"></param>
    /// <returns>The root-level <em>helpItems</em> element.</returns>
    public XElement GenerateHelpXml(IEnumerable<Command> commands, Predicate<string> isExcludedParameterSetName) {
        var helpItemsElement = new XElement(MshNs + "helpItems", new XAttribute("schema", "maml"));
        foreach (var command in commands) {
            helpItemsElement.Add(Comment("Cmdlet: " + command.Name));
            helpItemsElement.Add(Command(command, isExcludedParameterSetName));
        }
        return helpItemsElement;
    }

    /// <summary>
    /// Generates a <em>&lt;command:command&gt;</em> element for the specified command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="isExcludedParameterSetName"></param>
    /// <returns>A <em>&lt;command:command&gt;</em> element that represents the <paramref name="command"/>.</returns>
    private XElement Command(Command command, Predicate<string> isExcludedParameterSetName) {
        return new XElement(CommandNs + "command",
                new XAttribute(XNamespace.Xmlns + "maml", MamlNs),
                new XAttribute(XNamespace.Xmlns + "command", CommandNs),
                new XAttribute(XNamespace.Xmlns + "dev", DevNs),
                new XElement(CommandNs + "details",
                        new XElement(CommandNs + "name", command.Name),
                        new XElement(CommandNs + "verb", command.Verb),
                        new XElement(CommandNs + "noun", command.Noun),
                        Synopsis(command)),
                Description(command),
                Syntax(command, isExcludedParameterSetName),
                Parameters(command),
                InputTypes(command),
                ReturnTypes(command),
                CommandAlerts(command),
                Examples(command),
                RelatedLinks(command));
    }

    /// <summary>
    /// Generates the <em>&lt;command:syntax&gt;</em> element for a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="isExcludedParameterSetName">Determines whether to exclude a parameter set from the cmdlet XML Help file, based on its name.</param>
    /// <returns>A <em>&lt;command:syntax&gt;</em> element for the <paramref name="command"/>.</returns>
    private XElement Syntax(Command command, Predicate<string> isExcludedParameterSetName) {
        var syntaxElement = new XElement(CommandNs + "syntax");
        IEnumerable<string> parameterSetNames = command.ParameterSetNames.ToList();
        if (parameterSetNames.Count() > 1) {
            parameterSetNames = parameterSetNames.Where(name => name != ParameterAttribute.AllParameterSets);
        }
        foreach (var parameterSetName in parameterSetNames.Where(name => !isExcludedParameterSetName(name))) {
            syntaxElement.Add(Comment("Parameter set: " + parameterSetName));
            syntaxElement.Add(SyntaxItem(command, parameterSetName));
        }
        return syntaxElement;
    }

    /// <summary>
    /// Generates the <em>&lt;command:syntaxItem&gt;</em> element for a specific parameter set of a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="parameterSetName">The parameter set name.</param>
    /// <returns>A <em>&lt;command:syntaxItem&gt;</em> element for the specific <paramref name="parameterSetName"/> of the <paramref name="command"/>.</returns>
    private XElement SyntaxItem(Command command, string parameterSetName) {
        var syntaxItemElement = new XElement(CommandNs + "syntaxItem",
                new XElement(MamlNs + "name", command.Name));
        foreach (var parameter in command.GetParameters(parameterSetName)
                         .Where(p => !p.GetCustomAttributes<ObsoleteAttribute>().Any())
                         .OrderBy(p => p.GetPosition(parameterSetName))
                         .ThenBy(p => p.IsRequired(parameterSetName) ? "0" : "1")
                         .ThenBy(p => p.Name)) {
            syntaxItemElement.Add(Comment("Parameter: " + parameter.Name));
            syntaxItemElement.Add(ParameterItem(parameter, parameterSetName));
        }
        return syntaxItemElement;
    }

    /// <summary>
    /// Generates the <em>&lt;command:parameters&gt;</em> element for a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A <em>&lt;command:parameters&gt;</em> element for the <paramref name="command"/>.</returns>
    private XElement Parameters(Command command) {
        var parametersElement = new XElement(CommandNs + "parameters");
        foreach (var parameter in command.Parameters) {
            parametersElement.Add(Comment("Parameter: " + parameter.Name));
            parametersElement.Add(ParameterItem(parameter, ParameterAttribute.AllParameterSets));
        }
        return parametersElement;
    }

    /// <summary>
    /// Generates a <em>&lt;command:parameter&gt;</em> element for a single parameter.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <param name="parameterSetName">The specific parameter set name, or <see cref="ParameterAttribute.AllParameterSets"/>.</param>
    /// <returns>A <em>&lt;command:parameter&gt;</em> element for the <paramref name="parameter"/>.</returns>
    private XElement ParameterItem(Parameter parameter, string parameterSetName) {
        var position = parameter.GetPosition(parameterSetName);

        var element = new XElement(CommandNs + "parameter",
                new XAttribute("required", parameter.IsRequired(parameterSetName)),
                new XAttribute("globbing", parameter.SupportsGlobbing),
                new XAttribute("pipelineInput", parameter.GetIsPipelineAttribute(parameterSetName)),
                position != null ? new XAttribute("position", position) : null,
                new XElement(MamlNs + "name", parameter.Name),
                ParameterDescription(parameter),
                ParameterValue(parameter),
                Type(parameter.ParameterType, true),
                ParameterDefaultValue(parameter),
                ParameterEnumeratedValues(parameter));
        var aliasNames = parameter.Aliases.ToList();
        if (aliasNames.Count > 0) {
            element.Add(new XAttribute("aliases", string.Join(",", aliasNames)));
        }
        return element;
    }

    /// <summary>
    /// Generates a <em>&lt;command:parameterValueGroup&gt;</em> element for a parameter
    /// in order to display enum choices in the cmdlet's syntax section.
    /// </summary>
    private static XElement? ParameterEnumeratedValues(Parameter parameter) {
        var enumValues = parameter.EnumValues.ToList();
        if (enumValues.Any()) {
            var parameterValueGroupElement = new XElement(CommandNs + "parameterValueGroup");
            foreach (var enumValue in enumValues) {
                parameterValueGroupElement.Add(ParameterEnumeratedValueItem(enumValue));
            }
            return parameterValueGroupElement;
        }
        return null;
    }

    /// <summary>
    /// Generates a <em>&lt;command:parameterValue&gt;</em> element for a single enum value.
    /// </summary>
    private static XElement ParameterEnumeratedValueItem(string enumValue) {
        // These hard-coded attributes were copied from what PowerShell's own core cmdlets use
        return new XElement(CommandNs + "parameterValue",
                new XAttribute("required", false),
                new XAttribute("variableLength", false),
                enumValue);
    }

    /// <summary>
    /// Generates the <em>&lt;command:inputTypes&gt;</em> element for a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A <em>&lt;command:inputTypes&gt;</em> element for the <paramref name="command"/>.</returns>
    private XElement InputTypes(Command command) {
        var inputTypesElement = new XElement(CommandNs + "inputTypes");
        var pipelineParameters = command.GetParameters(ParameterAttribute.AllParameterSets)
                .Where(p => p.IsPipeline(ParameterAttribute.AllParameterSets));
        foreach (var parameter in pipelineParameters) {
            inputTypesElement.Add(InputTypeItem(parameter));
        }
        return inputTypesElement;
    }

    /// <summary>
    /// Generates the <em>&lt;command:inputType&gt;</em> element for a pipeline parameter.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>A <em>&lt;command:inputType&gt;</em> element for the <paramref name="parameter"/>'s type.</returns>
    private XElement InputTypeItem(Parameter parameter) {
        // reuse the parameter description
        var inputTypeDescription = ParameterDescription(parameter);
        return new XElement(CommandNs + "inputType",
                Type(parameter.ParameterType, inputTypeDescription == null),
                inputTypeDescription);
    }

    /// <summary>
    /// Generates the <em>&lt;command:returnValues&gt;</em> element for a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A <em>&lt;command:returnValues&gt;</em> element for the <paramref name="command"/>.</returns>
    private XElement ReturnTypes(Command command) {
        var returnValueElement = new XElement(CommandNs + "returnValues");
        foreach (var type in command.OutputTypes) {
            returnValueElement.Add(Comment("OutputType: " + (type == typeof(void) ? "None" : type.Name)));
            var returnValueDescription = TypeDescription(type);
            returnValueElement.Add(new XElement(CommandNs + "returnValue",
                    Type(type, returnValueDescription == null),
                    returnValueDescription));
        }
        return returnValueElement;
    }

    /// <summary>
    /// Generates a <em>&lt;dev:type&gt;</em> element for a type.
    /// </summary>
    /// <param name="type">The type for which a corresponding <em>&lt;dev:type&gt;</em> element is required.</param>
    /// <param name="includeMamlDescription">Indicates whether a <em>&lt;maml:description&gt;</em> element should be
    /// included for the type. A description can be obtained from the type's XML Doc comment, but it is useful to suppress it if
    /// a more context-specific description is available where the <em>&lt;dev:type&gt;</em> element is actually used.</param>
    /// <returns>A <em>&lt;dev:type&gt;</em> element for the specified <paramref name="type"/>.</returns>
    private XElement Type(Type type, bool includeMamlDescription) {
        return new XElement(DevNs + "type",
                new XElement(MamlNs + "name", type == typeof(void) ? "None" : type.FullName),
                new XElement(MamlNs + "uri"),
                includeMamlDescription ? TypeDescription(type) : null);
    }

    /// <summary>
    /// Creates a comment.
    /// </summary>
    /// <param name="text">The text of the comment.</param>
    /// <returns>An <see cref="XComment"/> instance based on the specified <paramref name="text"/>.</returns>
    private static XComment Comment(string text) {
        return new XComment($" {text} ");
    }

    /// <summary>
    /// Obtains a <em>&lt;maml:description&gt;</em> element for a cmdlet's synopsis.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A description element for the cmdlet's synopsis.</returns>
    private XElement? Synopsis(Command command) {
        var cmdletType = command.CmdletType;
        var commentsElement = reader.GetComments(cmdletType);
        if (commentsElement == null) {
            return null;
        }

        var summary = commentsElement.Element("summary");
        return ParseDescription(summary, w => reportWarning(cmdletType, w), "<summary>");
    }

    /// <summary>
    /// Obtains a <em>&lt;maml:description&gt;</em> element for a cmdlet's full description.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A description element for the cmdlet's full description.</returns>
    private XElement? Description(Command command) {
        var cmdletType = command.CmdletType;
        var comment = reader.GetComments(cmdletType);
        if (comment == null) {
            // warning should be already raised by synopsis code
            return null;
        }

        var descriptions = comment.Elements("para").ToList();
        if (descriptions.Count == 0) {
            reportWarning(cmdletType, "Missing <para> description tags.");
            return null;
        }

        var elem = new XElement(MamlNs + "description");
        foreach (var e in descriptions) {
            elem.Add(new XElement(MamlNs + "para", RenderParagraph(e)));
        }
        return elem;
    }

    /// <summary>
    /// Obtains a <em>&lt;command:examples&gt;</em> element for a cmdlet's examples.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>An examples element for the cmdlet.</returns>
    private XElement? Examples(Command command) {
        var cmdletType = command.CmdletType;
        var comments = reader.GetComments(cmdletType);
        if (comments == null) {
            reportWarning(cmdletType, "No XML doc comment found.");
            return null;
        }

        var xmlDocExamples = comments.XPathSelectElements("//example").ToList();
        if (!xmlDocExamples.Any()) {
            reportWarning(cmdletType, "No examples found.");
            return null;
        }

        var examples = new XElement(CommandNs + "examples");
        var exampleNumber = 1;
        foreach (var xmlDocExample in xmlDocExamples) {
            examples.Add(ExampleItem(xmlDocExample, exampleNumber, warningText => reportWarning(cmdletType, warningText)));
            exampleNumber++;
        }
        return exampleNumber == 1 ? null : examples;
    }

    /// <summary>
    /// Obtains a <em>&lt;command:example&gt;</em> element based on an <em>&lt;example&gt;</em> XML doc comment element.
    /// </summary>
    /// <param name="exampleElement">The XML doc comment example element.</param>
    /// <param name="exampleNumber">The number of the example.</param>
    /// <param name="reportWarning">Used to log any warnings.</param>
    /// <returns>An example element.</returns>
    private static XElement ExampleItem(XElement exampleElement, int exampleNumber, Action<string> reportWarning) {
        var items = exampleElement.XPathSelectElements("para | code").ToList();
        var intros = items.TakeWhile(x => x.Name == "para").ToList();
        var code = items.SkipWhile(x => x.Name == "para").TakeWhile(x => x.Name == "code").FirstOrDefault();
        var paras = items.SkipWhile(x => x.Name == "para").SkipWhile(x => x.Name == "code").ToList();

        var example = new XElement(CommandNs + "example",
                new XElement(MamlNs + "title", $"----------  EXAMPLE {exampleNumber}  ----------"));

        var isEmpty = true;
        if (intros.Any()) {
            var introduction = new XElement(MamlNs + "introduction");
            intros.ForEach(intro => introduction.Add(new XElement(MamlNs + "para", RenderParagraph(intro))));
            example.Add(introduction);
            isEmpty = false;
        }
        if (code != null) {
            example.Add(new XElement(DevNs + "code", TidyCode(code.Value)));
            isEmpty = false;
        }
        if (paras.Any()) {
            var remarks = new XElement(DevNs + "remarks");
            paras.ForEach(para => remarks.Add(new XElement(MamlNs + "para", RenderParagraph(para))));
            example.Add(remarks);
            isEmpty = false;
        }

        if (isEmpty) {
            reportWarning($"No para or code elements found for example {exampleNumber}.");
        }

        return example;
    }

    /// <summary>
    /// Obtains a <em>&lt;command:relatedLinks&gt;</em> element for a cmdlet's related links.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>An relatedLinks element for the cmdlet.</returns>
    private XElement? RelatedLinks(Command command) {
        var cmdletType = command.CmdletType;
        var comments = reader.GetComments(cmdletType);
        if (comments == null) {
            return null;
        }

        var links = comments.Elements("seealso").ToArray();
        if (!links.Any()) {
            return null;
        }

        var relatedLinks = new XElement(MamlNs + "relatedLinks");
        foreach (var link in links) {
            var href = link.Attribute("href") is {} attr ? attr.Value : null;
            var linkText = link.Value != "" ? link.Value : href ?? "";

            var linkElem = new XElement(MamlNs + "navigationLink", new XElement(MamlNs + "linkText", linkText));
            if (href != null) {
                linkElem.Add(new XElement(MamlNs + "uri", href));
            }
            relatedLinks.Add(linkElem);
        }
        return relatedLinks;
    }

    /// <summary>
    /// Obtains a <em>&lt;maml:alertSet&gt;</em> element for a cmdlet's notes.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>A <em>&lt;maml:alertSet&gt;</em> element for the cmdlet's notes.</returns>
    private XElement? CommandAlerts(Command command) {
        var cmdletType = command.CmdletType;
        var comments = reader.GetComments(cmdletType);
        if (comments == null) {
            return null;
        }

        // First see if there's an alertSet element in the comments
        var alertSet = comments.XPathSelectElement("//maml:alertSet", Resolver);
        if (alertSet != null) {
            return alertSet;
        }

        // Next, search for a list element of type <em>alertSet</em>.
        var list = comments.XPathSelectElement("//list[@type='alertSet']");
        if (list == null) {
            return null;
        }
        alertSet = new XElement(MamlNs + "alertSet");
        foreach (var item in list.XPathSelectElements("item")) {
            var term = item.XPathSelectElement("term");
            var description = item.XPathSelectElement("description");
            if (term != null && description != null) {
                var alertTitle = new XElement(MamlNs + "title", RenderParagraph(term));

                var alert = new XElement(MamlNs + "alert");
                var paras = description.XPathSelectElements("para").ToList();
                if (paras.Any()) {
                    paras.ForEach(para => alert.Add(new XElement(MamlNs + "para", RenderParagraph(para))));
                } else {
                    alert.Add(new XElement(MamlNs + "para", RenderParagraph(description)));
                }

                alertSet.Add(alertTitle, alert);
            }
        }
        return alertSet;
    }

    /// <summary>
    /// Obtains a <em>&lt;maml:description&gt;</em> element for a parameter.
    /// If the parameter is an Enum, add to the description a list of its legal values.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>A description element for the parameter.</returns>
    private XElement? ParameterDescription(Parameter parameter) {
        if (parameter is not ReflectionParameter rp) {
            // other parameter types do not support XML comments
            return null;
        }

        var comment = rp.MemberInfo switch {
            FieldInfo fi => reader.GetComments(fi),
            PropertyInfo pi => reader.GetComments(pi),
            _ => throw new NotSupportedException("Member type not supported: " + rp.MemberInfo.MemberType),
        };

        var description = ParseDescription(comment, w => reportWarning(rp.MemberInfo, w));

        if (parameter.EnumValues.Any()) {
            description ??= new XElement(MamlNs + "description");
            description.Add(new XElement(MamlNs + "para", "Possible values: " + string.Join(", ", parameter.EnumValues)));
        }
        return description;
    }

    private static XElement? ParseDescription(XElement? comment, Action<string> reportWarning, string desc = "description") {
        if (comment == null) {
            reportWarning($"Missing {desc}.");
            return null;
        }

        // either the docstring is composed of one or more paragraphs (no further structure is supported)...
        var descriptions = comment.Elements("para").ToArray();
        if (descriptions.Length > 0) {
            var elem = new XElement(MamlNs + "description");
            foreach (var e in descriptions) {
                elem.Add(new XElement(MamlNs + "para", RenderParagraph(e)));
            }
            return elem;
        }

        // or it is an unstructured string (possibly using <see cref=...>, which is filtered out before this step)
        var value = RenderParagraph(comment);
        if (!string.IsNullOrEmpty(value)) {
            return new XElement(MamlNs + "description", new XElement(MamlNs + "para", value));
        } else {
            reportWarning($"Empty {desc}.");
            return null;
        }
    }

    /// <param name="parameter">The parameter.</param>
    /// <returns>A description element for the parameter.</returns>
    private static XElement ParameterValue(Parameter parameter) {
        return new XElement(CommandNs + "parameterValue",
                new XAttribute("required", true),
                GetSimpleTypeName(parameter.ParameterType));
    }

    /// <summary>
    /// Obtains a <em>&lt;dev:defaultValue&gt;</em> element for a parameter.
    /// </summary>
    /// <param name="parameter">The parameter.</param>
    /// <returns>A default value element for the parameter's default value, or <em>null</em> if a default value could not be obtained.</returns>
    private XElement? ParameterDefaultValue(Parameter parameter) {
        var defaultValue = parameter.GetDefaultValue(reportWarning); // TODO: Get the default value from the doc comments?
        if (defaultValue != null) {
            if (defaultValue is IEnumerable enumerable and not string) {
                var content = string.Join(", ", enumerable.Cast<object>().Select(element => element.ToString()));
                if (content != "") {
                    return new XElement(DevNs + "defaultValue", content);
                }
            } else {
                return new XElement(DevNs + "defaultValue", defaultValue.ToString());
            }
        }
        return null;
    }

    /// <summary>
    /// Obtains a <em>&lt;maml:description&gt;</em> element for a type.
    /// </summary>
    /// <param name="type">The type for which a description is required.</param>
    /// <returns>A description for the type, or null if no description is available.</returns>
    private XElement? TypeDescription(Type type) {
        if (type.IsArray) {
            type = type.GetElementType()!;
        }

        var comment = reader.GetComments(type);
        return ParseDescription(comment, w => reportWarning(type, w));
    }

    /// <summary>
    /// Tidies up the text retrieved from an XML doc comment. Multiple whitespace characters, including CR/LF,
    /// are replaced with a single space, and leading and trailing whitespace is removed.
    /// </summary>
    /// <param name="value">The string to tidy.</param>
    /// <returns>The tidied string.</returns>
    private static string StripWhitespace(string value) {
        return new Regex(@"\s{2,}").Replace(value, " ").Trim();
    }

    private static string RenderParagraph(XElement element) {
        var sb = new StringBuilder();
        foreach (var node in element.Nodes()) {
            if (node is XElement e) {
                if (e.Name.LocalName == "see" && e.Attribute("href") is {} attr) {
                    sb.Append(attr.Value);
                } else {
                    sb.Append(e.Value);
                }
            } else {
                sb.Append(node);
            }
        }
        return StripWhitespace(sb.ToString());
    }

    private static string TidyCode(string value) {
        // Split the value into separate lines, and eliminate leading and trailing empty lines.
        IEnumerable<string> lines = value.Split(["\r\n", "\n"], StringSplitOptions.None)
                .SkipWhile(string.IsNullOrWhiteSpace)
                .Reverse()
                .SkipWhile(string.IsNullOrWhiteSpace)
                .Reverse()
                .ToList();

        // If all the non-empty lines start with leading whitespace, remove it. (i.e. dedent the code).
        var pattern = new Regex(@"^\s*");
        var nonEmptyLines = lines.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (nonEmptyLines.Any()) {
            var shortestPrefixLength = nonEmptyLines.Min(s => pattern.Match(s).Value.Length);
            if (shortestPrefixLength > 0) {
                lines = lines.Select(s => s.Length <= shortestPrefixLength ? "" : s.Substring(shortestPrefixLength));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetSimpleTypeName(Type type) {
        if (type.IsArray) {
            return GetSimpleTypeName(type.GetElementType()!) + "[]";
        }

        if (PredefinedSimpleTypeNames.TryGetValue(type, out var result)) {
            return result;
        }
        return type.Name;
    }

    private static readonly IDictionary<Type, string> PredefinedSimpleTypeNames =
            new Dictionary<Type, string> {
                {typeof(object), "object"},
                {typeof(string), "string"},
                {typeof(bool), "bool"},
                {typeof(byte), "byte"},
                {typeof(char), "char"},
                {typeof(short), "short"},
                {typeof(ushort), "ushort"},
                {typeof(int), "int"},
                {typeof(uint), "uint"},
                {typeof(long), "long"},
                {typeof(ulong), "ulong"},
                {typeof(float), "float"},
                {typeof(double), "double"},
            };
}