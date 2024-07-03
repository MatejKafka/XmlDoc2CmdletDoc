using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using XmlDoc2CmdletDoc.Core.Comments;
using XmlDoc2CmdletDoc.Core.Domain;

namespace XmlDoc2CmdletDoc.Core;

/// <summary>
/// Delegate used when reporting a warning against a reflected member.
/// </summary>
/// <param name="target">The reflected member to which the warning pertains.</param>
/// <param name="warningText">The warning message.</param>
public delegate void ReportWarning(MemberInfo target, string warningText);

/// <summary>
/// <para>Does all the work of generating the XML help file for an assembly. See <see cref="GenerateHelp"/>.</para>
/// <para>This class is stateless, so you can call <see cref="GenerateHelp"/> multitple times on multiple threads.</para>
/// </summary>
/// <remarks>Most of the detailed help generation is delegated to <see cref="HelpGenerator"/>.</remarks>
public static class Engine {
    /// <summary>
    /// Public entry point that triggers the creation of the cmdlet XML help file for a single assembly.
    /// </summary>
    /// <param name="options">Defines the locations of the input assembly, the input XML doc comments file for the
    /// assembly, and where the cmdlet XML help file should be written to.</param>
    /// <returns>A code indicating the result of the help generation.</returns>
    public static EngineExitCode GenerateHelp(Options options) {
        try {
            var warnings = new List<Tuple<MemberInfo, string>>();
            ReportWarning reportWarning = options.IgnoreMissingDocs
                    ? (_, _) => {}
                    : (target, warningText) => warnings.Add(Tuple.Create(target, warningText));

            var (loaderRet, assembly) = LoadAssembly(options);
            using var loader = loaderRet;

            var commentReader = LoadComments(options, reportWarning);
            var cmdletTypes = GetCommands(assembly);


            var generator = new HelpGenerator(commentReader, reportWarning);
            var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
                    generator.GenerateHelpXml(cmdletTypes, options.IsExcludedParameterSetName));

            HandleWarnings(options, warnings, assembly);

            using var stream = new FileStream(options.OutputHelpFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            document.Save(writer);

            return EngineExitCode.Success;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            if (exception is ReflectionTypeLoadException typeLoadException) {
                foreach (var loaderException in typeLoadException.LoaderExceptions) {
                    Console.Error.WriteLine("Loader exception: {0}", loaderException);
                }
            }
            var engineException = exception as EngineException;
            return engineException?.ExitCode ?? EngineExitCode.UnhandledException;
        }
    }

    /// <summary>
    /// Handles the list of warnings generated once the cmdlet help XML document has been generated.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="warnings">The warnings generated during the creation of the cmdlet help XML document. Each tuple
    /// consists of the type to which the warning pertains, and the text of the warning.</param>
    /// <param name="targetAssembly">The assembly of the PowerShell module being documented.</param>
    private static void HandleWarnings(Options options, IEnumerable<Tuple<MemberInfo, string>> warnings,
            Assembly targetAssembly) {
        var groups = warnings.Where(tuple => {
                    // Exclude warnings about types outside the assembly being documented.
                    var type = tuple.Item1 as Type ?? tuple.Item1.DeclaringType;
                    return type != null && type.Assembly == targetAssembly;
                })
                .GroupBy(tuple => GetFullyQualifiedName(tuple.Item1), tuple => tuple.Item2)
                .OrderBy(group => group.Key)
                .ToList();
        if (groups.Any()) {
            var writer = options.TreatWarningsAsErrors ? Console.Error : Console.Out;
            writer.WriteLine("Warnings:");
            foreach (var group in groups) {
                writer.WriteLine("    {0}:", group.Key);
                foreach (var warningText in group.Distinct()) {
                    writer.WriteLine("        {0}", warningText);
                }
            }
            if (options.TreatWarningsAsErrors) {
                throw new EngineException(EngineExitCode.WarningsAsErrors,
                        "Failing due to the occurence of one or more warnings");
            }
        }
    }

    /// <summary>
    /// Returns the fully-qualified name of a type, property or field.
    /// </summary>
    /// <param name="memberInfo">The member.</param>
    /// <returns>The fully qualified name of the member.</returns>
    private static string GetFullyQualifiedName(MemberInfo memberInfo) {
        return memberInfo is Type type
                ? type.FullName
                : $"{GetFullyQualifiedName(memberInfo.DeclaringType)}.{memberInfo.Name}";
    }

    /// <summary>
    /// Loads the assembly indicated in the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>The assembly indicated in the <paramref name="options"/>.</returns>
    private static (AssemblyDependencyResolver, Assembly) LoadAssembly(Options options) {
        if (!File.Exists(options.AssemblyPath)) {
            throw new EngineException(EngineExitCode.AssemblyNotFound,
                    "Assembly file not found: " + options.AssemblyPath);
        }

        AssemblyDependencyResolver loader = null;
        try {
            loader = new AssemblyDependencyResolver(options.AssemblyPath);
            return (loader, loader.Assembly);
        } catch (Exception exception) {
            loader?.Dispose();
            throw new EngineException(EngineExitCode.AssemblyLoadError,
                    "Failed to load assembly from file: " + options.AssemblyPath, exception);
        }
    }

    /// <summary>
    /// Obtains an XML Doc comment reader for the assembly in the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="reportWarning">Function used to log warnings.</param>
    /// <returns>A comment reader for the assembly in the <paramref name="options"/>.</returns>
    private static ICommentReader LoadComments(Options options, ReportWarning reportWarning) {
        var docCommentsPath = options.DocCommentsPath;
        if (!File.Exists(docCommentsPath)) {
            throw new EngineException(EngineExitCode.AssemblyCommentsNotFound,
                    "Assembly comments file not found: " + docCommentsPath);
        }
        try {
            return new CachingCommentReader(
                    new RewritingCommentReader(
                            new LoggingCommentReader(
                                    new XmlDocCommentReader(docCommentsPath), reportWarning)));
        } catch (Exception exception) {
            throw new EngineException(EngineExitCode.DocCommentsLoadError,
                    "Failed to load XML Doc comments from file: " + docCommentsPath,
                    exception);
        }
    }

    /// <summary>
    /// Retrieves a sequence of <see cref="Command"/> instances, one for each cmdlet defined in the specified <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>A sequence of commands, one for each cmdlet defined in the <paramref name="assembly"/>.</returns>
    private static IEnumerable<Command> GetCommands(Assembly assembly) {
        return assembly.ExportedTypes
                .Where(static t => t is {IsAbstract: false, IsInterface: false, IsValueType: false})
                .Where(t => t.IsSubclassOf(typeof(Cmdlet)))
                .Where(t => t.GetCustomAttributes<CmdletAttribute>(inherit: false).Any())
                .Select(type => new Command(type))
                .OrderBy(command => command.Noun)
                .ThenBy(command => command.Verb);
    }
}