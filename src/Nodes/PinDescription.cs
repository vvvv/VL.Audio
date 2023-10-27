using System;
using VL.Core;

namespace VL.Audio
{
    sealed class PinDescription : IVLPinDescription, IInfo
    {
        public PinDescription(string name, Type type, object defaultValue, string description)
        {
            Name = name;
            Type = type;
            DefaultValue = defaultValue;
            Summary = description;
        }

        public string Name { get; }
        public Type Type { get; }
        public object DefaultValue { get; }

        public string Summary { get; }

        public string Remarks => "";
    }
}
