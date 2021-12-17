using VL.Core;
using VL.Core.CompilerServices;
using VL.Lib.Basics.Resources;

// Tell VL where to find our initializer
[assembly: AssemblyInitializer(typeof(VL.Audio.Initialization))]

namespace VL.Audio
{
    public class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory factory)
        {
            factory.RegisterNodeFactory(nodeFactory);
            factory.RegisterService<NodeContext, IResourceProvider<GlobalEngine>>(n => ResourceProvider.NewPooledPerApp(n, () => new GlobalEngine()));
        }

        static IVLNodeDescriptionFactory nodeFactory = new NodeFactory();
    }
}
