using System;
using System.Collections.Generic;
using VL.Lib;
using VL.Lib.Collections;
using NAudio.Wave;
using System.Linq;

namespace VL.Audio
{
    [Serializable]
    public class AsioInputDevice : DynamicEnumBase<AsioInputDevice, AsioInputDeviceDefinition>
    {
        public AsioInputDevice(string value) : base(value)
        {
        }

        //this method needs to be imported in VL to set the default
        public static AsioInputDevice CreateDefault()
        {
            //use method of base class if nothing special required
            return CreateDefaultBase("Default");
        }
    }

    public class AsioInputDeviceDefinition : DynamicEnumDefinitionBase<AsioInputDeviceDefinition>
    {
        const string defaultEntry = "Default";

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var driverNames = new Dictionary<string, object>();

            if (AsioOut.GetDriverNames().Any())
            {
                // Add a default entry which makes it up to the system to select a device
                driverNames.Add("Default", default(string));

                foreach (var driverName in AsioOut.GetDriverNames())
                {
                    //the return dictionary holds the names of the entries as key with an optional "tag"
                    //here the tag is null but you can provide any object that you want to associate with the entry
                    driverNames[driverName] = null;
                }
            }
            else
                driverNames["ASIO driver missing. See https://thegraybook.vvvv.org/reference/libraries/audio.html for options!"] = null; 

            return driverNames;
        }

        public string GetDefaultDriver()
        {
            var inputDevice = AsioInputDeviceDefinition.Instance;
             //prefer "ASIO4ALL..", if not available use the first non-default entry
             return inputDevice.Entries.First(e => e.StartsWith("ASIO4ALL")) ?? inputDevice.Entries.First(e => e != defaultEntry);
        }
    }
}