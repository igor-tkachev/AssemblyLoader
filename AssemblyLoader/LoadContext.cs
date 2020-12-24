#if !NET45

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;
using Newtonsoft.Json.Linq;

namespace AssemblyLoader
{
	class LoadContext : AssemblyLoadContext
	{
		public LoadContext(Loader loader, string assemblyPath)
		{
			_loader       = loader;
			_assemblyPath = assemblyPath;

#if NETCOREAPP3_1 || NET5_0
			_dependencyResolver = new AssemblyDependencyResolver(assemblyPath.Replace('\\', Path.DirectorySeparatorChar));
#endif
		}

		readonly Loader _loader;
		readonly string _assemblyPath;


		DependencyContext?            _dependencyContext;
		ICompilationAssemblyResolver? _assemblyResolver;

#if NETCOREAPP3_1 || NET5_0
		readonly AssemblyDependencyResolver _dependencyResolver;
#endif

		public Assembly LoadAssembly()
		{
			var assembly = LoadFromAssemblyPath(_assemblyPath);

			_dependencyContext = DependencyContext.Load(assembly);
			_assemblyResolver  = new CompositeCompilationAssemblyResolver(
				new ICompilationAssemblyResolver[]
				{
					new AppBaseCompilationAssemblyResolver(Path.GetDirectoryName(_assemblyPath)),
					new ReferenceAssemblyPathResolver(),
					new PackageCompilationAssemblyResolver(),
				});

			static string? GetRuntimeIdentifier(Assembly a)
			{
				if (Path.GetDirectoryName(a.Location) is {} dir)
				{
					var files = Directory.GetFiles(dir, "*.deps.json");

					if (files.Length != 0)
					{
						var json = JObject.Parse(File.ReadAllText(Path.Combine(dir, files[0])));

						if (json["runtimeTarget"]?["name"]?.ToString() is {} name)
						{
							var slashPos = name.LastIndexOf('/');

							if (slashPos >= 0)
								return name.Substring(slashPos + 1);
						}
					}
				}

				return null;
			}

			var rid =
				GetRuntimeIdentifier(assembly) ??
				GetRuntimeIdentifier(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly);

			if (rid != null)
			{
				_compatibleRuntimes = _dependencyContext.RuntimeGraph
					.Where(g => g.Runtime == rid)
					.Select(g => new[] { g.Runtime }.Concat(g.Fallbacks).ToArray())
					.FirstOrDefault() ?? new string[0];

				var names1 = _dependencyContext.GetDefaultAssemblyNames().ToList();
				var names2 = _dependencyContext.GetDefaultNativeAssets().ToList();
				var names3 = _dependencyContext.GetDefaultNativeRuntimeFileAssets().ToList();
				var names4 = _dependencyContext.GetRuntimeAssemblyNames(rid).ToList();
				var names5 = _dependencyContext.GetRuntimeNativeAssets(rid).ToList();
				var names6 = _dependencyContext.GetRuntimeNativeRuntimeFileAssets(rid).ToList();
			}

			return assembly;
		}

		string[] _compatibleRuntimes = new string[0];

