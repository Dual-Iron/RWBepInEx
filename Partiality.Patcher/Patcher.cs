using Mono.Cecil;
using Mono.Cecil.Cil;
using BepInEx;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using static Mono.Cecil.Cil.OpCodes;
using static Mono.Cecil.Cil.Instruction;

namespace Partiality.Patcher
{
    public partial class Patcher
    {
        private static int globalId;

        private readonly ReferenceTransformer types = new ReferenceTransformer();
        private readonly ModuleDefinition module;
        private readonly int id;

        public Patcher(ModuleDefinition module)
        {
            id = globalId++;
            this.module = module;
        }

        public void UpdateILReferences()
        {
            Program.Logger.LogInfo($"{id}: Updating MonoMod HookGen names");

            try
            {
                new TypeScanner(types).TransformTypes(module);
            }
            catch (Exception e)
            {
                Program.Logger.LogError($"{id}: Failed updating MonoMod hook names: " + e);
            }
        }

        public void IgnoreSecurity()
        {
            Program.Logger.LogInfo($"{id}: Ignoring accessibility violations");

            // Thank you pastebee for making this code.

            try
            {
                // This took me so many grueling hours of insanity and hair pulling to debug. There are no errors.
                // This attribute CANNOT be defined any other way, or the assembly doesn't load properly.
                AddIgnoreAccessChecksToAttr();

                // Also required.
                SkipVerification();
            }
            catch (Exception e)
            {
                Program.Logger.LogError($"{id}: Failed fixing accessibility violations: " + e);
            }
        }

