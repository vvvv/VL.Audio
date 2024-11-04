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
            FConfigFile = GetConfigFile();

            if (AudioDeviceDefinition.Instance.Entries.Count > 1) //"Default" entry is always there!
            {
                LoadConfiguration(FConfigFile);
            }
        }

        // Tries to fetch user app data path from VLSession.Instance.UserAppDataFolder by reflection to avoid dependency on VLSession.Instance (not present in exported app as of 2022.5)
        static string GetConfigFile()
        {
            var langAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "VL.HDE");
            if (langAssembly is null)
                return null;
            var sessionType = langAssembly.GetType("VL.HDE.API");
            if (sessionType is null)
                return null;
            var userAppDataFolderProperty = sessionType.GetProperty("UserAppDataFolder");
            if (userAppDataFolderProperty is null)
                return null;
            var userAppDataFolder = userAppDataFolderProperty.GetValue(null) as string;
            if (userAppDataFolder is null)
                return null;
            return Path.Combine(userAppDataFolder, configFile);
        }

        public int DriverSettingsCount { get; private set; }
        public void AddDriverSettingsReference() { DriverSettingsCount += 1; }
        public void RemoveDriverSettingsReference() { DriverSettingsCount -= 1; }

        public int TimingSettingsCount { get; private set; }
        public void AddTimingSettingsReference() { TimingSettingsCount += 1; }
        public void RemoveTimingSettingsReference() { TimingSettingsCount -= 1; }

        public bool ShowScopeInTooltip { get; set; }

        public void LoadConfiguration(string configurationFile)
        {
            //define some absolut baseline defaults for when there is no settings file yet
            var selectedDriver = defaultEntry;
            var selectedWasapiInput = defaultEntry;
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

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/WASAPIInputName");
                if (node != null)
                    selectedWasapiInput = node.InnerText;

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/SampleRate");
                if (node != null)
                    int.TryParse(node.InnerText, out selectedSamplerate);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/InputChannels");
                if (node != null)
                {
                    int.TryParse(node.Attributes["Count"].Value, out selectedInputCount);
                    int.TryParse(node.Attributes["Offset"].Value, out selectedInputOffset);
                }

                node = doc.DocumentElement.SelectSingleNode("/Settings/Driver/OutputChannels");
                if (node != null)
                {
                    int.TryParse(node.Attributes["Count"].Value, out selectedOutputCount);
                    int.TryParse(node.Attributes["Offset"].Value, out selectedOutputOffset);
                }

                //node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/Tempo");
                //if (node != null)
                //    float.TryParse(node.InnerText, out selectedTempo);

                //node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/Loop");
                //if (node != null)
                //    bool.TryParse(node.InnerText, out selectedLoop);

                //node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/LoopStartBeat");
                //if (node != null)
                //    float.TryParse(node.InnerText, out selectedLoopStartBeat);

                //node = doc.DocumentElement.SelectSingleNode("/Settings/Timing/LoopEndBeat");
                //if (node != null)
                //    float.TryParse(node.InnerText, out selectedLoopEndBeat);

                node = doc.DocumentElement.SelectSingleNode("/Settings/Tooltip/ShowScope");
                if (node != null && bool.TryParse(node.InnerText, out var showScope))
                    ShowScopeInTooltip = showScope;
                else
                    ShowScopeInTooltip = true;
            }

            try
            {
                Engine.PreviewDriver(selectedDriver, selectedWasapiInput);
                var sampleRates = AudioSampleRateDefinition.Instance.Entries.ToList();
                
                // select one and only sample rate for wasapi
                if (sampleRates.Count == 1 && int.TryParse(sampleRates[0], out var rate))
                    selectedSamplerate = rate;

                if (sampleRates.Contains(selectedSamplerate.ToString()))
                {
                    Engine.ChangeDriverSettings(selectedDriver, selectedWasapiInput, selectedSamplerate, selectedInputCount, selectedInputOffset, selectedOutputCount, selectedOutputOffset);

                    Engine.Timer.BPM = selectedTempo;
                    Engine.Timer.Loop = selectedLoop;
                    Engine.Timer.LoopStartBeat = selectedLoopStartBeat;
                    Engine.Timer.LoopEndBeat = selectedLoopEndBeat;

                    Engine.Play = true; // should depend on application runtime state?
                }
            }
            catch
            { }
        }

        public void Dispose()
        {
            AudioService.DisposeEngine();
        }
    }
}
