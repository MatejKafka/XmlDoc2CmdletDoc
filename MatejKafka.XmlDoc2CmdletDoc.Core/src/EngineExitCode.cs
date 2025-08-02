namespace XmlDoc2CmdletDoc.Core;

/// Exit codes for the <see cref="Engine"/>.
public enum EngineExitCode {
    Success = 0,
    /// The target assembly could not be found.
    AssemblyNotFound = 1,
    /// The target assembly could not be loaded. This could indicate that the
    /// target assembly is not architecture independent.
    AssemblyLoadError = 2,
    /// The XML Doc comments file for the target assembly could not be found.
    AssemblyCommentsNotFound = 3,
    /// An error occurred whilst trying to load the target assembly's XML Doc comments file.
    DocCommentsLoadError = 4,
    /// An unspecified exception occurred.
    UnhandledException = 5,
    /// One or more warnings occurred, and warnings are treated as errors when in strict mode.
    WarningsAsErrors = 6,
}