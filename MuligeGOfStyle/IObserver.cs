using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MuligeGOfStyle
{
    // Subscriber (observer)
    // Modtager temperaturændringer.
    public interface IObserver
    {
        void Update(float temperature);
    }

}
