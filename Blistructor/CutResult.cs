using System;
using System.Collections.Generic;
using System.Text;

namespace Blistructor
{
    public class CutResult
    {
        public readonly Blister CutOut;
        public readonly Blister Current;
        public readonly List<Blister> ExtraBlisters;
        public readonly CutState State;

        public CutResult(CutState state = CutState.Failed)
        {
            CutOut = null;
            Current = null;
            ExtraBlisters = new List<Blister>();
            State = state;
        }

        public CutResult(Blister cutOut, Blister current, List<Blister> newBlisters, CutState state = CutState.Succeed)
        {
            CutOut = cutOut;
            Current = current;
            ExtraBlisters = newBlisters;
            State = state;
        }
    }
}

