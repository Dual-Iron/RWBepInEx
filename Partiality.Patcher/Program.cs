using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Partiality.Patcher
{
    public static class Program
    {
        internal static ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("PartPatch");

        internal static ModuleDefinition NewHooksModule { get; private set; }
        internal static TypeDefinition PartialityMod { get; private set; }

        public static IEnumerable<string> TargetDLLs { get { yield break; } }

#pragma warning disable IDE0060 // Remove unused parameter
        public static void Patch(AssemblyDefinition assembly) { }
#pragma warning restore IDE0060

        public static void Finish()
        {
            try
            {
                string hooksPath = Combine(Paths.PluginPath, "PartialityWrapper", "HOOKS-Assembly-CSharp.dll");
                NewHooksModule = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(hooksPath))).MainModule;
                string partPath = Combine(Paths.PluginPath, "PartialityWrapper", "Partiality.dll");
                PartialityMod = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(partPath))).MainModule?.GetType("Partiality.Modloader.PartialityMod");
            }
            catch (Exception e)
            {
                Logger.LogInfo("Could not get the required assemblies: " + e.Message);
                return;
            }

            var resolver = new PatcherAssemblyResolver();
            foreach (var path in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using (var asm = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(path)), new ReaderParameters { AssemblyResolver = resolver }))
                    {
                        foreach (var module in asm.Modules)
                        {
                            PatchOne(module);
                        }
                        asm.Write(path);
                    }
                }
                catch (BadImageFormatException) { }
            }

            NewHooksModule.Dispose();
            PartialityMod.Module.Assembly.Dispose();
        }

        private static void PatchOne(ModuleDefinition module)
        {
            // If it references HOOKS-Assembly-CSharp and uses Partiality, it might be legacy!
            // Note this code is very inefficient (LINQ and 2x the needed iterations!), but I doubt most mod assemblies have that many references, so meh.
            if (TryGetPartialityType(module, out var partType))
            {
                Logger.LogInfo("Patching " + partType.FullName);
                var patcher = new Patcher(module);
                patcher.SupportPartialityType(partType);
                patcher.IgnoreSecurity();
                if (module.AssemblyReferences.Any(r => r.Name == "HOOKS-Assembly-CSharp"))
                {
                    patcher.UpdateILReferences();
                }
            }
        }

        private static bool TryGetPartialityType(ModuleDefinition module, out TypeDefinition partType)
        {
            partType = null;
            if (module.CustomAttributes.Any(c => c.AttributeType.Name == nameof(PartialityPatchedAttribute)))
            {
                return false;
            }
            var ctor = module.ImportReference(markAttrCtor);
            module.CustomAttributes.Add(new CustomAttribute(ctor));
            foreach (var type in module.Types)
            {
                if (type.BaseType?.FullName == "Partiality.Modloader.PartialityMod")
                {
                    if (partType != null)
                    {
                        throw new NotImplementedException("Why does this Partiality mod have two different PartialityMod types?");
                    }
                    partType = type;
                }
            }
            return partType != null;
        }

        private static string Combine(string firstPath, params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                firstPath = Path.Combine(firstPath, paths[i]);
            }
            return firstPath;
        }

        private static readonly MethodBase markAttrCtor = typeof(PartialityPatchedAttribute).GetConstructors()[0];
    }
}
