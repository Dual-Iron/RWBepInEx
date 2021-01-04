using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx.Partiality.Patcher
{
    public class ReferenceTransformer
    {
        struct NameDetails
        {
            public PrefixType prefix;
            public int fnIndex;
            public int underscoreIndex;
        }

        enum PrefixType
        {
            orig_, hook_, add_, remove_
        }

        private readonly Dictionary<string, string> types = new Dictionary<string, string>();

        /// <summary>
        /// Transforms the member's name, if need be.
        /// </summary>
        public void DoTransform(MemberReference member)
        {
            var input = member.Name;
            if (member.DeclaringType == null)
            {
                // MonoMod hookgen types and methods ALWAYS have a declaring type, because methods can only exist inside types.
                return;
            }
            var details = ShouldTransform(input);
            if (!details.HasValue)
            {
                return;
            }
            if (types.TryGetValue(input, out string ret))
            {
                member.Name = ret;
                return;
            }

            member.Name = types[member.Name] = Transform(member, details.Value);
        }

        private NameDetails? ShouldTransform(string name)
        {
            NameDetails details = default;

            // Scan backwards for digits.
            for (int i = name.Length - 1; i >= 0; i--)
            {
                // Continue scanning if there's digits.
                if (char.IsDigit(name[i]))
                {
                    continue;
                }
                // If we reach an underscore, it might be significant! The hooks always used to end in _XXX where XXX is a number.
                if (name[i] == '_' && int.TryParse(name.Substring(i + 1), out details.fnIndex))
                {
                    details.underscoreIndex = i;
                    break;
                }
                // Otherwise, no, it's not special.
                return null;
            }
            // Check for telltale MonoMod prefixes.
            foreach (var prefix in (PrefixType[])Enum.GetValues(typeof(PrefixType)))
            {
                if (name.StartsWith(prefix.ToString("g")))
                {
                    details.prefix = prefix;
                    return details;
                }
            }
            return null;
        }

        // Tries to find the right new member name for the deprecated reference
        // Returns new member name as a string
        private string Transform(MemberReference member, NameDetails details)
        {
            var newHooks = Program.NewHooksAssembly.MainModule;
            var newHooksType = newHooks.GetType(member.DeclaringType.FullName);

            string culledName = member.Name.Substring(0, details.underscoreIndex);

            Program.Logger.LogInfo("Finding match for " + member.FullName);

            // This code may be gross, but at least it's compact & efficient!
            // Start with an index of 0. Count up with each identical hook name we find, and once the number matches the old number, we have the right overload.
            if (member is TypeReference)
            {
                int index = 0;
                foreach (var type in newHooksType.NestedTypes)
                    if (type.Name.StartsWith(culledName) && index++ == details.fnIndex)
                        return type.Name;
            }
            else if (member is MethodReference)
            {
                int index = 0;
                if (details.prefix == PrefixType.add_)
                {
                    foreach (var @event in newHooksType.Events)
                        if (@event.AddMethod.Name.StartsWith(culledName))
                            if (index++ == details.fnIndex)
                                return @event.AddMethod.Name;
                }
                else
                {
                    foreach (var @event in newHooksType.Events)
                        if (@event.RemoveMethod.Name.StartsWith(culledName))
                            if (index++ == details.fnIndex)
                                return @event.RemoveMethod.Name;
                }
            }

            Program.Logger.LogWarning("Could not find a match for " + member.FullName + "!");
            return member.Name;
        }
    }
}
