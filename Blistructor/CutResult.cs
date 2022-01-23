using System;
using System.Collections.Generic;
using System.Text;

namespace Blistructor
{
    public class CutResult
    {
        public readonly SubBlister CutOut;
        public readonly SubBlister Current;
        public readonly List<SubBlister> ExtraBlisters;
        public readonly CutState State;

        public CutResult(CutState state = CutState.Failed)
        {
            CutOut = null;
            Current = null;
            ExtraBlisters = new List<SubBlister>();
            State = state;
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

