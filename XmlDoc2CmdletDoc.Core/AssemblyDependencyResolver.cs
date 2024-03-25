#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace XmlDoc2CmdletDoc.Core;

// adapted from https://github.com/dotnet/runtime/issues/1050
internal sealed class AssemblyDependencyResolver : IDisposable {
    private readonly AssemblyLoadContext _loadContext;
    public readonly Assembly Assembly;
    private readonly DependencyContext _dependencyContext;
    private readonly ICompilationAssemblyResolver _assemblyResolver;

    public AssemblyDependencyResolver(string loadedAssemblyPath) {
        _loadContext = new AssemblyLoadContext(loadedAssemblyPath, true);
        _loadContext.Resolving += OnResolving;
        Assembly = _loadContext.LoadFromAssemblyPath(loadedAssemblyPath);
        _dependencyContext = DependencyContext.Load(Assembly)
                             ?? throw new RuntimeException("Could not load DependencyContext");
        _assemblyResolver = new CompositeCompilationAssemblyResolver([
            // probe the app's bin folder
            new AppBaseCompilationAssemblyResolver(Path.GetDirectoryName(loadedAssemblyPath)!),
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
        if (_assemblyResolver.TryResolveAssemblyPaths(compileLibrary, assemblyPaths)) {
            try {
                return _loadContext.LoadFromAssemblyPath(assemblyPaths.First());
            } catch {
                // ignored
            }
        }

        return null;
    }
}