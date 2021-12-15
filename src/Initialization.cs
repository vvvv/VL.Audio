using VL.Core;
using VL.Core.CompilerServices;

// Tell VL where to find our initializer
[assembly: AssemblyInitializer(typeof(VL.Audio.Initialization))]

namespace VL.Audio
{
    public class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory factory)
        {
            factory.RegisterNodeFactory(nodeFactory);
        }

        static IVLNodeDescriptionFactory nodeFactory = new NodeFactory();
    }
}
