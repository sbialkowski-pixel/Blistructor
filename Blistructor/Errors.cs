using System;
using System.Collections.Generic;
using System.Text;

namespace Blistructor
{

    [Serializable]
    public class AnchorException : Exception
    {
        public AnchorException() { }
        public AnchorException(string message) : base(message) { }
        public AnchorException(string message, Exception inner) : base(message, inner) { }
        protected AnchorException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
