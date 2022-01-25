using System;
using System.Collections.Generic;
using VL.Lib;
using VL.Lib.Collections;
using NAudio.Wave;
using VVVV.Audio;

namespace VL.Audio
{
    public enum AudioSampleRateEnum
    {
        Hz8000 = 8000,
        Hz11025 = 11025,
        Hz16000 = 16000,
        Hz22050 = 22050,
        Hz32000 = 32000,
        Hz44056 = 44056,
        Hz44100 = 44100,
        Hz48000 = 48000,
        Hz88200 = 88200,
        Hz96000 = 96000,
        Hz176400 = 176400,
        Hz192000 = 192000,
        Hz352800 = 352800
    }

    [Serializable]
    public class AudioSampleRate : DynamicEnumBase<AudioSampleRate, AudioSampleRateDefinition>
    {
        public AudioSampleRate(string value) : base(value)
        {
        }

        //this method needs to be imported in VL to set the default
        public static AudioSampleRate CreateDefault()
        {
            //use method of base class if nothing special required
            return CreateDefaultBase();
        }
    }

    public class AudioSampleRateDefinition : DynamicEnumDefinitionBase<AudioSampleRateDefinition>
    {
        protected override IObservable<object> GetEntriesChangedObservable()
        {
            //TODO: Engine.DriverChanged
            return AudioService.Engine.DriverSettingsChanged;
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var samplingRates = new Dictionary<string, object>();

            foreach (var item in Enum.GetValues(typeof(AudioSampleRateEnum)))
                if (AudioService.Engine?.IsSampleRateSupported((int)item) ?? false)
                    samplingRates.Add(((int)item).ToString(), (int)item);

            //if (samplingRates.None())
            //    samplingRates["48000"] = 48000;

            return samplingRates;
        }

        protected override bool AutoSortAlphabetically => false;
    }
}
