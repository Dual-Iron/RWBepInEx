using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BepInEx.Partiality.Patcher
{
    public static class Program
    {
        internal static ManualLogSource Logger { get; } = Logging.Logger.CreateLogSource("PartPatch");

        internal static ModuleDefinition NewHooksModule { get; private set; }

        static Program() { }

        public static IEnumerable<string> TargetDLLs { get { yield break; } }
        public static void Patch(AssemblyDefinition assembly) { }

        public static void Finish()
        {
            string hooksAsm = Utility.CombinePaths(Paths.PluginPath, "PartialityWrapper", "HOOKS-Assembly-CSharp.dll");

            NewHooksModule = AssemblyDefinition.ReadAssembly(hooksAsm).MainModule;

            string backups = Directory.CreateDirectory(Path.Combine(Paths.PluginPath, "Backups")).FullName;

            foreach (var file in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.TopDirectoryOnly))
            {
                AssemblyDefinition asm;
                try
                {
                    asm = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(file)));
                }
                catch { continue; }

                if (asm.CustomAttributes.Any(c => c.AttributeType.FullName == typeof(PartialityPatchedAttribute).FullName) ||
                    !asm.Modules.Any(m => m.Types.Any(t => t.BaseType?.FullName == "Partiality.Modloader.PartialityMod")))
                {
                    asm.Dispose();
                    continue;
                }

                Logger.LogInfo("-- Patching " + asm.Name.Name + " --");

                Patcher.IgnoreAccessChecks(asm);
                foreach (var module in asm.Modules) 
                {
                    // If it references HOOKS-Assembly-CSharp and uses Partiality, it might be legacy!
                    // Note this code is very inefficient (LINQ and 2x the needed iterations!), but I doubt most mod assemblies have that many references, so meh.
                    if (module.AssemblyReferences.Any(a => a.Name == "HOOKS-Assembly-CSharp"))
                    {
                        Patcher.UpdateMonoModHookNames(module);
                    }
                }

                string filename = Path.GetFileName(file);
                string backupPath = Path.Combine(backups, filename);
                try
                {
                    File.Delete(backupPath);
                    File.Move(file, backupPath);
                }
                catch (Exception e)
                {
                    Logger.LogWarning("Could not back up " + asm.Name.Name + ". Reason: " + e.Message);
                }

                // Successfully patched.
                var attr = typeof(PartialityPatchedAttribute).GetConstructors()[0];
                asm.CustomAttributes.Add(new CustomAttribute(asm.MainModule.ImportReference(attr)));

                asm.Write(file);
                asm.Dispose();
            }

            NewHooksModule.Assembly.Dispose();
        }
    }

    public sealed class PartialityPatchedAttribute : Attribute
    {

    }
}