		protected override Assembly? Load(AssemblyName name)
		{
			if (name.Name == null)
				return null;

			var args = new ResolvingEventArgs(name);

			_loader.InvokeOnResolving(args, ResolvingStep.Resolving);

			if (args.ResolvedAssembly != null)
				return args.ResolvedAssembly;


#if NETCOREAPP3_1 || NET5_0
			args.ResolvedAssemblyPath = _dependencyResolver.ResolveAssemblyToPath(name);

			if (!string.IsNullOrEmpty(args.ResolvedAssemblyPath) && File.Exists(args.ResolvedAssemblyPath))
			{
				_loader.InvokeOnResolving(args, ResolvingStep.Resolved);

				if (args.ResolvedAssembly != null)
					return args.ResolvedAssembly;

				args.ResolvedAssembly = LoadFromAssemblyPath(args.ResolvedAssemblyPath);

				if (args.ResolvedAssembly != null)
				{
					_loader.InvokeOnResolving(args, ResolvingStep.Loaded);
					return args.ResolvedAssembly;
				}
			}
#endif

			var library =
				(
					from r in _dependencyContext?.RuntimeLibraries
					where
						r.Type != "package" && string.Equals(r.Name, name.Name, StringComparison.OrdinalIgnoreCase) ||
						r.RuntimeAssemblyGroups.Any(g =>
							g.RuntimeFiles.Any(f =>
								//f.AssemblyVersion == name.Version.ToString() &&
								Path.GetFileNameWithoutExtension(f.Path) == name.Name))
					select r
				)
				.FirstOrDefault();

			if (library != null)
			{
				var wrapper = new CompilationLibrary(
					library.Type,
					library.Name,
					library.Version,
					library.Hash,
					library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
					library.Dependencies,
					library.Serviceable);

				var assemblies = new List<string>();

				if (_assemblyResolver?.TryResolveAssemblyPaths(wrapper, assemblies) == true)
				{
					assemblies = OrderAssemblies(name, library, assemblies);

					foreach (var assembly in assemblies)
					{
						args.ResolvedAssemblyPath = assembly;

						_loader.InvokeOnResolving(args, ResolvingStep.Resolved);

						if (args.ResolvedAssembly != null)
							return args.ResolvedAssembly;
					}

					try
					{
						args.ResolvedAssemblyPath = assemblies[0];
						args.ResolvedAssembly     = LoadFromAssemblyPath(args.ResolvedAssemblyPath);

						if (args.ResolvedAssembly != null)
						{
							_loader.InvokeOnResolving(args, ResolvingStep.Loaded);
							return args.ResolvedAssembly;
						}
					}
					catch (Exception ex)
					{
						args.Exception = ex;
						_loader.InvokeOnResolving(args, ResolvingStep.Failed);

						throw;
					}
				}
			}

			if (args.ResolvedAssembly == null)
				_loader.InvokeOnResolving(args, ResolvingStep.NotResolved);

			return args.ResolvedAssembly;
		}

		List<string> OrderAssemblies(AssemblyName name, RuntimeLibrary runtimeLibrary, List<string> assemblies)
		{
			if (assemblies.Count == 1)
				return assemblies;

			assemblies = assemblies.Distinct().ToList();

			if (assemblies.Count == 1)
				return assemblies;

			var runtimeAssemblyGroups =
			(
				from g in runtimeLibrary.RuntimeAssemblyGroups
				where assemblies.Any(a => g.AssetPaths.Any(a.EndsWith))
				let order =
					string.IsNullOrEmpty(g.Runtime) ?
						int.MaxValue - 2 :
						Array.IndexOf(_compatibleRuntimes, g.Runtime) switch
						{
							-1    => int.MaxValue - 1,
							var n => n
						}
				select new { g, order }
			)
			.ToList();

			assemblies =
			(
				from a in assemblies
				from r in runtimeAssemblyGroups.Where(r => r.g.AssetPaths.Any(a.EndsWith)).DefaultIfEmpty()
				orderby
					Path.GetFileNameWithoutExtension(a) != name.Name,
					r?.order ?? int.MaxValue
				select a
			)
			.ToList();

			return assemblies;
		}

		protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
		{
			var args = new ResolvingEventArgs(new AssemblyName(unmanagedDllName));

			_loader.InvokeOnResolving(args, ResolvingStep.Resolving);

//			if (args.ResolvedAssembly != null)
//				return args.ResolvedAssembly;

#if NETCOREAPP3_1 || NET5_0
			var resolvedPath = _dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);

			if (!string.IsNullOrEmpty(resolvedPath) && File.Exists(resolvedPath))
			{
				return LoadUnmanagedDllFromPath(resolvedPath);
			}
#endif

			return base.LoadUnmanagedDll(unmanagedDllName);
		}
	}
}

#endif