using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx.Partiality.Patcher
{

    public class ReferenceTransformer
    {
        private static readonly string[] cautionPrefixes = new[] { "orig_", "hook_", "add_", "remove_" };

        private readonly Dictionary<string, string> types = new Dictionary<string, string>();

        /// <summary>
        /// Transforms the member's name, if need be.
        /// </summary>
        public void DoTransform(MemberReference member)
        {
            string input = member.Name;
            if (!ShouldTransform(input))
            {
                return;
            }
            if (types.TryGetValue(input, out string ret))
            {
                member.Name = ret;
                return;
            }
            member.Name = types[member.Name] = Transform(member);
        }

        private bool ShouldTransform(string name)
        {
            // Scan backwards for digits.
            for (int i = name.Length - 1; i >= 0; i--)
            {
                // Continue scanning if there's digits.
                if (char.IsDigit(name[i]))
                {
                    continue;
                }
                // If we reach an underscore, it might be significant! The hooks always used to end in _XXX where XXX is a number.
                if (name[i] == '_')
                {
                    break;
                }
                // Otherwise, no, it's not special.
                return false;
            }
            // Check for telltale MonoMod prefixes.
            foreach (var prefix in cautionPrefixes)
            {
                if (name.StartsWith(prefix))
                {
                    return true;
                }
            }
            return false;
        }

        // Tries to find the right new member name for the deprecated reference
        // Returns new member name as a string
        private string Transform(MemberReference member)
        {
            Program.logger.LogInfo("Deprecated member? " + member.GetType() + ": " + member.FullName);
            return member.Name;
        }
    }
}
