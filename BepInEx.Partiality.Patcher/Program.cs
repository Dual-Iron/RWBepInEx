using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
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
            Logger.LogInfo("Initializing Partiality patcher");

            string hooksAsm = Utility.CombinePaths(Paths.PluginPath, "PartialityWrapper", "HOOKS-Assembly-CSharp.dll");

            NewHooksModule = AssemblyDefinition.ReadAssembly(hooksAsm).MainModule;

            string backups = Directory.CreateDirectory(Path.Combine(Paths.BepInExRootPath, "pluginBackups")).FullName;

            foreach (var file in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.TopDirectoryOnly))
            {
                AssemblyDefinition asm;
                try
                {
                    asm = AssemblyDefinition.ReadAssembly(file);
                }
                catch { continue; }

                if (asm.CustomAttributes.Any(c => c.AttributeType.Name == "_PartialityPorted"))
                {
                    asm.Dispose();
                    continue;
                }

                Logger.LogInfo("-- Patching " + asm.Name.Name + " --");

                Patcher.IgnoreAccessChecks(asm);

                foreach (var module in asm.Modules)
                {
                    if (module.AssemblyReferences.Any(a => a.Name == "HOOKS-Assembly-CSharp") &&
                        module.Types.Any(t => t.BaseType?.FullName == "Partiality.Modloader.PartialityMod"))
                    {
                        Patcher.UpdateMonoModHookNames(module);
                    }
                }

                // Successfully patched.
                var attrType = new TypeDefinition(asm.Name.Name, "_PartialityPorted", TypeAttributes.Sealed | TypeAttributes.NotPublic | TypeAttributes.AnsiClass | TypeAttributes.AutoLayout | TypeAttributes.BeforeFieldInit, asm.MainModule.ImportReference(typeof(Attribute)));
                var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, asm.MainModule.TypeSystem.Void);
                ctor.Body = new MethodBody(ctor);
                ctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                attrType.Methods.Add(ctor);
                asm.MainModule.Types.Add(attrType);
                asm.CustomAttributes.Add(new CustomAttribute(ctor));

                using (var ms = new MemoryStream())
                {
                    asm.Write(ms);
                    asm.Dispose();

                    string filename = Path.GetFileName(file);

                    try
                    {
                        File.Copy(file, Path.Combine(backups, filename), true);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning("Could not back up " + filename + ". Reason: " + e.Message);
                    }

                    File.WriteAllBytes(file, ms.ToArray());
                }
            }

            NewHooksModule.Assembly.Dispose();
            NewHooksModule = null;

            Logger.LogInfo("Finished Partiality patcher");
        }
    }
}
