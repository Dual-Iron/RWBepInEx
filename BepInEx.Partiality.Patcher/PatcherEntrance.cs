using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BepInEx.Partiality.Patcher
{
    public static class PatcherEntrance
    {
        private static readonly string partDirectory = Directory.CreateDirectory(Paths.PluginPath + "\\..\\partiality").FullName;

        static PatcherEntrance()
        {
            // Ensure all assemblies we want are loaded
            AssemblyPatcher.PatchAndLoad(Paths.PluginPath);
            AssemblyPatcher.PatchAndLoad(partDirectory);
        }

        public static IEnumerable<string> TargetDLLs
        {
            get
            {
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
            // If it references the old HOOKS-Assembly-CSharp, fix it!
            if (assembly.MainModule.AssemblyReferences.Any(n => n.FullName.StartsWith("HOOKS-Assembly-CSharp")))
            {
                var patcher = new Patcher(assembly.MainModule);
                patcher.UpdateMonoModHookNames();
                patcher.IgnoreAccessChecks();
                patcher.Finish();
            }
        }
    }
}
