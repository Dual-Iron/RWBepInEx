using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Runtime.CompilerServices;
using static Mono.Cecil.Cil.OpCodes;
using static System.Reflection.BindingFlags;

namespace BepInEx.Partiality.Patcher
{
    public static class Patcher
    {
        public static void UpdateMonoModHookNames(ModuleDefinition module)
        {
            try
            {
                new TypeScanner(new ReferenceTransformer()).TransformTypes(module);
            }
            catch (Exception e)
            {
                Program.Logger.LogError($"Failed updating MonoMod hook names: " + e);
            }
        }

        public static void IgnoreAccessChecks(AssemblyDefinition assembly)
        {
            // Thanks pastebee for this code
            // It's horrific but it works!
            try
            {
                var module = assembly.MainModule;

                var IgnoresAccessChecksToAttribute = new TypeDefinition("System.Runtime.CompilerServices", "IgnoresAccessChecksToAttribute", TypeAttributes.Public | TypeAttributes.BeforeFieldInit, module.ImportReference(typeof(Attribute)));
                IgnoresAccessChecksToAttribute.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(AttributeUsageAttribute).GetConstructor(new Type[] { typeof(AttributeTargets) })), new byte[] { 1, 0, 1, 0, 0, 0, 1, 0, 84, 2, 13, 65, 108, 108, 111, 119, 77, 117, 108, 116, 105, 112, 108, 101, 1 }));

                var backingField = new FieldDefinition("<AssemblyName>k__BackingField", FieldAttributes.Private | FieldAttributes.InitOnly, module.TypeSystem.String);
                IgnoresAccessChecksToAttribute.Fields.Add(backingField);
                backingField.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(CompilerGeneratedAttribute).GetConstructor(new Type[0]))));

                var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
                ctor.Parameters.Add(new ParameterDefinition("assemblyName", ParameterAttributes.None, module.TypeSystem.String));

                ILProcessor il = ctor.Body.GetILProcessor();
                il.Emit(Ldarg_0);
                il.Emit(Call, module.ImportReference(typeof(Attribute).GetConstructor(Public | NonPublic | Instance, null, new Type[0], null)));
                il.Emit(Ldarg_0);
                il.Emit(Ldarg_1);
                il.Emit(Stfld, backingField);
                il.Emit(Ret);

                IgnoresAccessChecksToAttribute.Methods.Add(ctor);

                var get_AssemblyName = new MethodDefinition("get_AssemblyName", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, module.TypeSystem.String);

                il = get_AssemblyName.Body.GetILProcessor();
                il.Emit(Ldarg_0);
                il.Emit(Ldfld, backingField);
                il.Emit(Ret);

                get_AssemblyName.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(CompilerGeneratedAttribute).GetConstructor(new Type[0]))));

                IgnoresAccessChecksToAttribute.Methods.Add(get_AssemblyName);

                var AssemblyName = new PropertyDefinition("AssemblyName", PropertyAttributes.None, module.TypeSystem.String);
                AssemblyName.GetMethod = get_AssemblyName;
                IgnoresAccessChecksToAttribute.Properties.Add(AssemblyName);

                module.Types.Add(IgnoresAccessChecksToAttribute);

                module.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(System.Security.UnverifiableCodeAttribute).GetConstructor(new Type[0]))));

                var dec = new SecurityDeclaration(SecurityAction.RequestMinimum);
                var attr = new SecurityAttribute(module.ImportReference(typeof(System.Security.Permissions.SecurityPermissionAttribute)));
                attr.Properties.Add(new CustomAttributeNamedArgument("SkipVerification", new CustomAttributeArgument(module.TypeSystem.Boolean, true)));
                dec.SecurityAttributes.Add(attr);
                assembly.SecurityDeclarations.Add(dec);
                assembly.CustomAttributes.Add(new CustomAttribute(ctor, new byte[] { 1, 0, 15, 65, 115, 115, 101, 109, 98, 108, 121, 45, 67, 83, 104, 97, 114, 112, 0, 0 }));
            }
            catch (Exception e)
            {
                Program.Logger.LogError($"Failed ignoring access modifiers: " + e);
            }
        }
    }
}
