using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

using Xunit;
using Xunit.Abstractions;

namespace Scrutor.Tests
{
    #region Tests Data

    public interface IService { }
    public class ServiceImpl : IService { }
    public interface IDecorableService<TContext> { }
    public class SameContext { }
    public class DecorableServiceWrapper<TContext> : IDecorableService<TContext>
    {
        public DecorableServiceWrapper(IDecorableService<TContext> inner)
        {
            Inner = inner;
        }

        public IDecorableService<TContext> Inner { get; }
    }

    #endregion

    #region Benchmarks

    [MarkdownExporterAttribute.GitHub]
    [MemoryDiagnoser]
    [MinColumn]
    [MaxColumn]
    [SimpleJob(invocationCount: 10, launchCount: 1)]
    public class DecorationBenchmark
    {
        private const string DECORABLE_SERVICE_SOURCES_TEMPLATE = "public class DecorableService{0} : IDecorableService<SameContext> {{ }}";

        private IServiceCollection _services;

        [Params(1000, 10000)]
        public int TotalDescriptors { get; set; }

        [Params(3, 30, 300, 800)]
        public int TotalDecorable { get; set; }

        [IterationSetup]
        public void Setup()
        {
            // Considering only the worst case where our decorable services were added in the end of the registration process :|

#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
            var sc = new ServiceCollection();

            if (TotalDescriptors < TotalDecorable)
                return;

            for (int i = 0; i < (TotalDescriptors - TotalDecorable); ++i)
                sc.AddTransient<IService, ServiceImpl>();       

            string decorableServiceAssemblyName = "decorable_services";
            Assembly decorableServiceAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == decorableServiceAssemblyName).FirstOrDefault();
            if (decorableServiceAssembly == null)
            {
                var decorableServicesSourcesSB = new StringBuilder(@"
namespace Scrutor.Tests
{
");
                for (int i = 0; i < TotalDecorable; ++i)
                    decorableServicesSourcesSB.AppendLine(string.Format(DECORABLE_SERVICE_SOURCES_TEMPLATE, i));

                decorableServicesSourcesSB.Append(@"
}");

                SyntaxTree decorableServicesST = CSharpSyntaxTree.ParseText(decorableServicesSourcesSB.ToString());
                CSharpCompilation decorableServicesCU = CSharpCompilation.Create(decorableServiceAssemblyName,
                    new[] { decorableServicesST },
                    new[]
                    {
                    MetadataReference.CreateFromFile(typeof(DecorationBenchmark).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                    },
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                using (var ms = new MemoryStream())
                {
                    var result = decorableServicesCU.Emit(ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    decorableServiceAssembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                }
            }

            for (int i = 0; i < TotalDecorable; ++i)
            {
                Type implType = decorableServiceAssembly.GetType($"Scrutor.Tests.DecorableService{i}");

                sc.AddTransient(typeof(IDecorableService<SameContext>), implType);
            }

            _services = sc;
        }

        [IterationCleanup]
        public void Cleanup()
        {
            _services = null;
        }

        #region New benchmarks

        [Benchmark(Baseline = true)]
        public void TryDecorate_worst_case()
        {
            if (TotalDescriptors < TotalDecorable)
                return;

            _services.TryDecorate(typeof(IDecorableService<>), typeof(DecorableServiceWrapper<>));
        }

        #endregion

        #region Old benchmarks

        [Benchmark]
        public void TryDecorateOld_worst_case()
        {
            _services.TryDecorateOld(typeof(IDecorableService<>), typeof(DecorableServiceWrapper<>));
        }

        #endregion
    }

    #endregion
: TestBase
    {
        private readonly ITestOutputHelper _rOutput;

        public DecorationPerformanceTests(ITestOutputHelper output)
        {
            _rOutput = output;
        }

        [Fact]
        public void TryDecorate_worst_case_benchmark()
        {
#if DEBUG
            Summary summary = BenchmarkRunner.Run<DecorationBenchmark>(new DebugInProcessConfig());
#else
            Summary summary = BenchmarkRunner.Run<DecorationBenchmark>();
#endif

            _rOutput.WriteLine(summary.ToString());
        }

        [Fact]
        public void TryDecoratate_manual_benchmark()
        {
            IServiceCollection __GetSC()
            {
                string DECORABLE_SERVICE_SOURCES_TEMPLATE = "public class DecorableService{0} : IDecorableService<SameContext> {{ }}";

                var TotalDescriptors = 10000;
                var TotalDecorable = 100;

                var scC = new ServiceCollection();

                for (int i = 0; i < (TotalDescriptors - TotalDecorable); ++i)
                    scC.AddTransient<IService, ServiceImpl>();

                string decorableServiceAssemblyName = "decorable_services";
                Assembly decorableServiceAssembly = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == decorableServiceAssemblyName).FirstOrDefault();
                if (decorableServiceAssembly == null)
                {
                    var decorableServicesSourcesSB = new StringBuilder(@"
namespace Scrutor.Tests
{
");
                    for (int i = 0; i < TotalDecorable; ++i)
                        decorableServicesSourcesSB.AppendLine(string.Format(DECORABLE_SERVICE_SOURCES_TEMPLATE, i));

                    decorableServicesSourcesSB.Append(@"
}");

                    SyntaxTree decorableServicesST = CSharpSyntaxTree.ParseText(decorableServicesSourcesSB.ToString());
                    CSharpCompilation decorableServicesCU = CSharpCompilation.Create(decorableServiceAssemblyName,
                        new[] { decorableServicesST },
                        new[]
                        {
                    MetadataReference.CreateFromFile(typeof(DecorationBenchmark).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                        },
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                    using (var ms = new MemoryStream())
                    {
                        var result = decorableServicesCU.Emit(ms);
                        ms.Seek(0, SeekOrigin.Begin);

                        decorableServiceAssembly = AssemblyLoadContext.Default.LoadFromStream(ms);
                    }
                }

                for (int i = 0; i < TotalDecorable; ++i)
                {
                    Type implType = decorableServiceAssembly.GetType($"Scrutor.Tests.DecorableService{i}");

                    scC.AddTransient(typeof(IDecorableService<SameContext>), implType);
                }

                return scC;
            }

            var sc = __GetSC();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            sc.TryDecorate(typeof(IDecorableService<>), typeof(DecorableServiceWrapper<>));

            stopwatch.Stop();
            var res = stopwatch.Elapsed;

            _rOutput.WriteLine(res.ToString());


            sc = __GetSC();

            stopwatch = new Stopwatch();
            stopwatch.Start();

            sc.TryDecorate(typeof(IDecorableService<>), typeof(DecorableServiceWrapper<>));

            stopwatch.Stop();
            res = stopwatch.Elapsed;

            _rOutput.WriteLine(res.ToString());
        }
    }
}
