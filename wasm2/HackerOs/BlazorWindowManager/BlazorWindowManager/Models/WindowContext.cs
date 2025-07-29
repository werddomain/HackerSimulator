using BlazorWindowManager.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorWindowManager.Models
{
    internal class WindowContext
    {

        public WindowBase Window { get; internal set; } = default!;
        public WindowContent Content { get; internal set; } = default!;

        public WindowInfo Info { get; internal set; } = default!;
        public IServiceProvider ServiceProvider { get; internal set; } = default!;
        public IServiceScope serviceScope { get; internal set; } = default!;
        /// <summary>
        /// Component reference for content area
        /// </summary>
        public ElementReference ContentElement { get; internal set; }

        public bool Rendered { get; internal set; }
        public bool Disposed { get; internal set; } = false;
    }
}
