using System;
using System.Collections.Generic;
using System.Linq;

#if PIXEL
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
using ExtraMath = Pixel.Rhino.RhinoMath;
#else
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using ExtraMath = Rhino.RhinoMath;
#endif

using log4net;
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    /// <summary>
    /// What to validate:
    /// - CutBlister Integrity
    /// - Grapser (Jaws) cut - collision
    /// - If CutBLister is without any JAW if more the one PILL left
    /// - 
    /// </summary>
    public class CutValidator
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.PillCutProposals");
        private CutProposal CutProposal { get; set; }
        private Grasper Grasper { get; set; }
        public CutValidator(CutProposal cutProposal, Grasper grasper)
        {
            CutProposal = cutProposal;
            Grasper = grasper;
        }


        /// <summary>
        /// Check Connectivity (AdjacingPills) against pill planned to be cut.
        /// </summary>
        /// <returns>True if all ok, false if there is inconsistency.</returns>
        public bool CheckConnectivityIntegrityInLeftovers()
        {
            foreach (PolylineCurve leftover in CutProposal.BestCuttingData.BlisterLeftovers)
            {
                Blister newBli = new Blister(CutProposal.Blister.Pills, leftover);
                if (!newBli.CheckConnectivityIntegrity(CutProposal.Pill))
                {
                    log.Warn("CheckConnectivityIntegrity failed. Proposed cut cause inconsistency in leftovers");
                    return false;
                }
            }
            return false;
        }


    }
}
