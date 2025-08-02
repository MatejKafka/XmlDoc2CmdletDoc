using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace XmlDoc2CmdletDoc.Core.Domain;

/// Represents a single parameter of a cmdlet.
public abstract class Parameter {
    /// The type of the cmdlet this parameter is defined on.
    protected readonly Type CmdletType;
    private readonly IEnumerable<ParameterAttribute> _attributes;

    /// Creates a new instance.
    /// <param name="cmdletType">The type of the cmdlet the parameter belongs to.</param>
    /// <param name="attributes">The parameter attributes of the cmdlet.</param>
    protected Parameter(Type cmdletType, IEnumerable<ParameterAttribute> attributes) {
        CmdletType = cmdletType;
        _attributes = attributes;
    }

    /// The name of the parameter.
    public abstract string Name {get;}

    /// The type of the parameter.
    public abstract Type ParameterType {get;}

    /// The type of this parameter's member - method, constructor, property, and so on.
    public abstract MemberTypes MemberType {get;}

    /// Indicates whether the parameter supports globbing.
    public abstract bool SupportsGlobbing {get;}

    /// The names of the parameter sets that the parameter belongs to.
    public IEnumerable<string> ParameterSetNames => _attributes.Select(attr => attr.ParameterSetName);

    private IEnumerable<ParameterAttribute> GetAttributes(string parameterSetName) =>
            parameterSetName == ParameterAttribute.AllParameterSets
                    ? _attributes
                    : _attributes.Where(attr => attr.ParameterSetName == parameterSetName ||
                                                attr.ParameterSetName == ParameterAttribute.AllParameterSets);

    /// Indicates whether the parameter is mandatory.
    public bool IsRequired(string parameterSetName) => GetAttributes(parameterSetName).Any(attr => attr.Mandatory);

    /// Indicates whether the parameter takes its value from the pipeline input.
    public bool IsPipeline(string parameterSetName) =>
            GetAttributes(parameterSetName).Any(attr => attr.ValueFromPipeline || attr.ValueFromPipelineByPropertyName);

    /// Indicates whether the parameter takes its value from the pipeline input.
    public string GetIsPipelineAttribute(string parameterSetName) {
        var attributes = GetAttributes(parameterSetName).ToList();
        bool byValue = attributes.Any(attr => attr.ValueFromPipeline);
        bool byParameterName = attributes.Any(attr => attr.ValueFromPipelineByPropertyName);
        return byValue
                ? byParameterName
                        ? "true (ByValue, ByPropertyName)"
                        : "true (ByValue)"
                : byParameterName
                        ? "true (ByPropertyName)"
                        : "false";
    }

    /// The position of the parameter, or <em>null</em> if no position is defined.
    public string? GetPosition(string parameterSetName) {
        var attribute = GetAttributes(parameterSetName).FirstOrDefault();
        if (attribute == null) return null;
        return attribute.Position == int.MinValue ? "named" : Convert.ToString(attribute.Position);
    }

    /// The default value of the parameter. This may be obtained by instantiating the cmdlet and accessing the parameter
    /// property or field to determine its initial value.
    public abstract object? GetDefaultValue(Action<MemberInfo, string> reportWarning);

    /// The list of enumerated value names. Returns an empty sequence if there are no enumerated values
    /// (normally because the parameter type is not an Enum type).
    public IEnumerable<string> EnumValues {
        get {
            if (MemberType == MemberTypes.Property) {
                Type? enumType = null;

                if (ParameterType.IsEnum)
                    enumType = ParameterType;
                else {
                    foreach (var @interface in ParameterType.GetInterfaces()) {
                        if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IEnumerable<>)) {
                            var genericArgument = @interface.GetGenericArguments()[0];

                            if (genericArgument.IsEnum)
                                enumType = genericArgument;

                            break;
                        }
                    }
                }

                if (enumType != null) {
                    return enumType
                            .GetFields(BindingFlags.Public | BindingFlags.Static)
                            .Select(field => field.Name);
                }
            }

            return [];
        }
    }

    /// The list of parameter aliases.
    public IEnumerable<string> Aliases => GetCustomAttributes<AliasAttribute>().FirstOrDefault()?.AliasNames ?? [];

    /// Retrieves custom attributes defined on the parameter.
    /// <typeparam name="T">The type of attribute to retrieve.</typeparam>
    public abstract IEnumerable<T> GetCustomAttributes<T>() where T : Attribute;
}