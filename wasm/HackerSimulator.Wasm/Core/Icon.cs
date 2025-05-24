using System;
using System.Text;

namespace HackerSimulator.Wasm.Core
{
    /// <summary>
    /// Helper to represent and render icons from various sources like Font Awesome,
    /// Iconic fonts, image files or data URIs. The icon can be implicitly
    /// converted to and from string using a simple descriptor syntax
    /// (e.g. "fa:save", "image:/imgs/save.png", "data:...").
    /// </summary>
    public class Icon
    {
        /// <summary>
        /// Type of icon represented.
        /// </summary>
        public IconType Type { get; }

        /// <summary>
        /// Raw value of the icon after the prefix.
        /// </summary>
        public string Value { get; }

        private Icon(IconType type, string value)
        {
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Parse an icon descriptor string into an <see cref="Icon"/> instance.
        /// </summary>
        public static Icon Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Icon text cannot be empty", nameof(text));

            var parts = text.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var prefix = parts[0].Trim().ToLowerInvariant();
                var value = parts[1];
                return prefix switch
                {
                    "fa" => new Icon(IconType.FontAwesome, value),
                    "iconic" => new Icon(IconType.Iconic, value),
                    "mat" => new Icon(IconType.Material, value),
                    "material" => new Icon(IconType.Material, value),
                    "mud" => new Icon(IconType.MudBlazor, value),
                    "lucide" => new Icon(IconType.Lucide, value),
                    "iconscout" => new Icon(IconType.IconScout, value),
                    "emoji" => new Icon(IconType.Emoji, value),
                    "image" => new Icon(IconType.Image, value),
                    "ico" => new Icon(IconType.Image, value),
                    "data" => new Icon(IconType.Data, value),
                    _ => new Icon(IconType.Image, text)
                };
            }
            return new Icon(IconType.Image, text);
        }

        /// <summary>
        /// Implicit conversion from string to <see cref="Icon"/>.
        /// </summary>
        public static implicit operator Icon(string text) => Parse(text);

        /// <summary>
        /// Implicit conversion from <see cref="Icon"/> to string using descriptor syntax.
        /// </summary>
        public static implicit operator string(Icon icon) => icon.ToString();

        /// <inheritdoc />
        public override string ToString()
        {
            var prefix = Type switch
            {
                IconType.FontAwesome => "fa",
                IconType.Iconic => "iconic",
                IconType.Material => "mat",
                IconType.MudBlazor => "mud",
                IconType.Lucide => "lucide",
                IconType.IconScout => "iconscout",
                IconType.Emoji => "emoji",
                IconType.Data => "data",
                _ => "image"
            };
            return $"{prefix}:{Value}";
        }

        /// <summary>
        /// Render the icon as an HTML string. Optional anonymous object can be
        /// provided to generate additional HTML attributes.
        /// </summary>
        public string ToHtml(object? htmlAttributes = null)
        {
            var attrs = BuildAttributes(htmlAttributes);
            return Type switch
            {
                IconType.FontAwesome => $"<i class=\"fa fa-{Value}\"{attrs}></i>",
                IconType.Iconic => $"<span class=\"iconic-{Value}\"{attrs}></span>",
                IconType.Material => $"<span class=\"material-icons\"{attrs}>{Value}</span>",
                IconType.MudBlazor => $"<span class=\"mud-icon material-icons\"{attrs}>{Value}</span>",
                IconType.Lucide => $"<i class=\"lucide lucide-{Value}\"{attrs}></i>",
                IconType.IconScout => $"<i class=\"uil uil-{Value}\"{attrs}></i>",
                IconType.Emoji => $"<span{attrs}>{Value}</span>",
                IconType.Data => $"<img src=\"{EnsureDataPrefix(Value)}\"{attrs} />",
                _ => $"<img src=\"{Value}\"{attrs} />"
            };
        }

        private static string EnsureDataPrefix(string data)
        {
            return data.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? data
                : $"data:{data}";
        }

        private static string BuildAttributes(object? htmlAttributes)
        {
            if (htmlAttributes == null) return string.Empty;
            var props = htmlAttributes.GetType().GetProperties();
            if (props.Length == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var prop in props)
            {
                var name = prop.Name;
                var value = prop.GetValue(htmlAttributes)?.ToString();
                if (value != null)
                {
                    sb.Append(' ') 
                      .Append(name) 
                      .Append("=\"") 
                      .Append(value) 
                      .Append('\"');
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Supported icon types.
    /// </summary>
    public enum IconType
    {
        FontAwesome,
        Iconic,
        Material,
        MudBlazor,
        Lucide,
        IconScout,
        Emoji,
        Image,
        Data
    }
}

