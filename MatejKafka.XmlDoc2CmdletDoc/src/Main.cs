using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using XmlDoc2CmdletDoc.Core;

namespace XmlDoc2CmdletDoc;

public static class Program {
    public static void Main(string[] args) {
        var options = ParseArguments(args);
        Console.Error.WriteLine(options);

        try {
            Engine.GenerateHelp(options);
        } catch (EngineException ex) {
            var code = (int) ex.ExitCode;
            PrintError(code, ex.Message);
            Environment.Exit(code);
        } catch (ReflectionTypeLoadException ex) {
            var code = (int) EngineExitCode.AssemblyLoaderError;
            foreach (var e in ex.LoaderExceptions) {
                PrintError(code, e?.ToString());
            }
            Environment.Exit(code);
        } catch (Exception ex) {
            var code = (int) EngineExitCode.GenericException;
            PrintError(code, ex.ToString());
            Environment.Exit(code);
        }
    }

    private static void PrintError(int code, string message) {
        Console.Error.WriteLine($"XmlDoc2CmdletDoc: error PS{code:D3}: {message}");
    }

    private static Options ParseArguments(IReadOnlyList<string> args) {
        try {
            var treatWarningsAsErrors = false;
            var ignoreMissingDocs = false;
            var ignoreOptional = false;
            var excludedParameterSets = new List<string>();
            string assemblyPath = null;

            for (var i = 0; i < args.Count; i++) {
                if (args[i] == "-strict") {
                    treatWarningsAsErrors = true;
                } else if (args[i] == "-ignoreMissing") {
                    ignoreMissingDocs = true;
                } else if (args[i] == "-ignoreOptional") {
                    ignoreOptional = true;
                } else if (args[i] == "-excludeParameterSets") {
                    i++;
                    if (i >= args.Count) throw new ArgumentException();
                    excludedParameterSets.AddRange(args[i].Split([','], StringSplitOptions.RemoveEmptyEntries)
                            .Select(p => p.Trim()));
                } else if (assemblyPath == null) {
                    assemblyPath = args[i];
                } else {
                    throw new ArgumentException($"Unrecognized argument: {args[i]}");
                }
            }

            if (assemblyPath == null) {
                throw new ArgumentException("Missing assembly path (first positional argument).");
            }

            var warnings = ignoreMissingDocs ? Warnings.IgnoreAll
                    : ignoreOptional ? Warnings.RequireCmdletSynopsis | Warnings.RequireTypeDescription
                    : Warnings.RequireAll;

            return new Options(assemblyPath, treatWarningsAsErrors, warnings, excludedParameterSets.Contains);
        } catch (ArgumentException) {
            Console.Error.WriteLine(
                    "Usage: XmlDoc2CmdletDoc.exe [-strict] [-ignoreMissing] [-ignoreOptional] [-excludeParameterSets parameterSetToExclude1,parameterSetToExclude2] assemblyPath");
            Environment.Exit(-1);
            throw;
        }
    }
}