using System;
using Grasshopper.Kernel;

namespace Lumbricus
{
    public class LumbricusInfo : GH_AssemblyInfo
    {
        public override string Name => "Lumbricus";
        public override string Description => "3D printing path visualization — sweep a nozzle profile along toolpaths to preview printed geometry.";
        public override Guid Id => new Guid("CCD761C1-A1A2-41F6-9C15-CC0E8DFA826F");
        public override string AuthorName => "Lumbricus";
        public override string AuthorContact => "";
    }
}
