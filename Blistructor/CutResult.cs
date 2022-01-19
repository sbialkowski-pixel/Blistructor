using System;
using System.Collections.Generic;
using System.Text;

namespace Blistructor
{
    public class CutResult
    {
        public SubBlister CutOut;
        public SubBlister Current;
        public List<SubBlister> ExtraBlisters;
        public CutState State;

        public CutResult()
        {
            CutOut = null;
            Current = null;
            ExtraBlisters = null;
            State = CutState.Failed;
        }

        public CutResult(SubBlister cutOut, SubBlister current, List<SubBlister> newBlisters, CutState state = CutState.Cutted)
        {
            CutOut = cutOut;
            Current = current;
            ExtraBlisters = newBlisters;
            State = state;
        }
    }
}

