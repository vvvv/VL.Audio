using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VL.Model;
using System.IO;
using System.Xml;

namespace VL.Audio
{
    public class GlobalEngine: IDisposable
    {
        const string defaultEntry = "Default";
        const string configFile = "VL.Audio.Configuration.xml";

        public AudioEngine Engine { get; private set; }

        string FConfigFile;
        public string ConfigurationFile => FConfigFile;

        public GlobalEngine()
        {
            Engine = AudioService.Engine;
            FConfigFile = Path.Combine(VLSession.Instance.UserAppDataFolder, configFile);

            if (AudioDeviceDefinition.Instance.Entries.Count > 1) //"Default" entry is always there!
            {
                LoadConfiguration(FConfigFile);
            }
        }

        public void LoadConfiguration(string configurationFile)
        {
            //define some absolut baseline defaults for when there is no settings file yet
            var selectedDriver = defaultEntry;
            var wasapiInput = defaultEntry;
            var selectedSamplerate = 48000;
            var selectedInputCount = 2;
            var selectedInputOffset = 0;
            var selectedOutputCount = 2;
            var selectedOutputOffset = 0;
            var selectedTempo = 120f;
            var selectedLoop = false;
            var selectedLoopStartBeat = 0f;
            var selectedLoopEndBeat = 0f;

            //check settings file for selected driver and settings
            if (File.Exists(configurationFile))
            {
                var doc = new XmlDocument();
                doc.Load(configurationFile);

                var node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/Name");
                if (node != null)
                    selectedDriver = node.InnerText;

                node = doc.DocumentElement.SelectSingleNode("/Settings/WasapiInput/Name");
                if (node != null)
                    wasapiInput = node.InnerText;

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/SampleRate");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedSamplerate);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/InputCount");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedInputCount);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/InputOffset");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedInputOffset);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/OutputCount");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedOutputCount);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/OutputOffset");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedOutputOffset);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/Tempo");
                if (node != null)
                    float.TryParse(node.InnerText, out selectedTempo);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/Loop");
                if (node != null)
                    bool.TryParse(node.InnerText, out selectedLoop);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/LoopStartBeat");
                if (node != null)
                    float.TryParse(node.InnerText, out selectedLoopStartBeat);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/LoopEndBeat");
                if (node != null)
                    float.TryParse(node.InnerText, out selectedLoopEndBeat);
            }

            var drivers = AudioDeviceDefinition.Instance;
            var wasapiInputs = WasapiInputDeviceDefinition.Instance;

            //make sure selected driver is currently available
            if (!drivers.Entries.Contains(selectedDriver))
                selectedDriver = defaultEntry;

            if (!wasapiInputs.Entries.Contains(wasapiInput))
                wasapiInput = defaultEntry;

            //if "Default" is selected, make an actual choice
            if (selectedDriver == defaultEntry)
                selectedDriver = drivers.GetDefaultDriver();

            if (wasapiInput == defaultEntry)
                wasapiInput = wasapiInputs.GetDefaultDriver();

            try
            {
                Engine.PreviewDriver(selectedDriver, wasapiInput);

                var sampleRates = AudioSampleRateDefinition.Instance.Entries.ToList();
                
                // select one and only sample rate for wasapi
                if (sampleRates.Count == 1 && int.TryParse(sampleRates[0], out var rate))
                    selectedSamplerate = rate;

                if (sampleRates.Contains(selectedSamplerate.ToString()))
                {
                    Engine.GetSupportedChannels(out var inputChannelCount, out var outputChannelCount);
                    selectedInputCount = Math.Min(inputChannelCount, selectedInputOffset + selectedInputCount);
                    selectedOutputCount = Math.Min(outputChannelCount, selectedOutputOffset + selectedOutputCount);
                    Engine.ChangeDriverSettings(selectedDriver, wasapiInput, selectedSamplerate, selectedInputCount, selectedInputOffset, selectedOutputCount, selectedOutputOffset);

                    Engine.Timer.BPM = selectedTempo;
                    Engine.Timer.Loop = selectedLoop;
                    Engine.Timer.LoopStartBeat = selectedLoopStartBeat;
                    Engine.Timer.LoopEndBeat = selectedLoopEndBeat;

                    Engine.Play = true; // should depend on application runtime state?
                }
            }
            finally
            { }
        }

        public void Dispose()
        {
            AudioService.DisposeEngine();
        }
    }
}
