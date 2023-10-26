#nullable enable
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lib.Basics.Resources;

// Tell VL where to find our initializer
[assembly: AssemblyInitializer(typeof(VL.Audio.Initialization))]

namespace VL.Audio
{
    public class Initialization : AssemblyInitializer<Initialization>
    {
        private IResourceProvider<GlobalEngine>? _engineProvider;
        private NodeFactory? _nodeFactory;

        public override void Configure(AppHost appHost)
        {
            // Register the engine provider so patches can access it
            if (_engineProvider is null)
            {
                _engineProvider = ResourceProvider.NewPooledSystemWide("VL.Audio", _ => new GlobalEngine());
            }
            appHost.Services.RegisterService(_engineProvider);

            // Register our node factory
            if (_nodeFactory is null)
            {
                _nodeFactory = new NodeFactory(_engineProvider);
            }
            appHost.NodeFactoryRegistry.RegisterNodeFactory(_nodeFactory);

            base.Configure(appHost);
        }
    }
}
