using System;
using System.Collections.Generic;
using System.Linq;
using XmlDoc2CmdletDoc.Core;

namespace XmlDoc2CmdletDoc
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var options = ParseArguments(args);
            Console.WriteLine(options);
            var engine = new Engine();

            var exitCode = engine.GenerateHelp(options);
            if (exitCode != 0)
            {
                Console.WriteLine("GenerateHelp completed with exit code '{0}'", exitCode);
            }
            Environment.Exit((int)exitCode);
        }

        private static Options ParseArguments(IReadOnlyList<string> args)
        {
            try
            {
                var treatWarningsAsErrors = false;
                var ignoreMissingDocs = false;
                var excludedParameterSets = new List<string>();
                string assemblyPath = null;

                for (var i = 0; i < args.Count; i++)
                {
                    if (args[i] == "-strict")
                    {
                        treatWarningsAsErrors = true;
                    }
                    else if (args[i] == "-ignoreMissing")
                    {
                        ignoreMissingDocs = true;
                    }
                    else if (args[i] == "-excludeParameterSets")
                    {
                        i++;
                        if (i >= args.Count) throw new ArgumentException();
                        excludedParameterSets.AddRange(args[i].Split(new [] {','}, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
                    }
                    else if (assemblyPath == null)
                    {
                        assemblyPath = args[i];
                    }
                    else
                    {
                        throw new ArgumentException();
                    }
                }

                if (assemblyPath == null)
                {
                    throw new ArgumentException();
                }

                return new Options(assemblyPath, treatWarningsAsErrors, ignoreMissingDocs, excludedParameterSets.Contains);
            }
            catch (ArgumentException)
            {
                Console.Error.WriteLine("Usage: XmlDoc2CmdletDoc.exe [-strict] [-ignoreMissing] [-excludeParameterSets parameterSetToExclude1,parameterSetToExclude2] assemblyPath");
                Environment.Exit(-1);
                throw;
            }
        }
    }
}