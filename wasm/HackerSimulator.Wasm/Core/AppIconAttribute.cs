using System;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Attribute to specify the default icon for an application or process
    /// using the <see cref="Icon"/> helper string format.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AppIconAttribute : Attribute
    {
        public AppIconAttribute(string icon)
        {
            Icon = icon;
        }

        /// <summary>
        /// Icon string in the IconHelper descriptor format (e.g. "fa:save").
        /// </summary>
        public string Icon { get; }
    }
}
