using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AssemblyLoader
{
	public class Loader : IDisposable
	{
		public Loader()
		{
			AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
		}

#if !NET45
		LoadContext? _loadContext;
#endif

		public event Action<ResolvingEventArgs>? OnResolving;

		public Assembly?    Assembly          { get; internal set; }
		public List<string> AssemblyLocations { get; } = new List<string>();

		public Assembly Load(string assemblyPath)
		{
#if NET45
			return Assembly = Assembly.LoadFrom(assemblyPath);
#else
			_loadContext = new LoadContext(this, assemblyPath);

			return Assembly = _loadContext.LoadAssembly();
#endif
		}

		public Assembly? LoadAssembly(AssemblyName assemblyName)
		{
#if NET45
			return Assembly.Load(assemblyName);
#else
			return _loadContext?.LoadFromAssemblyName(assemblyName);
#endif
		}

		Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
		{
			if (args.Name == null)
				return null;

			var name      = new AssemblyName(args.Name);

			var eventArgs = new ResolvingEventArgs(name)
			{
				RequestingAssembly = args.RequestingAssembly,
			};

			InvokeOnResolving(eventArgs, ResolvingStep.Resolving);

			if (eventArgs.ResolvedAssembly != null)
				return eventArgs.ResolvedAssembly;

			LoadFromLocations(eventArgs);

			if (eventArgs.ResolvedAssembly == null)
			{
				InvokeOnResolving(eventArgs, ResolvingStep.NotResolved);
			}

			return eventArgs.ResolvedAssembly;
		}

		internal void InvokeOnResolving(ResolvingEventArgs args, ResolvingStep step)
		{
			args.Step = step;
			OnResolving?.Invoke(args);
		}

		bool LoadFromLocations(ResolvingEventArgs args)
		{
			IEnumerable<string> GetAssemblyLocations()
			{
				if (Assembly != null)
					yield return Path.GetDirectoryName(Assembly.Location)!;

				foreach (var location in AssemblyLocations)
					yield return location;

				if (args.RequestingAssembly?.Location != null)
					yield return Path.GetDirectoryName(args.RequestingAssembly.Location)!;

				yield return Environment.CurrentDirectory;
			}

			foreach (var al in GetAssemblyLocations())
			{
				args.ResolvedAssemblyPath = Path.Combine(al, args.Name.Name + ".dll");

				if (File.Exists(args.ResolvedAssemblyPath))
				{
					try
					{
						InvokeOnResolving(args, ResolvingStep.Resolved);

						if (args.ResolvedAssembly != null)
							return true;

						args.ResolvedAssembly = Assembly.LoadFrom(args.ResolvedAssemblyPath);

						if (args.ResolvedAssembly != null)
						{
							InvokeOnResolving(args, ResolvingStep.Loaded);
							return true;
						}
					}
					catch (Exception ex)
					{
						args.Exception = ex;
						InvokeOnResolving(args, ResolvingStep.Failed);

						throw;
					}
				}
			}

			return false;
		}

		public void Dispose()
		{
			AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
			GC.SuppressFinalize(this);
		}
	}
}
