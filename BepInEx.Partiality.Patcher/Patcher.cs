using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BepInEx.Partiality.Patcher
{
    public partial class Patcher
    {
        private static readonly MethodBase ignoreAccessChecksCtor = typeof(IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) });

        private static int globalId;

        private readonly ReferenceTransformer types = new ReferenceTransformer();
        private readonly ModuleDefinition module;
        private readonly int id;

        public Patcher(ModuleDefinition module)
        {
            id = globalId++;
            this.module = module;
        }

        public void UpdateMonoModHookNames()
        {
            Program.logger.LogInfo($"{id}: Updating MonoMod HookGen names");

            try
            {
                new TypeScanner(types).ApplyOverTypes(module);
            }
            catch (Exception e)
            {
                Program.logger.LogError($"{id}: Failed updating MonoMod hook names: " + e);
            }
        }

        public void IgnoreAccessChecks()
        {
            Program.logger.LogInfo($"{id}: Adding IgnoresAccessChecksToAttribute");

            try
            {
                var ctorRef = module.ImportReference(ignoreAccessChecksCtor);
                var item = new CustomAttribute(ctorRef);
                var attributeArg = new CustomAttributeArgument(module.TypeSystem.String, "Assembly-CSharp");

                item.ConstructorArguments.Add(attributeArg);

                module.Assembly.CustomAttributes.Add(item);
            }
            catch (Exception e)
            {
                Program.logger.LogError($"{id}: Failed adding attribute: " + e);
            }
        }

        public void Finish()
        {
        }
    }
}
