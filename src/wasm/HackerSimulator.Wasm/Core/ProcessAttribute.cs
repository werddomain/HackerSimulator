using System;

namespace HackerSimulator.Wasm.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ProcessAttribute : Attribute
    {
        public string Name { get; }

        public ProcessAttribute(string name)
        {
            Name = name;
        }
    }
}
