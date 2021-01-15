using BepInEx;
using Mono.Cecil;
using System;
using System.Linq;

namespace Partiality.Patcher
{
    public static class DependencyAdder
    {
        private static readonly string[] ignored = new[]
        {
            "mscorlib", "System", "System.Core", "Assembly-CSharp", "UnityEngine", "HOOKS-Assembly-CSharp", "BepInEx", "Partiality"
        };

        public static void AddDependencies(ModuleDefinition module, AssemblyFile file, MetadataResolver metadataResolver)
        {
            // Add all needed attributes of BepInDependency.
            foreach (var reference in module.AssemblyReferences)
            {
                // Don't bother with types that are known to not be bep plugins.
                if (ignored.Contains(reference.Name))
                {
                    continue;
                }

                string guid = null;
                try
                {
                    // Resolve the assembly and search for a BepInPlugin GUID.
                    using (var asm = metadataResolver.AssemblyResolver.Resolve(reference))
                    {
                        guid = FindGuid(metadataResolver, asm);
                    }
                }
                catch (Exception e) { Program.Logger.LogError(e); }

                // If no GUID was found, then there isn't one.
                if (guid == null)
                {
                    continue;
                }

                Program.Logger.LogInfo("Adding dependency in " + file.plugin.Name + " for " + guid);

                Type depFlags = typeof(BepInDependency.DependencyFlags);
                var attrCtor = typeof(BepInDependency).GetConstructor(new[] { typeof(string), depFlags });
                var attrDep = new CustomAttribute(module.ImportReference(attrCtor));
                attrDep.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, guid));
                attrDep.ConstructorArguments.Add(new CustomAttributeArgument(module.ImportReference(depFlags), BepInDependency.DependencyFlags.HardDependency));
                file.plugin.CustomAttributes.Add(attrDep);
            }
        }

        private static string FindGuid(MetadataResolver metadataResolver, AssemblyDefinition asm)
        {
            foreach (var refModule in asm.Modules)
            {
                foreach (var type in refModule.Types)
                {
                    var ret = GetGuid(type, metadataResolver);
                    if (ret != null)
                        return ret;
                }
            }
            return null;
        }

        private static string GetGuid(TypeReference typeRef, MetadataResolver resolver)
        {
            bool IsBaseUnityPlugin(TypeDefinition type)
            {
                if (type.BaseType is null || type.BaseType.FullName == "System.Object")
                {
                    return false;
                }
                if (type.BaseType.FullName == "BepInEx.BaseUnityPlugin")
                {
                    return true;
                }
                return IsBaseUnityPlugin(resolver.Resolve(type.BaseType));
            }

            var typeDef = resolver.Resolve(typeRef);
            if (typeDef != null && !typeDef.IsAbstract && !typeDef.IsInterface && IsBaseUnityPlugin(typeDef))
            {
                var bepPlugin = typeDef.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.FullName == "BepInEx.BepInPlugin");
                if (bepPlugin != null && bepPlugin.ConstructorArguments[0].Value is string guid)
                {
                    return guid;
                }
            }
            return null;
        }
    }
}
