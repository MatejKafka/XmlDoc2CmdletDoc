using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace XmlDoc2CmdletDoc.Core.Domain;

/// Represents a single parameter of a cmdlet that is defined at runtime.
public class RuntimeParameter : Parameter {
    private readonly RuntimeDefinedParameter _runtimeDefinedParameter;

    /// Creates a new instance.
    /// <param name="cmdletType">The type of the cmdlet the parameter belongs to.</param>
    /// <param name="runtimeDefinedParameter">The dynamic runtime parameter member of the cmdlet.</param>
    public RuntimeParameter(Type cmdletType, RuntimeDefinedParameter runtimeDefinedParameter)
            : base(cmdletType, runtimeDefinedParameter.Attributes.OfType<ParameterAttribute>()) {
        _runtimeDefinedParameter = runtimeDefinedParameter;
    }

    /// The name of the parameter.
    public override string Name => _runtimeDefinedParameter.Name;

    /// The type of the parameter.
    public override Type ParameterType => _runtimeDefinedParameter.ParameterType;

    /// The type of this parameter's member - method, constructor, property, and so on.
    public override MemberTypes MemberType =>
            MemberTypes.Property; //RuntimeDefinedParameters are always defined as a Property

    /// <inheritdoc />
    public override bool SupportsGlobbing => _runtimeDefinedParameter.Attributes.OfType<SupportsWildcardsAttribute>().Any();

    /// The default value of the parameter. Runtime parameters do not support specifying default values.
    public override object? GetDefaultValue(Action<MemberInfo, string> reportWarning) {
        // RuntimeDefinedParameter cannot have a default value
        return null;
    }

    /// Retrieves custom attributes defined on the parameter.
    /// <typeparam name="T">The type of attribute to retrieve.</typeparam>
    public override IEnumerable<T> GetCustomAttributes<T>() => _runtimeDefinedParameter.Attributes.OfType<T>();
}