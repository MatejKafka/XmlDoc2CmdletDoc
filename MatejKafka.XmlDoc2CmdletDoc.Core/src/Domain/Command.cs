#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Reflection;

namespace XmlDoc2CmdletDoc.Core.Domain;

/// <summary>
/// Represents a single cmdlet.
/// </summary>
public class Command {
    private readonly CmdletAttribute _attribute;

    /// <summary>
    /// Creates a new instance based on the specified cmdlet type.
    /// </summary>
    /// <param name="cmdletType">The type of the cmdlet. Must be a subclass of <see cref="Cmdlet"/>
    /// and have a <see cref="CmdletAttribute"/>.</param>
    public Command(Type cmdletType) {
        CmdletType = cmdletType ?? throw new ArgumentNullException(nameof(cmdletType));
        _attribute = CmdletType.GetCustomAttribute<CmdletAttribute>() ??
                     throw new ArgumentException("Missing CmdletAttribute", nameof(cmdletType));
    }

    /// <summary>
    /// The type of the cmdlet for this command.
    /// </summary>
    public readonly Type CmdletType;

    /// <summary>
    /// The cmdlet verb.
    /// </summary>
    public string Verb => _attribute.VerbName;

    /// <summary>
    /// The cmdlet noun.
    /// </summary>
    public string Noun => _attribute.NounName;

    /// <summary>
    /// The cmdlet name, of the form verb-noun.
    /// </summary>
    public string Name => Verb + "-" + Noun;

    /// <summary>
    /// The output types declared by the command.
    /// </summary>
    public IEnumerable<Type> OutputTypes => CmdletType.GetCustomAttributes<OutputTypeAttribute>()
            .SelectMany(attr => attr.Type)
            .Select(pstype => pstype.Type)
            .Distinct()
            .OrderBy(type => type.FullName);

    /// <summary>
    /// The parameters belonging to the command.
    /// </summary>
    public IEnumerable<Parameter> Parameters {
        get {
            IEnumerable<Parameter> parameters = CmdletType.GetMembers(BindingFlags.Instance | BindingFlags.Public)
                    .Where(member => member.GetCustomAttributes<ParameterAttribute>().Any())
                    .Select(member => new ReflectionParameter(CmdletType, member));
            if (typeof(IDynamicParameters).IsAssignableFrom(CmdletType)) {
                foreach (var nestedType in CmdletType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Public |
                                                                     BindingFlags.NonPublic)) {
                    parameters = parameters.Concat(nestedType.GetMembers(BindingFlags.Instance | BindingFlags.Public)
                            .Where(member => member.GetCustomAttributes<ParameterAttribute>().Any())
                            .Select(member => new ReflectionParameter(nestedType, member)));
                }

                var cmdlet = (IDynamicParameters) Activator.CreateInstance(CmdletType);

                if (cmdlet.GetDynamicParameters() is RuntimeDefinedParameterDictionary runtimeParamDictionary) {
                    parameters = parameters.Concat(runtimeParamDictionary
                            .Where(member => member.Value.Attributes.OfType<ParameterAttribute>().Any())
                            .Select(member => new RuntimeParameter(CmdletType, member.Value)));
                }
            }
            return parameters.ToList();
        }
    }

    /// <summary>
    /// The command's parameters that belong to the specified parameter set.
    /// </summary>
    /// <param name="parameterSetName">The name of the parameter set.</param>
    /// <returns>
    /// The command's parameters that belong to the specified parameter set.
    /// </returns>
    public IEnumerable<Parameter> GetParameters(string parameterSetName) =>
            parameterSetName == ParameterAttribute.AllParameterSets
                    ? Parameters
                    : Parameters.Where(p => p.ParameterSetNames.Contains(parameterSetName) ||
                                            p.ParameterSetNames.Contains(ParameterAttribute.AllParameterSets));

    /// <summary>
    /// The names of the parameter sets that the parameters belongs to.
    /// </summary>
    public IEnumerable<string> ParameterSetNames =>
            Parameters.SelectMany(p => p.ParameterSetNames)
                    .Union([ParameterAttribute.AllParameterSets]) // Parameterless cmdlets need this seeded
                    .Distinct();
}