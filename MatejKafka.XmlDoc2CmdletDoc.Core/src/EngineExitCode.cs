namespace XmlDoc2CmdletDoc.Core;

/// Exit codes for the <see cref="Engine"/>.
public enum EngineExitCode {
    GenericException = 100,
    AssemblyLoaderError = 101,
    /// The target assembly could not be found.
    AssemblyNotFound = 102,
    /// The target assembly could not be loaded. This could indicate that the
    /// target assembly is not architecture independent.
    AssemblyLoadError = 103,
    /// The XML Doc comments file for the target assembly could not be found.
    AssemblyCommentsNotFound = 104,
    /// An error occurred whilst trying to load the target assembly's XML Doc comments file.
    DocCommentsLoadError = 105,
}