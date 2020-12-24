using System;
using System.Reflection;

namespace AssemblyLoader
{
	public enum ResolvingStep
	{
		Resolving,
		Resolved,
		Loaded,
		NotResolved,
		Failed
	}

	public class ResolvingEventArgs
	{
		internal ResolvingEventArgs(AssemblyName name)
		{
			Name = name;
		}

		public ResolvingStep Step                 { get; internal set; }
		public AssemblyName  Name                 { get; internal set; }
		public Assembly?     RequestingAssembly   { get; internal set; }

		public string?       ResolvedAssemblyPath { get; internal set; }

		public Exception?    Exception            { get; internal set; }
		public Assembly?     ResolvedAssembly     { get; set; }
	}
}
