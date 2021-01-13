using System;
using System.Reflection;

namespace BepInExFix
{
    public static class AssemblyPatcher
    {
        private static Action<string> patchAndLoad;
        private static bool tried;

        private static Action<string> GetPatchAndLoadMethod()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly assembly = null;

            for (int i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetName().Name == "BepInEx.Preloader")
                {
                    assembly = assemblies[i];
                    break;
                }
            }

            var method = assembly?.GetType("BepInEx.Preloader.Patching.AssemblyPatcher")?.GetMethod("PatchAndLoad", new[] { typeof(string) });
            if (method == null)
                return null;

            return (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), method);
        }

        // Using constants as documentation, lol.
        // Prevents having to use an XML file for this tiny class.
        public const string PrepareAssembliesFrom_DOC = "Prepares all the assemblies in the specified directory. Call this before trying to get them through TargetDLLs in your patcher. " +
            "Should be done in the order that the assemblies are retrieved.";

        public static void PrepareAssembliesFrom(string directory)
        {
            if (patchAndLoad == null && !tried)
            {
                tried = true;
                patchAndLoad = GetPatchAndLoadMethod();
            }
            patchAndLoad?.Invoke(directory);
        }
    }
}
