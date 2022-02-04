using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kalmia.GoTextProtocol
{
    public class GTPException : Exception 
    {
        public GTPException(string message) : base(message) { }
    }
}
