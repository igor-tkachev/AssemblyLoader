using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

using AssemblyLoader;

using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace Tests
{
	[TestFixture]
	public class ResolverTests
	{
		[Test]
		public void Test([AssemblyPath] string assemblyPath)
		{
			using var loader = new Loader();

			loader.OnResolving += OnLoaderOnResolving;

			var assembly = loader.Load(assemblyPath);
			var test     = assembly.GetTypes().First(t => t.Name == "Test");

			test.GetMethod("Run")!.Invoke(null, new object[0]);
		}

		static void OnLoaderOnResolving(ResolvingEventArgs args)
		{
			var message = $"{args.Step}:\r\t{args.Name}\r\t{args.ResolvedAssembly?.Location ?? args.ResolvedAssemblyPath}";

			Debug.  WriteLine(message);
			Console.WriteLine(message);
		}

		public class AssemblyPathAttribute : NUnitAttribute, IParameterDataSource
		{
			public IEnumerable GetData(IParameterInfo parameter)
			{
				var frameworkName = GetFrameworkName();

				foreach (var projectDir in Directory.GetDirectories(GetTestProjectsFolder()))
				{
					var projectName  = Path.GetFileName(projectDir);

					if (projectName.StartsWith("Test"))
					{
						var assemblyName = Path.Combine(projectDir, "bin", "Debug", frameworkName, projectName + ".dll");

						if (File.Exists(assemblyName))
						{
							yield return assemblyName;
						}
					}
				}
			}
		}

		static string GetTestProjectsFolder()
		{
			var location     = typeof(ResolverTests).Assembly.Location;
			var dir          = Path.GetDirectoryName(location)!;
			var testProjects = Path.GetFullPath(Path.Combine(
				dir,
				"..", // net45, netcoreapp2.1, netcoreapp3.1
				"..", // Debug
				"..", // bin
				"..", // Tests
				"TestProjects"));

			return testProjects;
		}

		static string GetFrameworkName()
		{
			var location      = typeof(ResolverTests).Assembly.Location;
			var dir           = Path.GetDirectoryName(location)!;
			var frameworkName = Path.GetFileName(dir);

			return frameworkName;
		}

#if !NET461

		[Test]
		public void EachContextHasPrivateVersions()
		{
			using var json9Loader  = GetLoader("JsonNet9");
			using var json10Loader = GetLoader("JsonNet10");
			using var json11Loader = GetLoader("JsonNet11");

			var json9  = json9Loader. LoadAssembly(new AssemblyName("Newtonsoft.Json"));
			var json10 = json10Loader.LoadAssembly(new AssemblyName("Newtonsoft.Json"));
			var json11 = json11Loader.LoadAssembly(new AssemblyName("Newtonsoft.Json"));

			Assert.That(json9!. GetName().Version, Is.EqualTo(new Version("9.0.0.0")));
			Assert.That(json10!.GetName().Version, Is.EqualTo(new Version("10.0.0.0")));
			Assert.That(json11!.GetName().Version, Is.EqualTo(new Version("11.0.0.0")));

			Assert.That(
				json11.GetType("Newtonsoft.Json.JsonConvert", throwOnError: true), Is.Not.EqualTo(
				json10.GetType("Newtonsoft.Json.JsonConvert", throwOnError: true)));
			Assert.That(
				json10.GetType("Newtonsoft.Json.JsonConvert", throwOnError: true), Is.Not.EqualTo(
				json9. GetType("Newtonsoft.Json.JsonConvert", throwOnError: true)));

			static Loader GetLoader(string projectName)
			{
				var loader = new Loader();

				loader.OnResolving += OnLoaderOnResolving;

				var assemblyName = Path.Combine(GetTestProjectsFolder(), projectName, "bin", "Debug", GetFrameworkName(), projectName + ".dll");

				loader.Load(assemblyName);

				return loader;
			}
		}

#endif

#if !NET461

		[Test]
		public void Test1()
		{

			//var aaaa1 = System.AppContext.TargetFrameworkName;

			var a1 = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
			var a2 = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
			var a3 = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
			var a4 = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;

			var aaaa2 = System.AppContext.BaseDirectory;

			var aaaa3 = System.Reflection.Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
//			var aaaa4 = System.AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;

			var ca = Assembly.GetCallingAssembly();


			var dir = Path.GetDirectoryName(typeof(System.Runtime.GCSettings)!.GetTypeInfo()!.Assembly!.Location)!.Replace(@"file:\", "");

			var files = Directory.GetFiles(dir, "*.deps.json");
			if (files.Length == 0)
				return;

			// Read JSON content
			var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(dir, files[0])));
			var name = json["runtimeTarget"]["name"].ToString();

			// Read RID after slash
			var slashPos = name.LastIndexOf('/');

			if (slashPos == -1)
				return;

			var aaa = name.Substring(slashPos + 1);

//			var domaininfo = new AppDomainSetup
//			{
//				ApplicationBase = "f:\\work\\development\\latest"
//			};
//
//			AppDomain domain = AppDomain.CreateDomain("MyDomain", null, domaininfo);
//
//			// Write application domain information to the console.
//			Console.WriteLine("Host domain: " + AppDomain.CurrentDomain.FriendlyName);
//			Console.WriteLine("child domain: " + domain.FriendlyName);
//			Console.WriteLine("Application base is: " + domain.SetupInformation.ApplicationBase);
//
//			// Unload the application domain.
//			AppDomain.Unload(domain);
		}

#endif
	}
}
