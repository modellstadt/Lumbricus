using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;

namespace Lumbricus
{
    /// <summary>
    /// Sweeps a closed profile polyline along toolpath polylines to generate
    /// meshes visualizing 3D-printed bead geometry. Supports closed paths,
    /// optional end caps, and UV texture coordinates for material mapping.
    /// </summary>
    public class ProfileSweepComponent : GH_Component
    {
        public ProfileSweepComponent()
            : base(
                "Profile Sweep",
                "PrSweep",
                "Sweep a nozzle profile along 3D-print toolpaths to visualize bead geometry.",
                "Lumbricus",
                "3DPrint")
        { }

        public override Guid ComponentGuid =>
            new Guid("b2c3d4e5-f6a7-8901-bcde-f12345678901");

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                var stream = GetType().Assembly.GetManifestResourceStream("Lumbricus.Resources.ProfileSweep.png");
                if (stream != null)
                    return new System.Drawing.Bitmap(stream);
                return null;
            }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Polylines", "P", "Toolpath polylines to sweep along. If empty, shows demo vase.", GH_ParamAccess.list);
            pManager[0].Optional = true;
            pManager.AddCurveParameter("Profile", "Pr", "Closed profile polyline (nozzle cross-section). If empty, uses default elliptical bead.", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Reverse Profile", "Rev", "Reverse the profile winding direction.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("Cap", "C", "Add end caps to open paths.", GH_ParamAccess.item, true);
            pManager.AddBooleanParameter("Use UV", "UV", "Generate texture coordinates (U along path, V across paths).", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Progress", "T", "Print progress (0.0–1.0). Controls how much of the total toolpath is extruded, walking through all polylines sequentially.", GH_ParamAccess.item, 1.0);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Meshes", "M", "Swept mesh per toolpath.", GH_ParamAccess.list);
            pManager.AddTextParameter("Version", "V", "Component version.", GH_ParamAccess.item);
        }

        private const string VERSION = "0.2.0";

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Read inputs — all optional, demo fills in when missing
            bool reverseProfile = false;
            bool cap = true;
            bool useUV = false;
            double progress = 1.0;

            DA.GetData(2, ref reverseProfile);
            DA.GetData(3, ref cap);
            DA.GetData(4, ref useUV);
            DA.GetData(5, ref progress);
            progress = Math.Max(0.0, Math.Min(1.0, progress));

            // Try to read polylines
            var polylines = new List<Polyline>();
            var curves = new List<Curve>();
            if (DA.GetDataList(0, curves))
            {
                foreach (var crv in curves)
                {
                    if (crv == null) continue;
                    if (crv.TryGetPolyline(out Polyline pl))
                        polylines.Add(pl);
                    else
                    {
                        var polyCrv = crv.ToPolyline(0, 0, 0.1, 0, 0, 0, 0, 0, true);
                        if (polyCrv != null && polyCrv.TryGetPolyline(out Polyline approx))
                            polylines.Add(approx);
                    }
                }
            }

            // Try to read profile
            Polyline profile = new Polyline();
            Curve profileCurve = null;
            if (DA.GetData(1, ref profileCurve) && profileCurve != null)
            {
                if (!profileCurve.TryGetPolyline(out profile))
                {
                    var polyCrv = profileCurve.ToPolyline(0, 0, 0.01, 0, 0, 0, 0, 0, true);
                    if (polyCrv != null)
                        polyCrv.TryGetPolyline(out profile);
                }
            }

