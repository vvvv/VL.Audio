#nullable enable
using System.Collections.Immutable;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lib.Basics.Resources;

// Tell VL where to find our initializer
[assembly: AssemblyInitializer(typeof(VL.Audio.Initialization))]

namespace VL.Audio
{
    public class Initialization : AssemblyInitializer<Initialization>
    {
        private IResourceProvider<GlobalEngine>? _engineProvider;

        public override void Configure(AppHost appHost)
        {
            // Register the engine provider so patches can access it
            if (_engineProvider is null)
            {
                _engineProvider = ResourceProvider.NewPooledSystemWide("VL.Audio", _ => new GlobalEngine());
            }
            appHost.Services.RegisterService(_engineProvider);

            // Register our nodes
            appHost.RegisterNodeFactory("VL.Audio-Factory", nodeFactory =>
            {
                var nodes = ImmutableArray.CreateBuilder<IVLNodeDescription>();
                //sources
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(ADSRSignal), "ADSR", "Source", "Generates an ADSR envelope in 0..1 range", "", "envelope"));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(OscSignal), "Oscillator", "Source", "Creates an audio wave", "", "synthesis sine triangle square sawtooth wave"));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(NoiseSignal), "Noise", "Source", "Creates different types of noise", "", "pink white"));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(ValueToAudioSignal), "V2A", "Source", "Converts a value into a static audio signal", "", ""));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(GranulatorSignal), "Granulator2 (Internal)", "Source", "Reads grains from an audio file", "", "granular synthesis"));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(IFFTSignal), "IFFT", "Source", "Creates an audio signal from spectrum data", "", "additive synthesis inverse"));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(ValueSequenceSignal), "ValueSequence2 (Internal)", "Source", "Generates a sequence of values which are played back as an audio signal", "", "sequencer clip loop"));

                //filter
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(AnalogModelingFilterSignal), "Filter", "Filter", "Analog modeling filter", "", "moog"));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(OnePoleLowPassSignal), "IIR", "Filter", "Simple one pole lowpass filer, mainly for value smoothing", "", "onepole smoothing damper"));
                nodes.Add(new NodeDescription(nodeFactory, _engineProvider, typeof(WaveShaperSignal), "WaveShaper", "Filter", "Wave shaper to distort the audio signal", "", "distort saturate"));

                return new(nodes.ToImmutable());
            });

            base.Configure(appHost);
        }
    }
}
