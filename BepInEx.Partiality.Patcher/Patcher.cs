using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BepInEx.Partiality.Patcher
{
    public partial class Patcher
    {
        private readonly ModuleDefinition module;
        private readonly int id;

        private static readonly MethodBase ignoreAccessChecksCtor = typeof(IgnoresAccessChecksToAttribute).GetConstructor(new[] { typeof(string) });
        private static readonly ManualLogSource logger = Logger.CreateLogSource("PartPatch");

        private static int globalId;

        public Patcher(ModuleDefinition module)
        {
            id = globalId++;
            this.module = module;
        }

        public void UpdateMonoModHookNames()
        {
            logger.LogInfo("${id}: Updating MonoMod HookGen names");

            try
            {

            }
            catch (Exception e)
            {
                logger.LogError($"{id}: Failed updating MonoMod hook names: " + e);
            }
        }

        public void IgnoreAccessChecks()
        {
            logger.LogInfo($"{id}: Adding IgnoresAccessChecksToAttribute");

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
                logger.LogError($"{id}: Failed adding attribute: " + e);
            }
        }

        public void Finish()
        {
        }
    }
}
