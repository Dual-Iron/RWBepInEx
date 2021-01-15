using BepInEx;
using Mono.Cecil;
using System.Collections.Generic;
using System.IO;

namespace Partiality.Patcher
{
    public class PatcherResolver : DefaultAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters());
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name.Name == "HOOKS-Assembly-CSharp")
            {
                return Program.NewHooksModule.Assembly;
            }
            if (name.Name == "Partiality")
            {
                return Program.PartialityMod.Module.Assembly;
            }
            if (name.Name == "BepInEx")
            {
                return BepAsm;
            }
            return base.Resolve(name, parameters);
        }

        static AssemblyDefinition BepAsm => bepAsm ?? (bepAsm = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(Paths.BepInExAssemblyPath))));
        static AssemblyDefinition bepAsm;
    }
}