using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace XmlDoc2CmdletDoc.Core.Domain;

/// Represents a single parameter of a cmdlet that is identified via reflection.
public class ReflectionParameter : Parameter {
    /// The <see cref="PropertyInfo"/> or <see cref="FieldInfo"/> that defines the property.
    public readonly MemberInfo MemberInfo;

    /// Creates a new instance.
    /// <param name="cmdletType">The type of the cmdlet the parameter belongs to.</param>
    /// <param name="memberInfo">The parameter member of the cmdlet. May represent either a field or property.</param>
    public ReflectionParameter(Type cmdletType, MemberInfo memberInfo)
            : base(cmdletType, memberInfo.GetCustomAttributes<ParameterAttribute>()) {
        MemberInfo = memberInfo;
    }

    /// The name of the parameter.
    public override string Name => MemberInfo.Name;

    /// The type of the parameter.
    public override Type ParameterType {
        get {
            return MemberType switch {
                MemberTypes.Property => GetActualType(((PropertyInfo) MemberInfo).PropertyType),
                MemberTypes.Field => GetActualType(((FieldInfo) MemberInfo).FieldType),
                _ => throw new NotSupportedException("Unsupported type: " + MemberInfo)
            };
            Type GetActualType(Type type) => Nullable.GetUnderlyingType(type) ?? type;
        }
    }

    /// The type of this parameter's member - method, constructor, property, and so on.
    public override MemberTypes MemberType => MemberInfo.MemberType;

    /// <inheritdoc />
    public override bool SupportsGlobbing => MemberInfo.GetCustomAttributes<SupportsWildcardsAttribute>(true).Any();

    /// The default value of the parameter. This is obtained by instantiating the cmdlet and accessing the parameter
    /// property or field to determine its initial value.
    public override object? GetDefaultValue(Action<MemberInfo, string> reportWarning) {
        var cmdlet = Activator.CreateInstance(CmdletType);
        switch (MemberInfo.MemberType) {
            case MemberTypes.Property:
                var propertyInfo = ((PropertyInfo) MemberInfo);
                if (!propertyInfo.CanRead) {
                    reportWarning(MemberInfo, "Parameter does not have a getter. Unable to determine its default value");
                    return null;
                }
                return propertyInfo.GetValue(cmdlet);
            case MemberTypes.Field:
                return ((FieldInfo) MemberInfo).GetValue(cmdlet);
            default:
                throw new NotSupportedException("Unsupported type: " + MemberInfo);
        }
    }

    /// Retrieves custom attributes defined on the parameter.
    /// <typeparam name="T">The type of attribute to retrieve.</typeparam>
    public override IEnumerable<T> GetCustomAttributes<T>() => MemberInfo.GetCustomAttributes<T>(true);
}