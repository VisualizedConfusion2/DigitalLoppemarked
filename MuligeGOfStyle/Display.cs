using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MuligeGOfStyle
{
    public class Display : IObserver
    {
        public void Update(float temperature)
        {
            Console.WriteLine($"[Display] Viser: Aktuel temperatur er {temperature}°C");
        }
    }

}
