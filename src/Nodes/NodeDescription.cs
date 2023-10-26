using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using VL.Core;
using System.Reactive.Linq;
using VL.Lib.Basics.Resources;

namespace VL.Audio
{
    sealed class NodeDescription : IVLNodeDescription, IInfo, ITaggedInfo
    {
        readonly IResourceProvider<GlobalEngine> _engineProvider;
        readonly ImmutableArray<string> _tags;
        readonly List<PinDescription> _inputs = new List<PinDescription>();
        readonly List<PinDescription> _outputs = new List<PinDescription>();

        public NodeDescription(IVLNodeDescriptionFactory factory, IResourceProvider<GlobalEngine> engineProvider, Type signalType, string name, string category, string summary, string remarks, string tags)
        {
            Factory = factory;
            _engineProvider = engineProvider;
            Signal = signalType;
            Name = name;
            Category = "Audio." + category;
            Summary = summary;
            Remarks = remarks;
            _tags = tags.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();

            using (var signal = (AudioSignal)Activator.CreateInstance(signalType))
            {
                foreach (var input in signal.InParams)
                    _inputs.Add(new PinDescription(input.Name, DoubleToSingleT(input.GetValueType()), DoubleToSingleV(input.GetDefaultValue()), ""));

                //if (category == "Filter")
                //    inputs.Add(new PinDescription("Apply", typeof(bool), true, ""));

                _outputs.Add(new PinDescription("Output", typeof(AudioSignal), null, ""));

                foreach (var output in signal.OutParams)
                    _outputs.Add(new PinDescription(output.Name, DoubleToSingleT(output.GetValueType()), DoubleToSingleV(output.GetDefaultValue()), ""));
            }
        }

        Type DoubleToSingleT(Type type)
        {
            if (type == typeof(double))
                return typeof(float);
            else
                return type;
        }

        object DoubleToSingleV(object value)
        {
            if (value != null && value.GetType() == typeof(double))
                return (float)(double)value;
            else
                return value;
        }

        public IVLNodeDescriptionFactory Factory { get; }

        public Type Signal { get; private set; }

        public string Name { get; private set; }

        public string Category { get; private set; }

        public bool Fragmented => false;

        public IReadOnlyList<IVLPinDescription> Inputs => _inputs;

        public IReadOnlyList<IVLPinDescription> Outputs => _outputs;

        public IEnumerable<Core.Diagnostics.Message> Messages
        {
            get
            {
                yield break;
            }
        }

        public string Summary { get; private set; }

        public string Remarks { get; private set; }

        public IObservable<object> Invalidated => Observable.Never<object>();

        ImmutableArray<string> ITaggedInfo.Tags => _tags;

        public IVLNode CreateInstance(NodeContext context)
        {
            var engineHandle = _engineProvider.GetHandle();
            return new AudioSignalNode(this, engineHandle, context);
        }

        public bool OpenEditor()
        {
            return false;
        }
    }
}
