using System;
using Grasshopper.Kernel;

namespace Lumbricus
{
    public class LumbricusInfo : GH_AssemblyInfo
    {
        public override string Name => "Lumbricus";
        public override string Description => "3D printing path visualization — sweep a nozzle profile along toolpaths to preview printed geometry.";
        public override Guid Id => new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        public override string AuthorName => "Lumbricus";
        public override string AuthorContact => "";
    }
}
