using BepInEx;
using Mono.Cecil;
using System.Collections.Generic;
using System.IO;

namespace Partiality.Patcher
{
    public class PatcherAssemblyResolver : DefaultAssemblyResolver
    {
        public PatcherAssemblyResolver()
        {
        }

        public override AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters());
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            AssemblyDefinition results = null;
            if (results == null)
            {
                if (name.Name == "BepInEx")
                {
                    return BepAsm;
                }
                var pluginPath = Path.Combine(Paths.PluginPath, name.Name);
                if (File.Exists(pluginPath))
                {
                    try
                    {
                        return AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(pluginPath)));
                    }
                    catch { }
                }
            }
            return base.Resolve(name, parameters);
        }

        static AssemblyDefinition BepAsm => bepAsm ?? (bepAsm = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(Paths.BepInExAssemblyPath))));
        static AssemblyDefinition bepAsm;
    }
}