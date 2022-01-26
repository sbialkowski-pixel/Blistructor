using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
#if PIXEL
using Pixel.Rhino.Geometry;
using Pixel.Rhino.Geometry.Intersect;
#else
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
#endif
using Newtonsoft.Json.Linq;

namespace Blistructor
{
    public class BlisterCutter2
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.BlisterCutter2");
        public List<Blister> Queue = new List<Blister>();
        public List<Blister> Cutted = new List<Blister>();
        public Anchor anchor;
        private int loopTolerance = 5;

        public BlisterCutter2(Blister blisterToCut)
        {
        }

        public int CuttablePillsLeft
        {
            get
            {
                int counter = 0;
                foreach (Blister subBlister in Queue)
                {
                    counter += subBlister.Pills.Select(pill => pill.State).Where(state => state == PillState.Queue || state == PillState.Alone).ToList().Count;
                }
                return counter;
            }
        }

        private CuttingState PerformCut()
        {
            // Check if blister is correctly allign
            if (!anchor.IsBlisterStraight(Setups.MaxBlisterPossitionDeviation)) return CuttingState.CTR_WRONG_BLISTER_POSSITION;
            log.Info(String.Format("=== Start Cutting ==="));
            int initialPillCount = Queue[0].Pills.Count;
            if (Queue[0].ToTight) return CuttingState.CTR_TO_TIGHT;
            if (Queue[0].LeftPillsCount == 1) return CuttingState.CTR_ONE_PILL;
            if (!anchor.ApplyAnchorOnBlister()) return CuttingState.CTR_ANCHOR_LOCATION_ERR;

            int n = 0; // control
                       // Main Loop
            while (Queue.Count > 0)
            {
                //if (n > mainLimit && mainLimit != -1) break;
                // Extra control to not loop forever...
                if (n > initialPillCount + loopTolerance) break;
                log.Info(String.Format(String.Format("<<<<<<Blisters Count: Queue: {0}, Cutted {1}>>>>>>>>>>>>>>>>>>>>>>>>", Queue.Count, Cutted.Count)));
                // InnerLoop - Queue Blisters

                for (int i = 0; i < Queue.Count; i++)
                {
                    Blister subBlister = Queue[i];
                    log.Info(String.Format("{0} pills left to cut on on Blister:{1}", subBlister.Pills.Count, i));
                    if (subBlister.IsDone)
                    {
                        log.Info("Blister is already cutted or is to tight for cutting.");
                        continue;
                    }
                    // In tuple I have | CutOut Blister | Current Updated Blister | Extra Blisters to Cut (recived by spliting currentBlister) 
                    PillCutter cutter = new PillCutter(subBlister);
                    PillCutProposals cutProposal;
                    try
                    {
                        cutProposal = cutter.CutNext(onlyAnchor: false);
                    }
                    catch (Exception)
                    {
                        log.Error("!!!Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_FAILED;
                    }

                    if (cutProposal.HasGrasperCollisions())
                    {
                        subBlister.RemoveCollision(BlisterCutProposals);
                        cutProposal = cutter.CutNext(onlyAnchor: true);
                    }
                    cutProposal.Approve();

                    //CutResult result = subBlister.CutNext();
                    //log.Debug(String.Format("Cutting Result: Cutout: {0} - Current Blister {1} - New Blisters {2}.", result.CutOut, result.Current, result.ExtraBlisters.Count));
                    // If anything was cutted, add to list
                    if (cutProposal.CutOut != null)
                    {
                        Pill cuttedPill = cutProposal.CutOut.Pills[0];
                        if (!cuttedPill.IsAnchored)
                        {
                            log.Debug("Anchor - Update Pred Line");

                            anchor.Update(cutProposal.CutOut);
                        }
                        if (cuttedPill.IsAnchored && cuttedPill.State != PillState.Alone && CuttablePillsLeft == 2) anchor.FindNewAnchorAndApplyOnBlister(cutProposal.CutOut);
                        log.Debug("Adding new CutOut subBlister to Cutted list");
                        Cutted.Add(cutProposal.CutOut);
                    }
                    else
                    {
                        log.Error("!!!Cannot cut blister Anymore!!!");
                        return CuttingState.CTR_FAILED;
                    }
                    // override current bluster, if null , remove it from Queue list
                    if (cutProposal.Current == null)
                    {
                        log.Info("Current subBlister is empty. Removing from Queue");
                        Queue.RemoveAt(i);
                        i--;
                        break;
                    }
                    else
                    {
                        log.Debug("Updating subBlister");
                        subBlister = cutProposal.Current;
                        // Sort Pills by last Knife Possition -> Last Pill Centre
                        // Point3d lastKnifePossition = Cutted.Last().Cells[0].bestCuttingData.GetLastKnifePossition();
                        Point3d lastKnifePossition = Cutted.Last().Pills[0].PillCenter;
                        if (lastKnifePossition.X != double.NaN) subBlister.SortPillsByPointDirection(lastKnifePossition, false);
                        //if (lastKnifePossition.X != double.NaN) subBlister.SortCellsByCoordinates(true);
                    }
                    // Add extra blsters if any was created
                    if (cutProposal.ExtraBlisters.Count != 0)
                    {
                        log.Debug("Adding new subBlister(s) to Queue");
                        Queue.AddRange(cutProposal.ExtraBlisters);
                        break;
                    }
                }
                n++;
            }

            if (initialPillCount == Cutted.Count) return CuttingState.CTR_SUCCESS;
            else return CuttingState.CTR_FAILED;
        }
    }

