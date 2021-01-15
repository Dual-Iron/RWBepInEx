using BepInEx;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Partiality.Patcher
{
    public class DependencyResolver : PatcherResolver
    {
        private static readonly string[] paths = new[]
        {
            Paths.PluginPath, Path.Combine(Paths.PluginPath, "PartialityWrapper"), Paths.PatcherPluginPath, Paths.BepInExAssemblyDirectory, Paths.ManagedPath
        };

        private readonly List<AssemblyFile> assemblies;

        public DependencyResolver(List<AssemblyFile> assemblies)
        {
            this.assemblies = assemblies;
        }

        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            try
            {
                return base.Resolve(name, parameters);
            }
            catch
            {
                for (int i = 0; i < assemblies.Count; i++)
                {
                    if (assemblies[i].def.Name.Name == name.Name)
                    {
                        return assemblies[i].def;
                    }
                }

                for (int i = 0; i < paths.Length; i++)
                {
                    string path = Path.Combine(paths[i], name.Name + ".dll");
                    if (File.Exists(path))
                    {
                        return AssemblyDefinition.ReadAssembly(path, parameters);
                    }
                }
                throw;
            }
        }
    }
}
