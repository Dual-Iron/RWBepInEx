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
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace Partiality.Patcher
{
    public static class Program
    {
        private static readonly MethodBase markAttrCtor = typeof(PartialityPatchedAttribute).GetConstructors()[0];

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
                NewHooksModule = ReadFromMemory(Combine(Paths.PluginPath, "PartialityWrapper", "HOOKS-Assembly-CSharp.dll")).MainModule;
                PartialityMod = ReadFromMemory(Combine(Paths.PluginPath, "PartialityWrapper", "Partiality.dll")).MainModule?.GetType("Partiality.Modloader.PartialityMod");
                PatchPartiality(PartialityMod.Module);
            }
            catch (Exception e)
            {
                Logger.LogError ("Could not get the required assemblies: " + e);
                return;
            }

            var resolver = new PatcherAssemblyResolver();
            foreach (var path in Directory.GetFiles(Paths.PluginPath, "*.dll", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using (var asm = ReadFromMemory(path, new ReaderParameters { AssemblyResolver = resolver }))
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

        private static AssemblyDefinition ReadFromMemory(string path, ReaderParameters param = null)
        {
            return AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(path)), param ?? new ReaderParameters());
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

        private static void PatchPartiality(ModuleDefinition module)
        {
            if (module.CustomAttributes.Any(c => c.AttributeType.Name == nameof(PartialityPatchedAttribute)))
            {
                return;
            }
            var attrCtor = module.ImportReference(markAttrCtor);
            module.CustomAttributes.Add(new CustomAttribute(attrCtor));

            Logger.LogInfo("Correcting Partiality DLL...");

            // Make base type BaseUnityPlugin
            PartialityMod.IsAbstract = true;
            PartialityMod.BaseType = module.ImportReference(typeof(BaseUnityPlugin));

            var ctor = PartialityMod.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic);
            for (int i = 0; i < ctor.Body.Instructions.Count; i++)
            {
                if (ctor.Body.Instructions[i].Operand is MethodReference methodRef && methodRef.DeclaringType.Name == "Object" && methodRef.Name == ".ctor")
                {
                    var ctorInfo = typeof(BaseUnityPlugin).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    ctor.Body.Instructions[i].Operand = module.ImportReference(ctorInfo);
                }
            }

            // Neuter the LoadAllMods method
            var loadAllModsMethod = module.GetType("Partiality.Modloader.ModManager").Methods.FirstOrDefault(m => m.Name == "LoadAllMods");
            loadAllModsMethod.Body = new Mono.Cecil.Cil.MethodBody(loadAllModsMethod);
            loadAllModsMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            // Add safety cctor
            var partManager = module.GetType("Partiality.PartialityManager");
            var createInstance = partManager.Methods.FirstOrDefault(m => m.Name == "CreateInstance");
            var cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            cctor.Body = new Mono.Cecil.Cil.MethodBody(cctor);
            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Call, createInstance));
            cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            partManager.Methods.Add(cctor);

            module.Assembly.Write(Combine(Paths.PluginPath, "PartialityWrapper", "Partiality.dll"));
        }
    }
}