        private void AddIgnoreAccessChecksToAttr()
        {
            // Create attribute type
            var IgnoresAccessChecksToAttribute = new TypeDefinition("System.Runtime.CompilerServices", "IgnoresAccessChecksToAttribute", TypeAttributes.Public | TypeAttributes.BeforeFieldInit, module.ImportReference(typeof(Attribute)));

            // AttributeUsage attribute on type
            var item = new CustomAttribute(module.ImportReference(typeof(AttributeUsageAttribute).GetConstructor(new Type[] { typeof(AttributeTargets) })), new byte[] { 1, 0, 1, 0, 0, 0, 1, 0, 84, 2, 13, 65, 108, 108, 111, 119, 77, 117, 108, 116, 105, 112, 108, 101, 1 });
            IgnoresAccessChecksToAttribute.CustomAttributes.Add(item);

            // Backing field for public AssemblyName { get; }
            var backingField = new FieldDefinition("<AssemblyName>k__BackingField", FieldAttributes.Private | FieldAttributes.InitOnly, module.TypeSystem.String);
            backingField.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes))));
            IgnoresAccessChecksToAttribute.Fields.Add(backingField);

            // Getter method for public AssemblyName { get; }
            var get_AssemblyName = new MethodDefinition("get_AssemblyName", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, module.TypeSystem.String);
            var il = get_AssemblyName.Body.GetILProcessor();
            il.Emit(Ldarg_0);
            il.Emit(Ldfld, backingField);
            il.Emit(Ret);
            get_AssemblyName.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(CompilerGeneratedAttribute).GetConstructor(new Type[0]))));
            IgnoresAccessChecksToAttribute.Methods.Add(get_AssemblyName);

            // Add final property definition for public AssemblyName { get; }
            var AssemblyName = new PropertyDefinition("AssemblyName", PropertyAttributes.None, module.TypeSystem.String)
            {
                GetMethod = get_AssemblyName
            };
            IgnoresAccessChecksToAttribute.Properties.Add(AssemblyName);

            // Constructor(string assemblyName)
            var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            ctor.Parameters.Add(new ParameterDefinition("assemblyName", ParameterAttributes.None, module.TypeSystem.String));
            il = ctor.Body.GetILProcessor();
            il.Emit(Ldarg_0);
            il.Emit(Call, module.ImportReference(typeof(Attribute).GetConstructor(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[0], null)));
            il.Emit(Ldarg_0);
            il.Emit(Ldarg_1);
            il.Emit(Stfld, backingField);
            il.Emit(Ret);
            IgnoresAccessChecksToAttribute.Methods.Add(ctor);

            // Add attribute type to the module
            module.Types.Add(IgnoresAccessChecksToAttribute);

            // Declare attribute type for the assembly
            module.Assembly.CustomAttributes.Add(new CustomAttribute(ctor, new byte[] { 1, 0, 15, 65, 115, 115, 101, 109, 98, 108, 121, 45, 67, 83, 104, 97, 114, 112, 0, 0 }));
        }

        private void SkipVerification()
        {
            // Add UnverifiableCode attribute
            module.CustomAttributes.Add(new CustomAttribute(module.ImportReference(typeof(System.Security.UnverifiableCodeAttribute).GetConstructor(new Type[0]))));

            // Skip security verification
            var dec = new SecurityDeclaration(SecurityAction.RequestMinimum);
            var attr = new SecurityAttribute(module.ImportReference(typeof(System.Security.Permissions.SecurityPermissionAttribute)));
            attr.Properties.Add(new CustomAttributeNamedArgument("SkipVerification", new CustomAttributeArgument(module.TypeSystem.Boolean, true)));
            dec.SecurityAttributes.Add(attr);
            module.Assembly.SecurityDeclarations.Add(dec);
        }

        public void SupportPartialityType(TypeDefinition partType)
        {
            Program.Logger.LogInfo($"{id}: Replacing Partiality mod: " + partType.FullName);

            AddCompanyAttr(partType);

            GeneratePluginClass(partType);

            for (int i = module.AssemblyReferences.Count - 1; i >= 0; i--)
            {
                if (module.AssemblyReferences[i].Name == "BepInEx" && module.AssemblyReferences[i].Version < new Version(5, 4, 0, 0))
                {
                    module.AssemblyReferences.RemoveAt(i);
                    break;
                }
            }
        }

        private void AddCompanyAttr(TypeDefinition partType)
        {
            if (module.CustomAttributes.Any(c => 
            c.AttributeType.FullName == "System.Reflection.AssemblyCompanyAttribute" || 
            c.AttributeType.FullName == "System.Reflection.AssemblyCopyrightAttribute"))
            {
                return;
            }

            foreach (var method in partType.Methods)
            {
                string author = null;
                if (method.HasBody)
                    foreach (var instr in method.Body.Instructions)
                    {
                        if (instr.OpCode.Code == Code.Ldstr && instr.Operand is string str && instr.Next.OpCode.Code == Code.Stfld && instr.Next.Operand is FieldReference fieldRef && fieldRef.Name == "author")
                        {
                            author = str;
                            break;
                        }
                    }

                if (author != null)
                {
                    var ctor = typeof(System.Reflection.AssemblyCompanyAttribute).GetConstructors()[0];
                    var customAttr = new CustomAttribute(module.ImportReference(ctor));
                    customAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, author));
                    module.Assembly.CustomAttributes.Add(customAttr);
                    return;
                }
            }
        }

        private void GeneratePluginClass(TypeDefinition pluginType)
        {
            // .. add attribute
            var attr = new CustomAttribute(module.ImportReference(typeof(BepInPlugin).GetConstructors()[0]));
            attr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, pluginType.FullName.ToLower()));
            attr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, pluginType.Name));
            attr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, module.Assembly.Name.Version.ToString()));
            pluginType.CustomAttributes.Add(attr);

            ModifyBaseCtorCall(pluginType);
            GenerateLoadOrder(pluginType);
        }

        private void ModifyBaseCtorCall(TypeDefinition pluginType)
        {
            // Second instruction will always be base ctor call.
            var baseCtor = typeof(BaseUnityPlugin).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
            var ctor = pluginType.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && m.Parameters.Count == 0);
            if (ctor == null)
            {
                ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
                ctor.Body = new MethodBody(ctor);
                ctor.Body.Instructions.Add(Create(Ldarg_0));
                ctor.Body.Instructions.Add(Create(Ret));
                ctor.Body.Instructions.Add(Create(Ret));
                pluginType.Methods.Add(ctor);
            }
            ctor.Body.Instructions[1] = Create(Call, module.ImportReference(baseCtor));
        }

        private void GenerateLoadOrder(TypeDefinition pluginType)
        {
            var on_enable = pluginType.Methods.FirstOrDefault(m => m.Name == "OnEnable" && m.Parameters.Count == 0);
            if (on_enable == null)
            {
                on_enable = new MethodDefinition("OnEnable", MethodAttributes.HideBySig | MethodAttributes.Private, module.TypeSystem.Void);
                on_enable.Body = new MethodBody(on_enable);
                on_enable.Body.Instructions.Add(Create(Ret));
                pluginType.Methods.Add(on_enable);
            }

            var first = on_enable.Body.Instructions[0];

            // .. add methodbody
            var il = on_enable.Body.GetILProcessor();

            // Partiality load order is Init -> Init -> OnLoad -> OnLoad -> OnEnable.

            var init = pluginType.Methods.FirstOrDefault(m => m.Name == "Init" && m.Parameters.Count == 0);
            if (init != null)
            {
                il.InsertBefore(first, Create(Ldarg_0));
                il.InsertBefore(first, Create(Call, module.ImportReference(init)));
            }

            var on_load = pluginType.Methods.FirstOrDefault(m => m.Name == "OnLoad" && m.Parameters.Count == 0);
            if (on_load != null)
            {
                il.InsertBefore(first, Create(Ldarg_0));
                il.InsertBefore(first, Create(Callvirt, module.ImportReference(on_load)));
            }

            // .. set bep plugin data
            var modIDField = module.ImportReference(Program.PartialityMod.Fields.First(f => f.Name == "ModID"));
            var versionField = module.ImportReference(Program.PartialityMod.Fields.First(f => f.Name == "Version"));

            var pluginInfo = module.ImportReference(typeof(BaseUnityPlugin).GetProperty("Info").GetGetMethod());
            var metadata = module.ImportReference(pluginInfo.ReturnType.Resolve().Methods.First(f => f.Name == "get_Metadata"));
            var name = module.ImportReference(metadata.ReturnType.Resolve().Methods.First(f => f.Name == "set_Name"));
            var version = module.ImportReference(metadata.ReturnType.Resolve().Methods.First(f => f.Name == "set_Version"));

            // .. set name
            il.Emit(Ldarg_0);
            il.Emit(Callvirt, pluginInfo);
            il.Emit(Callvirt, metadata);
            il.Emit(Ldarg_0);
            il.Emit(Ldfld, modIDField);
            il.Emit(Callvirt, name);

            // .. set version
            il.Emit(Ldarg_0);
            il.Emit(Callvirt, pluginInfo);
            il.Emit(Callvirt, metadata);
            il.Emit(Ldarg_0);
            il.Emit(Ldfld, versionField);
            il.Emit(Call, module.ImportReference(correctVersion));
            il.Emit(Callvirt, version);

            il.Emit(Ret);
        }

        public static System.Reflection.MethodInfo correctVersion = typeof(Patcher).GetMethod("CorrectVersion");
        public static Version CorrectVersion(string input)
        {
            if (!input.Contains("."))
            {
                var newS = new System.Text.StringBuilder();
                for (int i = 0; i < input.Length - 1; i++)
                {
                    newS.Append(input[i]);
                    newS.Append(".");
                }
                input = newS.ToString();
            }
            try
            {
                return new Version(input);
            }
            catch
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName()?.Version ?? new Version(0, 0);
            }
        }
    }
}
