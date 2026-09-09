using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MuligeGOfStyle
{
    public class Logger : IObserver
    {
        public void Update(float temperature)
        {
            Console.WriteLine($"[Logger] Log: Temperatur ændret til {temperature}°C ({DateTime.Now:HH:mm:ss})");
        }
    }

}
