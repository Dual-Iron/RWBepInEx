using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using Mono.Cecil;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BepInEx.Partiality.Patcher
{
    public static class Program
    {
        private static readonly string partDirectory = Directory.CreateDirectory(Paths.PluginPath + "\\..\\partiality").FullName;

        internal static ManualLogSource Logger { get; } = Logging.Logger.CreateLogSource("PartPatch");

        internal static AssemblyDefinition NewHooksAssembly { get; private set; }

        static Program()
        {
            // Ensure all assemblies we want are loaded
            AssemblyPatcher.PatchAndLoad(Path.Combine(Paths.PluginPath, "PartialityWrapper"));
            AssemblyPatcher.PatchAndLoad(Paths.PluginPath);
            AssemblyPatcher.PatchAndLoad(partDirectory);
        }

        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                // Snag this assembly!
                yield return "HOOKS-Assembly-CSharp.dll";

                // Also patch Partiality to fix its reference.
                yield return "Partiality.dll";

                // Just target all DLLs in the plugin and partiality directory.
                foreach (var item in Directory.GetFiles(partDirectory, "*.dll"))
                {
                    yield return Path.GetFileName(item);
                }
                foreach (var item in Directory.GetFiles(Paths.PluginPath, "*.dll"))
                {
                    yield return Path.GetFileName(item);
                }
            }
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            if (assembly.Name.Name == "HOOKS-Assembly-CSharp")
            {
                NewHooksAssembly = assembly;
            }
            else
            // If it references HOOKS-Assembly-CSharp and uses Partiality, it might be legacy!
            if (TryGetHooksReference(assembly.MainModule, out int oldIndex))
            {
                assembly.MainModule.AssemblyReferences[oldIndex] = AssemblyNameReference.Parse(NewHooksAssembly.FullName);
                if (assembly.MainModule.AssemblyReferences.Any(n => n.Name == "Partiality"))
                {
                    var patcher = new Patcher(assembly.MainModule);
                    patcher.IgnoreAccessChecks();
                    patcher.UpdateMonoModHookNames();
                    patcher.Finish();
                }
            }
        }
        
        private static bool TryGetHooksReference(ModuleDefinition module, out int index)
        {
            for (int i = 0; i < module.AssemblyReferences.Count; i++)
            {
                var asmRef = module.AssemblyReferences[i];
                if (asmRef.Name == "HOOKS-Assembly-CSharp")
                {
                    index = i;
                    return true;
                }
            }
            index = 0;
            return false;
        }
    }
}
