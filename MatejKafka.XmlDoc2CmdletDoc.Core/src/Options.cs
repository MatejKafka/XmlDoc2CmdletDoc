using System;
using System.IO;

namespace XmlDoc2CmdletDoc.Core;

[Flags]
public enum Warnings {
    IgnoreAll = 0,
    RequireCmdletSynopsis = 1,
    RequireParameterDescription = 2,
    RequireTypeDescription = 4,
    RequireAll = 7,
}

/// Represents the settings necessary for generating the cmdlet XML help file for a single assembly.
/// <param name="assemblyPath">The path of the taget assembly whose XML Doc comments file is to be converted into a cmdlet XML Help file.</param>
/// <param name="treatWarningsAsErrors">Indicates whether the presence of warnings should be treated as a failure condition.</param>
/// <param name="warnings">Indicates which missing docs element should result in a warning.</param>
/// <param name="isExcludedParameterSetName">A predicate that determines whether a parameter set should be excluded from the
/// output help file, based on its name. This is intended to be used for deprecated parameter sets, to make them less discoverable.</param>
/// <param name="outputHelpFilePath">The output path of the cmdlet XML Help file.
/// If <c>null</c>, an appropriate default is selected based on <paramref name="assemblyPath"/>.</param>
/// <param name="docCommentsPath">The path of the XML Doc comments file for the target assembly.
/// If <c>null</c>, an appropriate default is selected based on <paramref name="assemblyPath"/></param>
public class Options(
        string assemblyPath,
        bool treatWarningsAsErrors = false,
        Warnings warnings = Warnings.RequireCmdletSynopsis,
        Predicate<string>? isExcludedParameterSetName = null,
        string? outputHelpFilePath = null,
        string? docCommentsPath = null) {
    /// The absolute path of the cmdlet assembly.
    public readonly string AssemblyPath = Path.GetFullPath(assemblyPath);

    /// The absolute path of the output <c>.dll-Help.xml</c> help file.
    public readonly string OutputHelpFilePath =
            Path.GetFullPath(outputHelpFilePath ?? Path.ChangeExtension(assemblyPath, "dll-Help.xml"));

    /// The absolute path of the assembly's XML Doc comments file.
    public readonly string DocCommentsPath = Path.GetFullPath(docCommentsPath ?? Path.ChangeExtension(assemblyPath, ".xml"));

    /// Indicates whether the presence of warnings should be treated as a failure condition.
    public readonly bool TreatWarningsAsErrors = treatWarningsAsErrors;

    public readonly Warnings Warnings = warnings;

    /// A predicate that determines whether a parameter set should be excluded from the
    /// output help file, based on its name. This is intended to be used for deprecated parameter sets,
    /// to make them less discoverable.
    public readonly Predicate<string> IsExcludedParameterSetName = isExcludedParameterSetName ?? (_ => false);

    public override string ToString() => $"AssemblyPath: {AssemblyPath}, " +
                                         $"OutputHelpFilePath: {OutputHelpFilePath}, " +
                                         $"TreatWarningsAsErrors {TreatWarningsAsErrors}, " +
                                         $"WarningLevel: {Warnings}";
}