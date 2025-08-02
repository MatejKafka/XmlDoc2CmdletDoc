using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace XmlDoc2CmdletDoc.Core;

// adapted from https://github.com/dotnet/runtime/issues/1050
internal sealed class AssemblyDependencyResolver : IDisposable {
    private readonly AssemblyLoadContext _loadContext;
    public readonly Assembly Assembly;
    private readonly DependencyContext? _dependencyContext;
    private readonly ICompilationAssemblyResolver _assemblyResolver;
    private readonly string _baseDir;

    public AssemblyDependencyResolver(string loadedAssemblyPath) {
        // NOTE: we need AssemblyLoadContext, MetadataLoadContext is not enough, because the cmdlets are instantiated
        //  to find default parameter values and dynamic parameters
        _loadContext = new AssemblyLoadContext(loadedAssemblyPath, true);
        _loadContext.Resolving += OnResolving;
        Assembly = _loadContext.LoadFromAssemblyPath(loadedAssemblyPath);
        // this will be null if there's no .deps.json file
        _dependencyContext = DependencyContext.Load(Assembly);
        _baseDir = Path.GetDirectoryName(loadedAssemblyPath)!;
        _assemblyResolver = new CompositeCompilationAssemblyResolver([
            // probe reference assemblies
            new ReferenceAssemblyPathResolver(),
            // probe NuGet package cache
            new PackageCompilationAssemblyResolver(),
        ]);
    }

    public void Dispose() {
        _loadContext.Resolving -= OnResolving;
        _loadContext.Unload();
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name) {
        var path = ResolveAssemblyPath(name);
        if (path == null) {
            return null; // not found
        }

        try {
            return _loadContext.LoadFromAssemblyPath(path);
        } catch {
            return null; // loading failed, ignore
        }
    }

    private string? ResolveAssemblyPath(AssemblyName name) {
        // loading from the base directory using `AppBaseCompilationAssemblyResolver` seems to behave a bit wonky,
        //  and I'm a bit confused on how it should even theoretically find the assemblies (it iterates over the `Assemblies`
        //  property of `CompilationLibrary`, but that one seems empty for all assemblies I tried)
        // try to load the assembly from the base assembly directory manually
        var localPath = ResolveFromBaseDirectory(name);
        if (localPath != null) {
            return localPath;
        }

        // TODO: check what happens with arch-specific subdirectories in the publish dir

        if (_dependencyContext == null) {
            return null; // no .deps.json file, and not present in the base directory
        }

        // try to resolve the dependent assembly by looking in the dependency ({assembly}.deps.json) file
        var compileLibrary = _dependencyContext.CompileLibraries
                .FirstOrDefault(x => x.Name.Equals(name.Name, StringComparison.OrdinalIgnoreCase));

        if (compileLibrary == null) {
            // if the application has PreserveCompilationContext set to 'false' we also need to check runtime libraries
            // this shouldn't be the case with projects using Microsoft.NET.Sdk.Web, which defaults to 'true'
            var runtimeLibrary = _dependencyContext.RuntimeLibraries
                    .FirstOrDefault(x => x.Name.Equals(name.Name, StringComparison.OrdinalIgnoreCase));

            if (runtimeLibrary == null) {
                return null;
            } else {
                compileLibrary = new CompilationLibrary(runtimeLibrary.Type, runtimeLibrary.Name, runtimeLibrary.Version,
                        runtimeLibrary.Hash, runtimeLibrary.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
                        runtimeLibrary.Dependencies, runtimeLibrary.Serviceable);
            }
        }

        var assemblyPaths = new List<string>();
        if (_assemblyResolver.TryResolveAssemblyPaths(compileLibrary, assemblyPaths) && assemblyPaths.Count > 0) {
            return assemblyPaths[0];
        }

        return null;
    }

    private string? ResolveFromBaseDirectory(AssemblyName name) {
        var path = Path.Join(_baseDir, name.Name + ".dll");

        AssemblyName fileAssembly;
        try {
            fileAssembly = AssemblyName.GetAssemblyName(path);
        } catch (FileNotFoundException) {
            return null;
        }

        if (!string.Equals(fileAssembly.FullName, name.FullName, StringComparison.OrdinalIgnoreCase)) {
            return null; // assembly details (most likely version) do not match
        }

        return path;
    }
}