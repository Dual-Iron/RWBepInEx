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
        internal static ManualLogSource Logger { get; } = Logging.Logger.CreateLogSource("PartPatch");

        internal static ModuleDefinition NewHooksAssembly { get; private set; }

        static Program()
        {
            // Ensure all assemblies we want are loaded
            AssemblyPatcher.PatchAndLoad(Path.Combine(Paths.PluginPath, "PartialityWrapper"));
            AssemblyPatcher.PatchAndLoad(Paths.PluginPath);
        }

        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                // Snag this assembly!
                yield return "HOOKS-Assembly-CSharp.dll";

                // Just target all DLLs in the plugin directory.
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
                NewHooksAssembly = assembly.MainModule;
            }
            else
                foreach (var module in assembly.Modules)
                    // If it references HOOKS-Assembly-CSharp and uses Partiality, it might be legacy!
                    // Note this code is very inefficient (LINQ and 2x the needed iterations!), but I doubt most mod assemblies have that many references, so meh.
                    if (Relevant(module))
                    {
                        var patcher = new Patcher(module);
                        patcher.IgnoreAccessChecks();
                        patcher.UpdateMonoModHookNames();
                        patcher.Finish();
                    }
        }

        private static bool Relevant(ModuleDefinition module)
        {
            bool foundHookGen = false;
            bool foundPartiality = false;

            for (int i = 0; i < module.AssemblyReferences.Count; i++)
            {
                var asmRef = module.AssemblyReferences[i];

                if (!foundPartiality && asmRef.Name == "Partiality")
                {
                    foundPartiality = true;
                }
                if (!foundHookGen && asmRef.Name == "HOOKS-Assembly-CSharp")
                {
                    foundHookGen = true;
                }

                if (foundPartiality && foundHookGen)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
