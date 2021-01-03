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
    public static class Program
    {
        private static readonly string partDirectory = Directory.CreateDirectory(Paths.PluginPath + "\\..\\partiality").FullName;

        internal static readonly ManualLogSource logger = Logger.CreateLogSource("PartPatch");

        static Program()
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
            // If it references HOOKS-Assembly-CSharp and uses Partiality, it might be legacy!
            // Note this code is very inefficient (LINQ and 2x the needed iterations!), but I doubt most mod assemblies have that many references, so meh.
            if (assembly.MainModule.AssemblyReferences.Any(n => n.FullName.StartsWith("HOOKS-Assembly-CSharp")) &&
                assembly.MainModule.AssemblyReferences.Any(n => n.FullName.StartsWith("Partiality")))
            {
                var patcher = new Patcher(assembly.MainModule);
                patcher.IgnoreAccessChecks();
                patcher.UpdateMonoModHookNames();
                patcher.Finish();
            }
        }
    }
}