            // ── Demo geometry when inputs missing ──
            if (polylines.Count == 0 || !profile.IsValid || profile.Count < 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "No inputs — showing demo vase.");
                GenerateDemo(out polylines, out profile);
            }

            var meshes = SweepProfile(polylines, profile, reverseProfile, cap, useUV, progress);
            DA.SetDataList(0, meshes);
            DA.SetData(1, VERSION);
        }

        /// <summary>
        /// Generates demo geometry: a cylindrical vase (stacked circular layers)
        /// with a small elliptical nozzle profile. Shown when no inputs are connected.
        /// </summary>
        private void GenerateDemo(out List<Polyline> polylines, out Polyline profile)
        {
            polylines = new List<Polyline>();
            int layers = 30;
            double radius = 20.0;
            double layerHeight = 0.8;
            int segments = 48;

            for (int layer = 0; layer < layers; layer++)
            {
                double z = layer * layerHeight;
                // Slight radius variation for a vase shape
                double r = radius + 3.0 * Math.Sin(z * 0.15);
                var pl = new Polyline();
                for (int i = 0; i <= segments; i++)
                {
                    double angle = 2.0 * Math.PI * i / segments;
                    pl.Add(new Point3d(r * Math.Cos(angle), r * Math.Sin(angle), z));
                }
                polylines.Add(pl);
            }

            // Elliptical nozzle profile (wider than tall)
            profile = new Polyline();
            double pw = 2.0;  // bead width
            double ph = 0.6;  // bead height
            int profSegs = 12;
            for (int i = 0; i <= profSegs; i++)
            {
                double angle = 2.0 * Math.PI * i / profSegs;
                profile.Add(new Point3d(pw * 0.5 * Math.Cos(angle), ph * 0.5 * Math.Sin(angle), 0));
            }
        }

        /// <summary>
        /// Core sweep algorithm: for each toolpath polyline, build a ring of
        /// profile vertices at each frame, then stitch quads between rings.
        /// </summary>
        private List<Mesh> SweepProfile(
            List<Polyline> polylines,
            Polyline profile,
            bool reverseProfile,
            bool cap,
            bool useUV,
            double progress)
        {
            var meshes = new List<Mesh>();

            if (polylines == null || polylines.Count == 0)
                return meshes;

            if (!profile.IsValid || profile.Count < 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Profile not valid or too few points.");
                return meshes;
            }
            if (!profile.IsClosed)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Profile must be a closed polyline.");
                return meshes;
            }

            var prof = new Polyline(profile);
            if (reverseProfile)
                prof.Reverse();

            int ringCount = prof.Count - 1; // closed polyline repeats first point
            if (ringCount < 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Profile must have at least 3 distinct points.");
                return meshes;
            }

            double tol = RhinoDoc.ActiveDoc != null
                ? RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
                : 1e-6;

            // ── Compute total length across all polylines ──
            var pathLengths = new double[polylines.Count];
            double totalLength = 0;
            for (int i = 0; i < polylines.Count; i++)
            {
                var pl = polylines[i];
                if (pl == null || !pl.IsValid || pl.Count < 2) continue;
                double len = 0;
                for (int j = 1; j < pl.Count; j++)
                    len += pl[j].DistanceTo(pl[j - 1]);
                pathLengths[i] = len;
                totalLength += len;
            }

            double targetLength = totalLength * progress;
            double usedLength = 0;

            int pathCount = polylines.Count;
            double vDenom = Math.Max(1, pathCount - 1);

            for (int pIndex = 0; pIndex < pathCount; pIndex++)
            {
                var plSrc = polylines[pIndex];
                if (plSrc == null || !plSrc.IsValid || plSrc.Count < 2)
                    continue;

                var pl = new Polyline(plSrc);

                // ── Progress trimming ──
                double remainingBudget = targetLength - usedLength;
                if (remainingBudget <= 0)
                    break; // all budget consumed

                if (remainingBudget < pathLengths[pIndex])
                {
                    // Trim this polyline to the remaining budget
                    var trimmed = new Polyline();
                    trimmed.Add(pl[0]);
                    double accumulated = 0;
                    for (int seg = 1; seg < pl.Count; seg++)
                    {
                        double segLen = pl[seg].DistanceTo(pl[seg - 1]);
                        if (accumulated + segLen >= remainingBudget)
                        {
                            // Interpolate the cut point
                            double t = (remainingBudget - accumulated) / segLen;
                            var cutPt = pl[seg - 1] + t * (pl[seg] - pl[seg - 1]);
                            trimmed.Add(cutPt);
                            break;
                        }
                        trimmed.Add(pl[seg]);
                        accumulated += segLen;
                    }
                    pl = trimmed;
                    usedLength = targetLength; // budget fully consumed
                }
                else
                {
                    usedLength += pathLengths[pIndex];
                }

                bool pathIsClosed =
                    pl.IsClosed || pl[0].DistanceTo(pl[pl.Count - 1]) <= tol;

                int frameCount = pathIsClosed ? pl.Count - 1 : pl.Count;
                if (frameCount < 2)
                    continue;

                double v = (pathCount > 1) ? (double)pIndex / vDenom : 0.0;

                var mesh = new Mesh();
                mesh.Vertices.UseDoublePrecisionVertices = true;
                if (useUV)
                    mesh.TextureCoordinates.Clear();

                // ── 1) Build rings ──
                for (int i = 0; i < frameCount; i++)
                {
                    Point3d pt = pl[i];

                    // Compute tangent (averaged for interior points)
                    Vector3d tan;
                    if (pathIsClosed)
                    {
                        int iPrev = (i - 1 + frameCount) % frameCount;
                        int iNext = (i + 1) % frameCount;
                        tan = (pl[i] - pl[iPrev]) + (pl[iNext] - pl[i]);
                    }
                    else
                    {
                        if (i == 0)
                            tan = pl[i + 1] - pl[i];
                        else if (i == frameCount - 1)
                            tan = pl[i] - pl[i - 1];
                        else
                            tan = (pl[i] - pl[i - 1]) + (pl[i + 1] - pl[i]);
                    }

                    if (!tan.Unitize())
                        tan = Vector3d.XAxis;

                    // Build orthonormal frame
                    var upRef = Vector3d.ZAxis;
                    if (Math.Abs(Vector3d.Multiply(tan, upRef)) > 0.99)
                        upRef = Vector3d.YAxis;

                    var x = Vector3d.CrossProduct(upRef, tan);
                    if (!x.Unitize()) x = Vector3d.XAxis;

                    var y = Vector3d.CrossProduct(tan, x);
                    if (!y.Unitize()) y = Vector3d.YAxis;

                    var frame = new Plane(pt, x, y);
                    var xform = Transform.PlaneToPlane(Plane.WorldXY, frame);

                    double u = 0.0;
                    if (useUV)
                    {
                        u = pathIsClosed
                            ? (double)i / frameCount
                            : (frameCount > 1 ? (double)i / (frameCount - 1) : 0.0);
                    }

                    for (int j = 0; j < ringCount; j++)
                    {
                        var p = prof[j];
                        p.Transform(xform);
                        mesh.Vertices.Add(p);

                        if (useUV)
                            mesh.TextureCoordinates.Add(new Point2f((float)u, (float)v));
                    }
                }

                // ── 2) Side faces ──
                if (pathIsClosed)
                {
                    for (int i = 0; i < frameCount; i++)
                    {
                        int iNext = (i + 1) % frameCount;
                        for (int j = 0; j < ringCount; j++)
                        {
                            int jNext = (j + 1) % ringCount;
                            mesh.Faces.AddFace(
                                i * ringCount + j,
                                iNext * ringCount + j,
                                iNext * ringCount + jNext,
                                i * ringCount + jNext);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < frameCount - 1; i++)
                    {
                        for (int j = 0; j < ringCount; j++)
                        {
                            int jNext = (j + 1) % ringCount;
                            mesh.Faces.AddFace(
                                i * ringCount + j,
                                (i + 1) * ringCount + j,
                                (i + 1) * ringCount + jNext,
                                i * ringCount + jNext);
                        }
                    }
                }

                // ── 3) End caps ──
                if (!pathIsClosed && cap && ringCount >= 3)
                {
                    // Start cap
                    int startOffset = 0;
                    var c0 = Point3d.Origin;
                    for (int j = 0; j < ringCount; j++)
                        c0 += mesh.Vertices[startOffset + j];
                    c0 /= ringCount;

                    int idxC0 = mesh.Vertices.Add(c0);
                    if (useUV)
                        mesh.TextureCoordinates.Add(new Point2f(0f, (float)v));

                    for (int j = 0; j < ringCount; j++)
                    {
                        int jNext = (j + 1) % ringCount;
                        mesh.Faces.AddFace(idxC0, startOffset + j, startOffset + jNext);
                    }

                    // End cap
                    int endOffset = (frameCount - 1) * ringCount;
                    var c1 = Point3d.Origin;
                    for (int j = 0; j < ringCount; j++)
                        c1 += mesh.Vertices[endOffset + j];
                    c1 /= ringCount;

                    int idxC1 = mesh.Vertices.Add(c1);
                    if (useUV)
                        mesh.TextureCoordinates.Add(new Point2f(1f, (float)v));

                    for (int j = 0; j < ringCount; j++)
                    {
                        int jNext = (j + 1) % ringCount;
                        mesh.Faces.AddFace(idxC1, endOffset + jNext, endOffset + j);
                    }
                }

                // ── 4) Finalize ──
                mesh.Normals.ComputeNormals();
                mesh.UnifyNormals(true);
                mesh.Compact();

                meshes.Add(mesh);
            }

            return meshes;
        }
    }
}
