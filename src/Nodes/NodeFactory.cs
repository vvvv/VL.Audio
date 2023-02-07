using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using VL.Core;
using VL.Lib.Collections;
using System.Reactive.Linq;
using VL.Lib.Basics.Resources;

namespace VL.Audio
{
    public class NodeFactory : IVLNodeDescriptionFactory
    {
        const string identifier = "VL.Audio-Factory";

        public readonly string Directory;
        public readonly string DirectoryToWatch;

        public NodeFactory(string directory = default, string directoryToWatch = default)
        {
            Directory = directory;
            DirectoryToWatch = directoryToWatch;

            var builder = ImmutableArray.CreateBuilder<IVLNodeDescription>();
            //sources
            builder.Add(new NodeDescription(this, typeof(ADSRSignal), "ADSR", "Source", "Generates an ADSR envelope in 0..1 range", "", "envelope"));
            builder.Add(new NodeDescription(this, typeof(OscSignal), "Oscillator", "Source", "Creates an audio wave", "", "synthesis sine triangle square sawtooth wave"));
            builder.Add(new NodeDescription(this, typeof(NoiseSignal), "Noise", "Source", "Creates different types of noise", "", "pink white"));
            builder.Add(new NodeDescription(this, typeof(ValueToAudioSignal), "V2A", "Source", "Converts a value into a static audio signal", "", ""));
            builder.Add(new NodeDescription(this, typeof(GranulatorSignal), "Granulator2 (Internal)", "Source", "Reads grains from an audio file", "", "granular synthesis"));
            builder.Add(new NodeDescription(this, typeof(IFFTSignal), "IFFT", "Source", "Creates an audio signal from spectrum data", "", "additive synthesis inverse"));
            builder.Add(new NodeDescription(this, typeof(ValueSequenceSignal), "ValueSequence2 (Internal)", "Source", "Generates a sequence of values which are played back as an audio signal", "", "sequencer clip loop"));

            //filter
            builder.Add(new NodeDescription(this, typeof(AnalogModelingFilterSignal), "Filter", "Filter", "Analog modeling filter", "", "moog"));
            builder.Add(new NodeDescription(this, typeof(OnePoleLowPassSignal), "IIR", "Filter", "Simple one pole lowpass filer, mainly for value smoothing", "", "onepole smoothing damper"));
            builder.Add(new NodeDescription(this, typeof(WaveShaperSignal), "WaveShaper", "Filter", "Wave shaper to distort the audio signal", "", "distort saturate"));
            NodeDescriptions = builder.ToImmutable();
        }

        public ImmutableArray<IVLNodeDescription> NodeDescriptions { get; }

        public string Identifier => "VL.Audio-Factory";

        public IObservable<object> Invalidated => Observable.Never<object>();

        public void Export(ExportContext exportContext)
        {

        }

        public IVLNodeDescriptionFactory ForPath(string path)
        {
            return null;
        }

        class NodeDescription : IVLNodeDescription, IInfo, ITaggedInfo
        {
            ImmutableArray<string> FTags;
            List<PinDescription> inputs = new List<PinDescription>();
            List<PinDescription> outputs = new List<PinDescription>();

            public NodeDescription(IVLNodeDescriptionFactory factory, Type signalType, string name, string category, string summary, string remarks, string tags)
            {
                Factory = factory;
                Signal = signalType;
                Name = name;
                Category = "Audio." + category;
                Summary = summary;
                Remarks = remarks;
                FTags = tags.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();
                
                using (var signal = (AudioSignal)Activator.CreateInstance(signalType))
                {
                    foreach (var input in signal.InParams)
                        inputs.Add(new PinDescription(input.Name, DoubleToSingleT(input.GetValueType()), DoubleToSingleV(input.GetDefaultValue()), ""));

                    //if (category == "Filter")
                    //    inputs.Add(new PinDescription("Apply", typeof(bool), true, ""));

                    outputs.Add(new PinDescription("Output", typeof(AudioSignal), null, ""));

                    foreach (var output in signal.OutParams)
                        outputs.Add(new PinDescription(output.Name, DoubleToSingleT(output.GetValueType()), DoubleToSingleV(output.GetDefaultValue()), ""));
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

            public IReadOnlyList<IVLPinDescription> Inputs => inputs;

            public IReadOnlyList<IVLPinDescription> Outputs => outputs;

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

            ImmutableArray<string> ITaggedInfo.Tags => FTags;

            public IVLNode CreateInstance(NodeContext context)
            {
                var engineHandle = context.Factory.CreateService<IResourceProvider<GlobalEngine>>(context).GetHandle();
                return new AudioSignalNode(this, engineHandle, context);
            }

            public bool OpenEditor()
            {
                return false;
            }
        }

        class PinDescription : IVLPinDescription, IInfo
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

        class AudioSignalNode : VLObject, IVLNode
        {
            AudioSignal FSignalInstance;
            IResourceHandle<GlobalEngine> FEngineHandle;

            class MyPin : IVLPin
            {
                public object Value { get; set; }
                public Type Type { get; set; }
                public string Name { get; set; }
            }

            readonly NodeDescription description;

            public AudioSignalNode(NodeDescription description, IResourceHandle<GlobalEngine> engineHandle, NodeContext nodeContext) : base(nodeContext)
            {
                this.description = description;
                FSignalInstance = (AudioSignal)Activator.CreateInstance(description.Signal);
                FEngineHandle = engineHandle;

                foreach (var input in description.Inputs)
                    FInputsMap.Add(input.Name, new MyPin() { Name = input.Name, Type = input.Type, Value = input.DefaultValue });

                Inputs = FInputsMap.Values.ToArray();

                foreach (var output in description.Outputs)
                    FOutputsMap.Add(output.Name, new MyPin() { Name = output.Name, Type = output.Type, Value = output.DefaultValue });
                
                Outputs = FOutputsMap.Values.ToArray();
            }

            public IVLNodeDescription NodeDescription => description;

            Dictionary<string, IVLPin> FInputsMap = new Dictionary<string, IVLPin>();
            Dictionary<string, IVLPin> FOutputsMap = new Dictionary<string, IVLPin>();

            public IVLPin[] Inputs { get; }

            public IVLPin[] Outputs { get; }

            public void Update()
            {
                var apply = true;
                //if (FInputsMap.TryGetValue("Apply", out var pin))
                //    apply = (bool)pin.Value;

                if (apply)
                {
                    foreach (var param in FSignalInstance.InParams)
                        if (param.GetValueType() == typeof(double))
                            param.SetValue((double)(float)FInputsMap[param.Name].Value);
                        else
                            param.SetValue(FInputsMap[param.Name].Value);
                    Outputs[0].Value = FSignalInstance;
                }
                else
                    Outputs[0].Value = FInputsMap["Input"].Value;

                foreach (var param in FSignalInstance.OutParams)
                    if (param.GetValueType() == typeof(double))
                        FOutputsMap[param.Name].Value = (float)(double)param.GetValue();
                    else
                        FOutputsMap[param.Name].Value = param.GetValue();
            }

            public void Dispose()
            {
                FSignalInstance.Dispose();
                FEngineHandle.Dispose();
            }
        }
    }
}
