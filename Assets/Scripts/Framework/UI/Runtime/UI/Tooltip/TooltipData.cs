using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    public sealed class TooltipData
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public Sprite Icon { get; set; }
        public IReadOnlyList<TooltipValue> Values { get; set; }
        public string Footer { get; set; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(Title) &&
            string.IsNullOrWhiteSpace(Description) &&
            Icon == null &&
            (Values == null || Values.Count == 0) &&
            string.IsNullOrWhiteSpace(Footer);
    }

    public readonly struct TooltipValue
    {
        public TooltipValue(string label, string text)
            : this(label, text, Color.white)
        {
        }

        public TooltipValue(string label, string text, Color color)
        {
            Label = label;
            Text = text;
            Color = color;
        }

        public string Label { get; }
        public string Text { get; }
        public Color Color { get; }
    }
}
