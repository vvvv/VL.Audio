using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.Audio;
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

            if (AsioInputDeviceDefinition.Instance.Entries.Count > 1) //"Default" entry is always there!
            {
                LoadConfiguration(FConfigFile);
            }
        }

        public void LoadConfiguration(string configurationFile)
        {
            //define some absolut baseline defaults for when there is no settings file yet
            var selectedDriver = defaultEntry;
            var selectedSamplerate = 48000;
            var selectedInputCount = 2;
            var selectedOutputCount = 2;
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

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/SampleRate");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedSamplerate);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/Inputs");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedInputCount);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/Outputs");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedOutputCount);

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

            var drivers = AsioInputDeviceDefinition.Instance;

            //make sure selected driver is currently available
            if (!drivers.Entries.Contains(selectedDriver))
                selectedDriver = defaultEntry;

            //if "Default" is selected, make an actual choice
            if (selectedDriver == defaultEntry)
                selectedDriver = drivers.GetDefaultDriver();

            try
            {
                Engine.PreviewDriver(selectedDriver);

                var sampleRates = AudioSampleRateDefinition.Instance.Entries.ToList();
                if (sampleRates.Contains(selectedSamplerate.ToString()))
                {
                    selectedInputCount = Math.Min(Engine.AsioOut.DriverInputChannelCount, selectedInputCount);
                    selectedOutputCount = Math.Min(Engine.AsioOut.DriverOutputChannelCount, selectedOutputCount);
                    Engine.ChangeDriverSettings(selectedDriver, selectedSamplerate, selectedInputCount, 0, selectedOutputCount, 0);

                    Engine.Timer.BPM = selectedTempo;
                    Engine.Timer.Loop = selectedLoop;
                    Engine.Timer.LoopStartBeat = selectedLoopStartBeat;
                    Engine.Timer.LoopEndBeat = selectedLoopEndBeat;

                    Engine.Play = true;
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
