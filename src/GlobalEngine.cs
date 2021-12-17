using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.Audio;

namespace VL.Audio
{
    public class GlobalEngine: IDisposable
    {
        public AudioEngine Engine { get; private set; }

        public GlobalEngine()
        {
            Engine = AudioService.Engine;

            var inputDevice = AsioInputDeviceDefinition.Instance;
            if (inputDevice.Entries.Count > 1) //default entry is always there!
            {
                var defaultDriverName = inputDevice.Entries[0];
                //FEngine.ChangeDriver(defaultDriverName, 0, 0);

                Engine.ChangeDriverSettings(defaultDriverName, 48000, 2, 0, 2, 0);

                Engine.Play = true;
            }
        }

        public void Dispose()
        {
            AudioService.DisposeEngine();
        }
    }
}
