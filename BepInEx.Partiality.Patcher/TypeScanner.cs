using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace BepInEx.Partiality.Patcher
{
    public class TypeScanner
    {
        private readonly ReferenceTransformer transformer;

        public TypeScanner(ReferenceTransformer relations)
        {
            transformer = relations;
        }

        /// <summary>
        /// Transforms references as necessary in the given assembly.
        /// </summary>
        /// <param name="module"></param>
        public void TransformTypes(ModuleDefinition module)
        {
            foreach (var type in module.Types)
            {
                try
                {
                    TransformType(type);
                }
                catch (Exception e)
                {
                    Program.Logger.LogError($"While scanning {type}: {e}");
                }
            }
        }

        private void TransformType(TypeDefinition type)
        {
            // Necessary evil
            foreach (var method in type.Methods)
            {
                TransformMethodDefinition(method);
            }
            foreach (var property in type.Properties)
            {
                TransformMethodDefinition(property.GetMethod);
                TransformMethodDefinition(property.SetMethod);
            }
            foreach (var field in type.Fields)
            {
                MutateTypeReference(field.FieldType);
            }
            foreach (var @event in type.Events)
            {
                TransformMethodDefinition(@event.AddMethod);
                TransformMethodDefinition(@event.RemoveMethod);
                TransformMethodDefinition(@event.InvokeMethod);
            }
        }

        private void TransformMethodDefinition(MethodDefinition method)
        {
            if (method == null)
                return;

            TransformMethodReference(method);

            if (!method.HasBody)
                return;

            foreach (var instr in method.Body.Instructions)
            {
                // If the operand is referencing a type (referencing orig_ or hook_ delegate), check it!
                if (instr.Operand is TypeReference typeRef)
                {
                    MutateTypeReference(typeRef);
                }
                // Or, if the operand is referencing a function (calling add_ or remove_ on events), check it, too!
                else if (instr.Operand is MethodReference methodRef)
                {
                    TransformMethodReference(methodRef);
                    transformer.DoTransform(methodRef);
                }
            }
        }

        private void TransformMethodReference(MethodReference method)
        {
            if (method == null)
                return;

            if (method.ReturnType != null)
                MutateTypeReference(method.ReturnType);

            foreach (var parameter in method.Parameters)
            {
                MutateTypeReference(parameter.ParameterType);
            }
        }

        private void MutateTypeReference(TypeReference type)
        {
            transformer.DoTransform(type);
        }
    }
}