    public class BlisterCutProposal : PillCutProposals
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.BlisterCutProposal");

        private Blister _blister;

        public Blister CutOut { get; private set; }
        public Blister Current { get; private set; }
        public List<Blister> ExtraBlisters { get; private set; }

        public BlisterCutProposal(PillCutter cutter, Blister blister) : base(cutter)
        {
            _blister = blister;
        }

        public bool IsValidCut()
        {
            // Inspect leftovers.
            foreach (PolylineCurve leftover in BestCuttingData.BlisterLeftovers)
            {
                Blister newBli = new Blister(_blister.Pills, leftover, _blister._workspace);
                if (!newBli.CheckConnectivityIntegrity(_pill))
                {
                    log.Warn("CheckConnectivityIntegrity failed. Propsed cut cause inconsistency in leftovers");
                    return false;
                }
                // BEFORE THIS I NEED TO UPDATE ANCHORS.
                // If after cutting none pill in leftovers has HasPosibleAnchor false, this mean BAAAAAD
                if (!newBli.HasActiveAnchor)
                {
                    log.Warn("No Anchor found for this leftover. Skip this cutting.");
                    return false;
                }
            }
            return true;
        }

        public void Approve()
        {
            if (State == CutState.Failed)
            {
                throw new Exception("Cannot approve failed cut. Big mistake!!!!");
            }

            ExtraBlisters = new List<Blister>();
            if (State == CutState.Alone)
            {
                CutOut = _blister;
                Current = null;
                return;
            }
            #region Update Pill
            log.Debug("Removing Connection data from cutted Pill. Updating pill status to Cutted");
            _pill.State = PillState.Cutted;
            _pill.RemoveConnectionData();
            #endregion

            #region Update Current
            log.Debug("Updating current Blister outline and remove cutted Pill from blister");
            _blister.Outline = BestCuttingData.BlisterLeftovers[0];
            int locationIndex = _blister.Pills.FindIndex(pill => pill.Id == _pill.Id);
            _blister.Pills.RemoveAt(locationIndex);

            // Case if Blister is splited because of this cut.
            log.Debug("Remove all cells which are not belong to this Blister anymore.");
            List<Pill> removerdPills = new List<Pill>(_blister.Pills.Count);
            for (int i = 0; i < _blister.Pills.Count; i++)
            {
                // If cell is no more inside this Blister, remove it.
                if (!Geometry.InclusionTest(_blister.Pills[i], _blister))
                {
                    // check if cell is aimed to cut. For 100% all cells in Blister should be Queue.. If not it;s BUGERSON
                    if (_blister.Pills[i].State != PillState.Queue) continue;
                    removerdPills.Add(_blister.Pills[i]);
                    _blister.Pills.RemoveAt(i);
                    i--;
                }
            }
            #endregion
            #region create CutOut
            CutOut = new Blister(_pill, BestCuttingData.Polygon, _blister._workspace);
            #endregion

            #region Leftovers
            for (int j = 1; j < BestCuttingData.BlisterLeftovers.Count; j++)
            {
                PolylineCurve blisterLeftover = BestCuttingData.BlisterLeftovers[j];
                Blister newBli = new Blister(removerdPills, blisterLeftover, _blister._workspace);
                // Verify if new Blister is attachetd to anchor
                if (newBli.HasPossibleAnchor)
                {
                };
                ExtraBlisters.Add(newBli);
            }
            #endregion
        }

