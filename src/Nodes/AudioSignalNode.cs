using System;
using System.Collections.Generic;
using System.Linq;
using VL.Core;
using System.Reactive.Linq;
using VL.Lib.Basics.Resources;

namespace VL.Audio
{
    sealed class AudioSignalNode : FactoryBasedVLNode, IVLNode
    {
        AudioSignal FSignalInstance;
        IResourceHandle<GlobalEngine> FEngineHandle;

        class MyPin : IVLPin
        {
            public object Value { get; set; }
            public Type Type { get; set; }
            public string Name { get; set; }
        }

        readonly NodeDescription description;

        public AudioSignalNode(NodeDescription description, IResourceHandle<GlobalEngine> engineHandle, NodeContext nodeContext) : base(nodeContext)
        {
            this.description = description;
            FSignalInstance = (AudioSignal)Activator.CreateInstance(description.Signal);
            FEngineHandle = engineHandle;

            foreach (var input in description.Inputs)
                FInputsMap.Add(input.Name, new MyPin() { Name = input.Name, Type = input.Type, Value = input.DefaultValue });

            Inputs = FInputsMap.Values.ToArray();

            foreach (var output in description.Outputs)
                FOutputsMap.Add(output.Name, new MyPin() { Name = output.Name, Type = output.Type, Value = output.DefaultValue });

            Outputs = FOutputsMap.Values.ToArray();
        }

        public IVLNodeDescription NodeDescription => description;

        Dictionary<string, IVLPin> FInputsMap = new Dictionary<string, IVLPin>();
        Dictionary<string, IVLPin> FOutputsMap = new Dictionary<string, IVLPin>();

        public IVLPin[] Inputs { get; }

        public IVLPin[] Outputs { get; }

        public void Update()
        {
            var apply = true;
            //if (FInputsMap.TryGetValue("Apply", out var pin))
            //    apply = (bool)pin.Value;

            if (apply)
            {
                foreach (var param in FSignalInstance.InParams)
                    if (param.GetValueType() == typeof(double))
                        param.SetValue((double)(float)FInputsMap[param.Name].Value);
                    else
                        param.SetValue(FInputsMap[param.Name].Value);
                Outputs[0].Value = FSignalInstance;
            }
            else
                Outputs[0].Value = FInputsMap["Input"].Value;

            foreach (var param in FSignalInstance.OutParams)
                if (param.GetValueType() == typeof(double))
                    FOutputsMap[param.Name].Value = (float)(double)param.GetValue();
                else
                    FOutputsMap[param.Name].Value = param.GetValue();
        }

        public void Dispose()
        {
            FSignalInstance.Dispose();
            FEngineHandle.Dispose();
        }
    }
}
