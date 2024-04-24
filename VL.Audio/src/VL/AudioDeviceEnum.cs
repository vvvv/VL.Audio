using System;
using System.Collections.Generic;
using VL.Lib;
using VL.Lib.Collections;
using NAudio.Wave;
using System.Linq;
using VL.Core.CompilerServices;

namespace VL.Audio
{
    [Serializable]
    public class AudioDevice : DynamicEnumBase<AudioDevice, AudioDeviceDefinition>
    {
        public const string DefaultEntry = "Default";
        public AudioDevice(string value) : base(value)
        {
        }

        [CreateDefault]
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
            var outputDeviceEnum = Instance;

            //order of selection
            //prefer system default device
            var defaultName = outputDeviceEnum.Entries.FirstOrDefault(e => e == AudioEngine.WasapiPrefix + AudioEngine.WasapiSystemDevice);

            //prefer first available WASAPI output
            if (defaultName == null)
                defaultName = outputDeviceEnum.Entries.FirstOrDefault(e => e.StartsWith(AudioEngine.WasapiPrefix));

            //prefer first available ASIO output
            if (defaultName == null)
                defaultName = outputDeviceEnum.Entries.FirstOrDefault(e => e.StartsWith(AudioEngine.ASIOPrefix));

            return defaultName;
        }
    }

    [Serializable]
    public class WasapiInputDevice : DynamicEnumBase<WasapiInputDevice, WasapiInputDeviceDefinition>
    {
        public const string DefaultEntry = "Default";

        public WasapiInputDevice(string value) : base(value)
        {
        }

        [CreateDefault]
        public static WasapiInputDevice CreateDefault()
        {
            //use method of base class if nothing special required
            return CreateDefaultBase(DefaultEntry);
        }
    }

    public class WasapiInputDeviceDefinition : DynamicEnumDefinitionBase<WasapiInputDeviceDefinition>
    {
        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var driverNames = new Dictionary<string, object>();

            // Add a default entry which makes it up to the system to select a device
            driverNames[WasapiInputDevice.DefaultEntry] = null;

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
                driverNames["No WASAPI Input Device Found!"] = null;

            return driverNames;
        }

        public string GetDefaultDriver()
        {
            var inputDeviceEnum = Instance;

            //order of selection
            //prefer system default device
            var defaultName = inputDeviceEnum.Entries.FirstOrDefault(e => e == AudioEngine.WasapiSystemDevice);

            //prefer first available
            if (defaultName == null)
                defaultName = inputDeviceEnum.Entries.FirstOrDefault();

            return defaultName;
        }
    }
}