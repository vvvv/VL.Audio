using System;
using System.Collections.Generic;
using VL.Lib;
using VL.Lib.Collections;
using NAudio.Wave;

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
            return CreateDefaultBase();
        }
    }

    public class AsioInputDeviceDefinition : DynamicEnumDefinitionBase<AsioInputDeviceDefinition>
    {
        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return HardwareChangedEvents.HardwareChanged;
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            Dictionary<string, object> driverNames = new Dictionary<string, object>();

            foreach (var driverName in AsioOut.GetDriverNames())
            {
                //the return dictionary holds the names of the entries as key with an optional "tag"
                //here the tag is null but you can provide any object that you want to associate with the entry
                driverNames[driverName] = null;
            }

            if (driverNames.None())
                driverNames["No ASIO!? -> get yours from http://www.asio4all.org/"] = null; 

            return driverNames;
        }
    }
}
