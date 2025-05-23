using System;

namespace HackerSimulator.Wasm.Core
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class OpenFileTypeAttribute : Attribute
    {
        public string[] Extensions { get; }
        public OpenFileTypeAttribute(params string[] extensions)
        {
            Extensions = extensions;
        }
    }
}
