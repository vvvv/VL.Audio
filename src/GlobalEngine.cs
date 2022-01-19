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
        const string settingsFile = "VL.Audio.Settings.xml";

        public AudioEngine Engine { get; private set; }

        public GlobalEngine()
        {
            Engine = AudioService.Engine;

            var inputDevice = AsioInputDeviceDefinition.Instance;
            if (inputDevice.Entries.Count > 1) //"Default" entry is always there!
            {
                //define some absolut baseline defaults for when there is no settings file yet
                var selectedDriver = defaultEntry;
                var selectedSamplerate = 48000;
                var selectedInputCount = 2;
                var selectedOutputCount = 2;

                //check settings file for selected driver and settings
                var settingspath = Path.Combine(VLSession.Instance.UserAppDataFolder, settingsFile);
                if (File.Exists(settingspath))
                {
                    var doc = new XmlDocument();
                    doc.Load(settingspath);

                    var node = doc.DocumentElement.SelectSingleNode("/Settings/Driver");
                    if (node != null)
                        selectedDriver = node.InnerText;

                    node = doc.DocumentElement.SelectSingleNode("/Settings/SampleRate");
                    if (node != null)
                        int.TryParse(node.InnerText, out selectedSamplerate);

                    node = doc.DocumentElement.SelectSingleNode("/Settings/Inputs");
                    if (node != null)
                        int.TryParse(node.InnerText, out selectedInputCount);

                    node = doc.DocumentElement.SelectSingleNode("/Settings/Outputs");
                    if (node != null)
                        int.TryParse(node.InnerText, out selectedOutputCount);
                }

                //make sure selected driver is currently available
                if (!inputDevice.Entries.Contains(selectedDriver))
                    selectedDriver = defaultEntry;

                //if "Default" is selected, make an actual choice
                if (selectedDriver == defaultEntry)
                    selectedDriver = inputDevice.GetDefaultDriver();

                try
                {
                    Engine.ChangeDriver(selectedDriver, 0, 0);

                    var sampleRates = AudioSampleRateDefinition.Instance.Entries.ToList();
                    if (sampleRates.Contains(selectedSamplerate.ToString()))
                    {
                        selectedInputCount = Math.Min(Engine.AsioOut.DriverInputChannelCount, selectedInputCount);
                        selectedOutputCount = Math.Min(Engine.AsioOut.DriverOutputChannelCount, selectedOutputCount);
                        Engine.ChangeDriverSettings(selectedSamplerate, selectedInputCount, selectedOutputCount);
                        Engine.Play = true;
                    }
                }
                finally
                { }
            }
        }

        public void Dispose()
        {
            AudioService.DisposeEngine();
        }
    }
}
