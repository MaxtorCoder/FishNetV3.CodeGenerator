using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace FishNet.CodeGenerating.ILCore
{
    internal static class ILCoreHelper
    {
        /// <summary>
        /// Returns AssembleDefinition for compiledAssembly.
        /// </summary>
        /// <returns></returns>
        internal static AssemblyDefinition GetAssemblyDefinition(ICompiledAssembly compiledAssembly, PostProcessorAssemblyResolver resolver)
        {
            var assemblyResolver = resolver ?? new PostProcessorAssemblyResolver(compiledAssembly);
            ReaderParameters readerParameters = new ReaderParameters
            {
                SymbolStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData),
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                AssemblyResolver = assemblyResolver,
                ReflectionImporterProvider = new PostProcessorReflectionImporterProvider(),
                ReadingMode = ReadingMode.Immediate
            };

            AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(compiledAssembly.InMemoryAssembly.PeData), readerParameters);
            //Allows us to resolve inside FishNet assembly, such as for components.
            assemblyResolver.AddAssemblyDefinitionBeingOperatedOn(assemblyDefinition);

            return assemblyDefinition;
        }


    }

}