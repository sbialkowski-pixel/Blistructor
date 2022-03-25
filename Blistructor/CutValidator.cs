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

        private List<Interval> CurrentJawPosibleIntervals { get; set; }
        private List<Interval> CutImpactIntervals { get; set; }
        private Interval BlisterImpactInterval { get; set; }

        public CutValidator(CutProposal cutProposal, Grasper grasper)
        {
            CutProposal = cutProposal;
            Grasper = grasper;
            CurrentJawPosibleIntervals = Grasper.GetJawPossibleIntervals();
            CutImpactIntervals = Grasper.ComputCutImpactInterval(CutProposal.BestCuttingData, applyJawBoundaries: true);
            BlisterImpactInterval = Grasper.ComputeTotalCutImpactInterval(CutProposal.BestCuttingData, CutImpactIntervals);
        }

        /// <summary>
        /// Check if Cut has any impact on Grasper.
        /// </summary>
        /// <returns></returns>
        public bool HasCutAnyImpactOnJaws
        {
            get
            {
                if (BlisterImpactInterval.IsValid)
                {
                    return true;
                }
                else return false;
            }
        }


        /// <summary>
        /// Check Connectivity (AdjacingPills) against pill planned to be cut.
        /// </summary>
        /// <param name="updateCutState">If true, CutProposal.State will be updated to CutState.Failed</param>
        /// <returns>True if all ok, false if there is inconsistency.</returns>
        public bool CheckConnectivityIntegrityInLeftovers(bool updateCutState = true)
        {
            if (CutProposal.State == CutState.Last) return true;
            foreach (PolylineCurve leftover in CutProposal.BestCuttingData.BlisterLeftovers)
            {
                // Duplicate Pills to not interference with original blister.
                List<Pill> dupPills = CutProposal.Blister.Pills.Select(pill => new Pill(pill)).ToList();
                Blister newBli = new Blister(dupPills, leftover);
                if (!newBli.CheckConnectivityIntegrity(CutProposal.Pill))
                {
                    log.Warn("This cut failed: CheckConnectivityIntegrity failed. Proposed cut cause inconsistency in leftovers");
                    if (updateCutState) CutProposal.State = CutState.Failed;
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Check for collision between current Jaws and cutImpactIntervals.
        /// </summary>
        /// <param name="updateCutState">If true, CutProposal.State wil be updated to CutState.Rejected</param>
        /// <returns></returns>
        public bool CheckJawsCollision(bool updateCutState = true)
        {
            List<JawPoint> currentJaws = Grasper.FindJawPoints(CurrentJawPosibleIntervals);
            List<Interval> currentJawsInterval = Grasper.GetRestrictedIntervals(currentJaws, Setups.JawKnifeAdditionalSafeDistance);
            if (Grasper.CollisionCheck(currentJawsInterval, CutImpactIntervals))
            {
                log.Warn("This cut was interrupted: Collision with Jaws detected.");
                if (updateCutState) CutProposal.State = CutState.Rejected;
                return false;
            }
            return true;
        }

        /// <summary>
        /// If this cut will remove whole jawPossibleLocation line, its is not good, at least if it is not last blister.
        /// </summary>
        /// <param name="updateCutState">If true, CutProposal.State will be updated to CutState.Failed</param>
        /// <returns>True if any Jaw can occure. False if whole jawPossibleLocation line is removed.</returns>
        public bool CheckJawsExistance(bool updateCutState = true)
        {
            if (CutProposal.State == CutState.Last) return true;
            if (BlisterImpactInterval.IncludesInterval(Grasper.IntervalsInterval(CurrentJawPosibleIntervals), true))
            {
                log.Warn("This cut failed: Proposed cut removes whole jawPossibleLocation. No place to grab blister.");
                if (updateCutState) CutProposal.State = CutState.Failed;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Create futureJawPosibleIntervals based on cutData and check if leftoves has place for Jaws.
        /// </summary>
        /// <param name="updateCutState">If true, CutProposal.State will be updated to CutState.Failed</param>
        /// <returns></returns>
        public bool CheckJawExistanceInLeftovers(bool updateCutState = true)
        {
            List<Interval> futureJawPosibleIntervals = Grasper.ApplyCutOnGrasperLocation(CurrentJawPosibleIntervals, CutProposal.BestCuttingData);
            List<LineCurve> futureJawPosibleLocation = Grasper.ConvertIntervalsToLines(futureJawPosibleIntervals);

            foreach (PolylineCurve leftover in CutProposal.BestCuttingData.BlisterLeftovers)
            {
                if (!Grasper.HasPlaceForJaw(futureJawPosibleLocation, leftover))
                {
                    log.Warn("This cut failed: Leftover has no place for Jaw.");
                    if (updateCutState) CutProposal.State = CutState.Failed;
                    return false;
                }
            }
            return true;
        }

        public bool CheckJawLimitVoilations(bool updateCutState = true)
        {
            List<Interval> futureJawPosibleIntervals = Grasper.ApplyCutOnGrasperLocation(CurrentJawPosibleIntervals, CutProposal.BestCuttingData);
            // This cut is not influancing grasper.
            if (futureJawPosibleIntervals == null) return false;

            List<JawPoint> newJaws = Grasper.FindJawPoints(futureJawPosibleIntervals);

            double xLocation = newJaws.Select(jaw => jaw.Location.X).Min();
            if (xLocation > Setups.CartesianJawYLimit)
            {
                log.Warn("This cut failed: Jaw2: max Y Limit voilation.");
                if (updateCutState) CutProposal.State = CutState.Failed;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Check if after applying cut, CutBLister can handle any Jaw.
        /// This is to check if second last piece can be hold by any Jaw.
        /// </summary>
        /// <param name="updateCutState"></param>
        /// <returns>True if Jaw can exist on this chuck after applying cut</returns>
        public bool CheckJawExistanceInCut(bool updateCutState = true)
        {
            List<Interval> grasperIntervals = Grasper.GetJawPossibleIntervals();

            foreach (Interval cutImpact in CutImpactIntervals)
            {
                List<Interval> remainingGraspersLocation = new List<Interval>(grasperIntervals.Count);

                foreach (Interval currentGraspersLocation in grasperIntervals)
                {
                    remainingGraspersLocation.AddRange(Interval.FromSubstraction(currentGraspersLocation, cutImpact).Where(interval => interval.Length > 0).ToList());
                }
                grasperIntervals = remainingGraspersLocation;
            }
            grasperIntervals = grasperIntervals.Select(spacing => { spacing.MakeIncreasing(); return spacing; }).ToList();
            grasperIntervals.OrderBy(spacing => spacing.T0).ToList();

            if (grasperIntervals == null) return false;
            List<LineCurve> futureJawPosibleLocation = Grasper.ConvertIntervalsToLines(grasperIntervals);
            if (!Grasper.HasPlaceForJaw(futureJawPosibleLocation, CutProposal.BestCuttingData.Polygon))
            {
                log.Warn("This cut failed: Current cut has no place for Jaw.");
                if (updateCutState) CutProposal.State = CutState.Failed;
                return false;
            }
            return true;
        }
    }
}
