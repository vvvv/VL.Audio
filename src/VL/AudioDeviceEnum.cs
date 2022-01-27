using System;
using System.Collections.Generic;
using VL.Lib;
using VL.Lib.Collections;
using NAudio.Wave;
using System.Linq;

namespace VL.Audio
{
    [Serializable]
    public class AudioDevice : DynamicEnumBase<AudioDevice, AudioDeviceDefinition>
    {
        public const string DefaultEntry = "Default";
        public AudioDevice(string value) : base(value)
        {
        }

        //this method needs to be imported in VL to set the default
        public static AudioDevice CreateDefault()
        {
            //use method of base class if nothing special required
            return CreateDefaultBase(DefaultEntry);
        }
    }

    public class AudioDeviceDefinition : DynamicEnumDefinitionBase<AudioDeviceDefinition>
    {
        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var driverNames = new Dictionary<string, object>();

            // Add a default entry which makes it up to the system to select a device
            driverNames[AudioDevice.DefaultEntry] = null;

            if (AudioService.OutputDrivers.Count > 0)
            {
                foreach (var driverName in AudioService.OutputDrivers)
                {
                    //the return dictionary holds the names of the entries as key with an optional "tag"
                    //here the tag is null but you can provide any object that you want to associate with the entry
                    driverNames[driverName] = null;
                }
            }
            else
                driverNames["No Audio Device Found!"] = null; 

            return driverNames;
        }

        public string GetDefaultDriver()
        {
            var inputDevice = Instance;

            //order of selection
            //prefer any asio device
            var defaultName = inputDevice.Entries.FirstOrDefault(e => e.StartsWith(AudioEngine.ASIOPrefix));

            //prefer system default WASAPI
            if (defaultName == null)
                defaultName = inputDevice.Entries.FirstOrDefault(e => e == AudioEngine.WasapiPrefix + AudioEngine.WasapiSystemDevice);

            return defaultName;
        }
    }

    [Serializable]
    public class WasapiInputDevice : DynamicEnumBase<WasapiInputDevice, WasapiInputDeviceDefinition>
    {
        public WasapiInputDevice(string value) : base(value)
        {
        }

        //this method needs to be imported in VL to set the default
        public static WasapiInputDevice CreateDefault()
        {
            //use method of base class if nothing special required
            return CreateDefaultBase("Default");
        }
    }

    public class WasapiInputDeviceDefinition : DynamicEnumDefinitionBase<WasapiInputDeviceDefinition>
    {
        const string defaultEntry = "Default";

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var driverNames = new Dictionary<string, object>();

            // Add a default entry which makes it up to the system to select a device
            driverNames[defaultEntry] = null;

            if (AudioService.WasapiInputDevices.Count > 0)
            {
                foreach (var driverName in AudioService.WasapiInputDevices)
                {
                    //the return dictionary holds the names of the entries as key with an optional "tag"
                    //here the tag is null but you can provide any object that you want to associate with the entry
                    driverNames[driverName] = null;
                }
            }
            else
                driverNames["No WASAPI Input Device Found! -> Check Your Drivers. See https://thegraybook.vvvv.org/reference/libraries/audio.html for options!"] = null;

            return driverNames;
        }

        public string GetDefaultDriver()
        {
            var inputDevice = Instance;
            var defaultName = AudioEngine.WasapiSystemDevice;
            if (AudioService.OutputDrivers.Count > 0)
                defaultName = AudioService.WasapiInputDevices[AudioService.WasapiInputDevicesDefaultIndex];

            return inputDevice.Entries.FirstOrDefault(e => e.StartsWith(defaultName)) ?? inputDevice.Entries.First(e => e != defaultEntry);
        }
    }
}