        /*
        public void ApplyCut(Pill foundCell, CutState foundCellState, int locationIndex)
        {
            List<Blister> newBlisters = new List<Blister>();
            // Ok. If cell is not alone, and Anchor requerments are met. Set cell status as Cutted, and remove all connection with this cell.
            if (foundCellState == CutState.Succeed)
            {
                foundCell.State = PillState.Cutted;
                foundCell.RemoveConnectionData();
            }

            log.Info("Updating current Blister outline. Creating cutout Blister to store.");

            // If more cells are on Blister, replace outline of current Blister by first curve from the list...
            // Update current Blister outline
            Outline = foundCell.bestCuttingData.BlisterLeftovers[0];
            // If all was ok, Create new Blister with cutted Outline
            Blister cutted = new Blister(foundCell, foundCell.bestCuttingData.Polygon, blister);
            // Remove this cell from current Blister
            pills.RemoveAt(locationIndex);
            // Deal with more then one leftover
            // Remove other cells which are not belong to this Blister anymore...
            log.Debug("Remove all cells which are not belong to this Blister anymore.");
            log.Debug(String.Format("Before removal {0}", pills.Count));
            List<Pill> removerdCells = new List<Pill>(pills.Count);
            for (int i = 0; i < pills.Count; i++)
            {
                // If cell is no more inside this Blister, remove it.
                if (!Geometry.InclusionTest(pills[i], this))
                {
                    // check if cell is aimed to cut. For 100% all cells in Blister should be Queue.. If not it;s BUGERSON
                    if (pills[i].State != PillState.Queue) continue;
                    removerdCells.Add(pills[i]);
                    pills.RemoveAt(i);
                    i--;
                }
            }
            // Check if any form remaining cells in current Blister has Active anchore. /It is not alone/ If doesent, return nulllllls 
            //  if (!this.HasActiveAnchor) return Tuple.Create<Blister, Blister, List<Blister>>(null, null, null);

            //  log.Debug(String.Format("After removal {0} - Removed Cells {1}", cells.Count, removerdCells.Count));
            //  log.Debug(String.Format("Loop by Leftovers  [{0}] (ommit first, it is current Blister) and create new blisters.", foundCell.bestCuttingData.BlisterLeftovers.Count - 1));
            //int cellCount = 0;
            // Loop by Leftovers (ommit first, it is current Blister) and create new blisters.
            //List<Blister> newBlisters = new List<Blister>();
            for (int j = 1; j < foundCell.bestCuttingData.BlisterLeftovers.Count; j++)
            {
                PolylineCurve blisterLeftover = foundCell.bestCuttingData.BlisterLeftovers[j];
                Blister newBli = new Blister(removerdCells, blisterLeftover, blister);
                // Verify if new Blister is attachetd to anchor
                //    if (!newBli.HasActiveAnchor) return Tuple.Create<Blister, Blister, List<Blister>>(null, null, null);
                //cellCount += newBli.Cells.Count;
                newBlisters.Add(newBli);
            }
            return new CutResult(cutted, this, newBlisters);
        }
        */
    }

    public class BlisterCutter
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.BlisterCutter");

        private PillCutProposals _cutProposals;
        private readonly Blister _blisterToCut;
        private PillCutter _pillCutter;

        public BlisterCutter(Blister blisterTotCut)
        {
            _blisterToCut = blisterTotCut;
        }

        public BlisterCutProposal CutNext(bool onlyAnchor = false)
        {
            if (!onlyAnchor)
            {
                for (int i = 0; i < _blisterToCut.Pills.Count; i++)
                {
                    Pill currentPill = _blisterToCut.Pills[i];
                    if (currentPill.IsAnchored) continue;
                    BlisterCutProposal proposal = CutPill(currentPill);
                    if (proposal == null) continue;
                    else
                    {
                        log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.Id, i));
                        return proposal;
                    }
                }
                // If nothing, try to cut anchored ones...
                log.Warn("No cutting data generated for whole Blister. Try to find cutting data in anchored...");
            }
            for (int i = 0; i < _blisterToCut.Pills.Count; i++)
            {
                Pill currentPill = _blisterToCut.Pills[i];
                if (!currentPill.IsAnchored) continue;
                BlisterCutProposal proposal = CutPill(currentPill);
                if (proposal == null) continue;
                else
                {
                    log.Info(String.Format("Cut Path found for pill {0} after checking {1} anchored pills", currentPill.Id, i));
                    return proposal;
                }
            }
            log.Warn("No cutting data generated for whole Blister.");
            throw new Exception("No cutting data generated for whole Blister.");
        }

        private BlisterCutProposal CutPill(Pill currentPill)
        {
            PillCutter pillCutter = new PillCutter(currentPill);
            pillCutter.TryCut();
            if (pillCutter.State == CutState.Failed)
            {
                return null;
            }
            else
            {
                BlisterCutProposal proposal = new BlisterCutProposal(pillCutter, _blisterToCut);
                if (proposal.IsValidCut()) return proposal;
                else return null;
            }
        }
        /*
        public CutResult CutNext()
        {
            log.Debug(String.Format("There is still {0} cells on Blister", pills.Count));
            // Try cutting only AnchorInactive cells
            for (int i = 0; i < pills.Count; i++)
            {
                Pill currentPill = pills[i];
                if (currentPill.IsAnchored) continue;
                CutState tryCutState = currentPill.TryCut(true);

                if (tryCutState != CutState.Failed)
                {
                    CutResult data = CuttedCellProcessing(currentPill, tryCutState, i);
                    if (data.State == CutState.Failed)
                    {
                        continue;
                    }
                    else
                    {
                        log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.Id, i));
                        return data;
                    }
                }
                else
                {
                    continue;
                }
            }
            // If nothing, try to cut anchored ones...
            log.Warn("No cutting data generated for whole Blister. Try to find cutting data in anchored ...");

            for (int i = 0; i < pills.Count; i++)
            {
                Pill currentPill = pills[i];
                if (!currentPill.IsAnchored) continue;
                CutState tryCutState = currentPill.TryCut(false);
                if (currentPill.IsAnchored && tryCutState != CutState.Failed)
                {
                    CutResult data = CuttedCellProcessing(currentPill, tryCutState, i);
                    if (data.State == CutState.Failed)
                    {
                        continue;
                    }
                    else
                    {
                        log.Info(String.Format("Cut Path found for pill {0} after checking {1} pills", currentPill.Id, i));
                        return data;
                    }
                }
                else
                {
                    continue;
                }
            }
            log.Warn("No cutting data generated for whole Blister.");
            return new CutResult(null, this, new List<Blister>());
        }
        */
        /*
        public CutResult CheckIfAlone()
        {
            if (tryCutState != CutState.Alone)
            {
                currentCell.State = PillState.Alone;
                log.Info(String.Format("Cell {0}. That was last cell on Blister.", currentCell.id));

                return new CutResult(this, null, new List<Blister>(), CutState.Alone);
            }
        }
        */


        private CutResult CuttedCellProcessing(Pill foundCell, CutState foundCellState, int locationIndex)
        {
            List<Blister> newBlisters = new List<Blister>();
            // If on Blister was only one cell, after cutting is status change to Alone, so just return it, without any leftover blisters. 
            if (foundCellState == CutState.Alone)
            {
                foundCell.State = PillState.Alone;
                log.Info(String.Format("Cell {0}. That was last cell on Blister.", foundCell.Id));

                return new CutResult(this, null, newBlisters, CutState.Alone);
            }

            log.Info(String.Format("Cell {0}. That was NOT last cell on Blister.", foundCell.Id));

            // Inspect leftovers.
            foreach (PolylineCurve leftover in foundCell.bestCuttingData.BlisterLeftovers)
            {
                Blister newBli = new SubBlister(pills, leftover, blister);
                if (!newBli.CheckConnectivityIntegrity(foundCell)) return new CutResult();
                if (!newBli.HasActiveAnchor)
                {
                    log.Warn("No Anchor found for this leftover. Skip this cutting.");
                    return new CutResult();
                }
            }
            return new CutResult(this, null, null, CutState.Proposal);
        }


        #region PREVIEW STUFF FOR DEBUG MOSTLY

        public List<PolylineCurve> GetCuttingPath()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (pills[0].bestCuttingData == null) return new List<PolylineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return pills[0].bestCuttingData.Path;
        }

        public List<LineCurve> GetCuttingLines()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (pills[0].bestCuttingData == null) return new List<LineCurve>();

            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return pills[0].bestCuttingData.bladeFootPrint;
        }
        public List<LineCurve> GetIsoRays()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            if (pills[0].bestCuttingData == null) return new List<LineCurve>();
            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            return pills[0].bestCuttingData.IsoSegments;
            //return cells[0].bestCuttingData.IsoRays;

        }
        public List<PolylineCurve> GetLeftOvers()
        {
            if (pills[0].bestCuttingData == null) return new List<PolylineCurve>();
            return pills[0].bestCuttingData.BlisterLeftovers;
        }
        public List<PolylineCurve> GetAllPossiblePolygons()
        {
            // !!!============If cell is anchor it probably doesn't have cutting stuff... To validate===========!!!!
            // cells[0].bestCuttingData.GenerateBladeFootPrint();
            if (pills[0].bestCuttingData == null) return new List<PolylineCurve>();
            return pills[0].bestCuttingData.BlisterLeftovers;
        }

        #endregion
    }

    public class Blister
    {
        private static readonly ILog log = LogManager.GetLogger("Cutter.Blister");

        internal Workspace _workspace;
        private readonly bool toTight = false;
        private PolylineCurve outline;
        private List<Pill> pills;
        public List<PolylineCurve> irVoronoi;


        #region CONSTRUCTORS
        private Blister(Workspace workspace)
        {
            _workspace = workspace;
        }

        /// <summary>
        /// Internal constructor for non-Outline stuff
        /// </summary>
        /// <param name="outline">Blister Shape</param>
        private Blister(PolylineCurve outline, Workspace workspace) : this(workspace)
        {
            pills = new List<Pill>();
            Geometry.UnifyCurve(outline);
            this.outline = outline;
        }

        /// <summary>
        /// Contructor mostly to create cut out blisters with one cell
        /// </summary>
        /// <param name="pillsOutline"></param>
        /// <param name="outline"></param>
        public Blister(Pill pillsOutline, PolylineCurve outline, Workspace workspace) : this(outline, workspace)
        {
            this.pills = new List<Pill>(1) { pillsOutline };
        }

        /// <summary>
        /// New Blister based on already existing cells and outline.
        /// </summary>
        /// <param name="pillsOutline">Existing cells</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<Pill> pillsOutline, PolylineCurve outline, Workspace workspace) : this(outline, workspace)
        {
            log.Debug("Creating new Blister");
            this.pills = new List<Pill>(pillsOutline.Count);
            // Loop by all given cells
            foreach (Pill pill in pillsOutline)
            {
                if (pill.State == PillState.Cutted) continue;
                // If cell is not cutOut, check if belong to this Blister.
                if (Geometry.InclusionTest(pill, this))
                {
                    pill.SubBlister = this;
                    this.pills.Add(pill);
                }

            }
            log.Debug(String.Format("Instantiated {0} cells on Blister", pills.Count));
            if (LeftPillsCount == 1) return;
            log.Debug("Sorting Cells");
            // Order by CoordinateIndicator so it means Z-ordering.
            SortPillsByCoordinates(true);
            //  this.cells = cells.OrderBy(cell => cell.CoordinateIndicator).Reverse().ToList();
            // Rebuild cells connectivity.
            log.Debug("Creating ConncectivityData");
            CreateConnectivityData();
        }

        /// <summary>
        /// New initial Blister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pillsOutline">Pills outline</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<PolylineCurve> pillsOutline, Polyline outline, Workspace workspace) : this(pillsOutline, outline.ToPolylineCurve(), workspace)
        {
        }

        /// <summary>
        /// New initial Blister with Cells creation base on pills outlines.
        /// </summary>
        /// <param name="pillsOutline">Pills outline</param>
        /// <param name="outline">Blister edge outline</param>
        public Blister(List<PolylineCurve> pillsOutline, PolylineCurve outline, Workspace blister) : this(outline, blister)
        {
            log.Debug("Creating new Blister");
            // Cells Creation
            pills = new List<Pill>(pills.Count);
            for (int cellId = 0; cellId < pillsOutline.Count; cellId++)
            {
                if (pillsOutline[cellId].IsClosed)
                {
                    Pill pill = new Pill(cellId, pillsOutline[cellId], this);
                    //  cell.SetDistance(guideLine);
                    pills.Add(pill);
                }
            }
            log.Debug(String.Format("Instantiated {0} pills on Blister", pills.Count));
            // If only 1 cell, finish here.
            if (pills.Count <= 1) return;
            // NOTE: Cells Sorting move to Blister nd controled in BListructor...
            // Order by Corner distance. First Two set as possible Anchor.
            // log.Debug("Sorting Cells");
            // cells = cells.OrderBy(cell => cell.CornerDistance).ToList();
            // for (int i = 0; i < 2; i++)
            //{
            //     cells[i].PossibleAnchor = true;
            // }
            toTight = ArePillsOverlapping();
            log.Info(String.Format("Is to tight? : {0}", toTight));
            if (toTight) return;
            irVoronoi = Geometry.IrregularVoronoi(pills, Outline.ToPolyline(), 50, 0.05);
            CreateConnectivityData();
        }
        #endregion

        #region PROPERTIES

        public int LeftPillsCount
        {
            get
            {
                int count = 0;
                if (pills.Count > 0)
                {
                    foreach (Pill pill in pills)
                    {
                        if (pill.State == PillState.Queue) count++;
                    }
                }
                return count;
            }
        }

        public List<int> LeftPillsIndices
        {
            get
            {
                List<int> indices = new List<int>();
                if (pills.Count == 0) return indices;
                foreach (Pill cell in pills)
                {
                    if (cell.State == PillState.Queue) indices.Add(cell.Id);
                }
                return indices;
            }
        }

        public List<Curve> GetPills(bool offset)
        {
            List<Curve> pillsOutline = new List<Curve>();
            foreach (Pill cell in pills)
            {
                if (offset) pillsOutline.Add(cell.Offset);
                else pillsOutline.Add(cell.Outline);
            }
            return pillsOutline;
        }

        public bool IsDone
        {
            get
            {
                if (ToTight) return true;
                else if (LeftPillsCount < 1) return true;
                else return false;
            }
        }

        public List<Pill> Pills { get { return pills; } }

        public Pill PillByID(int id)
        {
            List<Pill> a = pills.Where(cell => cell.Id == id).ToList();
            if (a.Count == 1) return a[0];
            return null;
        }

        public bool HasActiveAnchor
        {
            get
            {
                return pills.Any(cell => cell.IsAnchored);
            }
        }

        public bool HasPossibleAnchor
        {
            get
            {
                return pills.Any(cell => cell.possibleAnchor);
            }
        }

        public PolylineCurve Outline { get { return outline; } set { outline = value; } }

        //public PolylineCurve BBox { get { return bBox; } }

        public bool ToTight { get { return toTight; } }

        #endregion

        #region SORTS
        /// <summary>
        /// Z-Ordering, Descending
        /// </summary>
        public void SortPillsByCoordinates(bool reverse)
        {
            pills = pills.OrderBy(pill => pill.CoordinateIndicator).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByPointDirection(Point3d pt, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetDirectionIndicator(pt)).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByPointDistance(Point3d pt, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetDistance(pt)).ToList();
            if (reverse) pills.Reverse();
        }
        public void SortPillsByPointsDistance(List<Point3d> pts, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetClosestDistance(pts)).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByCurveDistance(Curve crv, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetDistance(crv)).ToList();
            if (reverse) pills.Reverse();
        }

        public void SortPillsByCurvesDistance(List<Curve> crvs, bool reverse)
        {
            pills = pills.OrderBy(pill => pill.GetClosestDistance(crvs)).ToList();
            if (reverse) pills.Reverse();
        }

        #endregion


        /// <summary>
        /// Check if PillsOutlines (with knife wird appiled) are not intersecting. 
        /// </summary>
        /// <returns>True if any pill intersect with other.</returns>
        protected bool ArePillsOverlapping()
        {
            // output = false;
            for (int i = 0; i < pills.Count; i++)
            {
                for (int j = i + 1; j < pills.Count; j++)
                {
                    List<IntersectionEvent> inter = Intersection.CurveCurve(pills[i].Offset, pills[j].Offset, Setups.IntersectionTolerance);
                    if (inter.Count > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Check Connectivity (AdjacingPills) against some pill planned to be cutout.
        /// </summary>
        /// <param name="pillToCut"></param>
        /// <returns>True if all ok, false if there is inconsistency.</returns>
        internal bool CheckConnectivityIntegrity(Pill pillToCut)
        {
            if (pills.Count > 1)
            {
                foreach (Pill pill in pills)
                {

                    if (pill.adjacentPills.Count == 1)
                    {
                        if (pill.adjacentPills[0].Id == pillToCut.Id)
                        {
                            log.Warn("Adjacent Cells Checker - Found alone Outline in multi-pills Blister. Skip this cutting.");
                            return false;
                        }
                    }
                }

                // Check for Blister integrity
                List<HashSet<int>> setsList = new List<HashSet<int>>();
                // Add initileze set
                HashSet<int> initSet = new HashSet<int>();
                initSet.Add(pills[0].Id);
                initSet.UnionWith(pills[0].GetAdjacentPillsIds());
                setsList.Add(initSet);
                for (int i = 1; i < pills.Count; i++)
                {
                    List<int> pillsIds = pills[i].GetAdjacentPillsIds();
                    // Remove foundCell Id
                    pillsIds.Remove(pillToCut.Id);
                    pillsIds.Add(pills[i].Id);
                    // check if smallSet fit to any bigger sets in list.
                    bool added = false;
                    for (int j = 0; j < setsList.Count; j++)
                    {
                        // If yes, add it and break forloop.
                        if (setsList[j].Overlaps(pillsIds))
                        {
                            setsList[j].UnionWith(pillsIds);
                            added = true;
                            break;
                        }
                    }
                    // if not added, create ne big set based on small one.
                    if (!added)
                    {
                        HashSet<int> newSet = new HashSet<int>();
                        newSet.UnionWith(pillsIds);
                        setsList.Add(newSet);
                    }
                }

                // If only one setList, its ok. If more, try to merge, if not possible, Blister is not consistent...
                if (setsList.Count > 1)
                {
                    // Create finalSet
                    HashSet<int> finalSet = new HashSet<int>();
                    finalSet.UnionWith(setsList[0]);
                    // Remove form setList all sets which are alredy added to finalSet, in this case first one
                    setsList.RemoveAt(0);
                    // Try 3 times. Why 3, i dont know. Just like that...
                    int initsetsListCount = setsList.Count;
                    for (int m = 0; m < 3; m++)
                    {
                        for (int k = 0; k < setsList.Count; k++)
                        {
                            if (finalSet.Overlaps(setsList[k]))
                            {
                                // if overlaped, add to finalSet
                                finalSet.UnionWith(setsList[k]);
                                // Remove form setList all sets which are alredy added to finalSet, in this case first one
                                setsList.RemoveAt(k);
                                k--;
                            }
                        }
                        // If all sets are merged, break;
                        if (setsList.Count == 0) break;
                        // If after second runf setList is same, means no change, brake;
                        if (m > 2 && setsList.Count == initsetsListCount) break;
                    }
                    if (setsList.Count > 0)
                    {
                        log.Warn("Adjacent Pills Checker - Pill AdjacentConnection not cconsistent. Skip this cutting.");
                        return false;
                    }
                    else return true;
                }
                else return true;
            }
            else
            {
                return true;
            }

        }

        /// <summary>
        /// Iterate throught pills and compute interconnectring data between them. 
        /// </summary>
        public void CreateConnectivityData()
        {
            log.Debug("Creating Conectivity Data.");
            foreach (Pill currentPill in pills)
            {
                // If current pill is cut out... go to next one.
                if (currentPill.State == PillState.Cutted) continue;
                // log.Debug(String.Format("Checking pill: {0}", currentCell.id));
                List<Point3d> currentMidPoints = new List<Point3d>();
                List<Curve> currentConnectionLines = new List<Curve>();
                List<Pill> currenAdjacentPills = new List<Pill>();
                foreach (Pill proxPill in pills)
                {
                    // If proxCell is cut out or cutCell is same as proxCell, next cell...
                    if (proxPill.State == PillState.Cutted || proxPill.Id == currentPill.Id) continue;
                    // log.Debug(String.Format("Checking pill: {0}", currentCell.id));
                    Line line = new Line(currentPill.PillCenter, proxPill.PillCenter);
                    Point3d midPoint = line.PointAt(0.5);
                    double t;
                    if (currentPill.voronoi.ClosestPoint(midPoint, out t, 2.000))
                    {
                        // log.Debug(String.Format("Checking pill: {0}", currentCell.id));
                        currenAdjacentPills.Add(proxPill);
                        currentConnectionLines.Add(new LineCurve(line));
                        currentMidPoints.Add(midPoint);
                    }
                }
                log.Debug(String.Format("Pill ID: {0} - Adjacent:{1}, Conection {2} Proxy {3}", currentPill.Id, currenAdjacentPills.Count, currentConnectionLines.Count, currentMidPoints.Count));
                currentPill.AddConnectionData(currenAdjacentPills, currentConnectionLines, currentMidPoints);
            }
        }
    }
}
