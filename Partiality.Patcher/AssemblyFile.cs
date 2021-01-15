using Mono.Cecil;

namespace Partiality.Patcher
{
    public struct AssemblyFile
    {
        public AssemblyDefinition def;
        public TypeDefinition plugin;
        public string path;
    }
}
