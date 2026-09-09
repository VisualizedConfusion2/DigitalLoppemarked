using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MuligeGOfStyle
{
    public class WeatherStation
    {
        public readonly List<IObserver> _observers = new();
        private float _temperature;
        public void SetTemp(float temp)
        {
            Console.WriteLine($"\nWeatherStation: Ny temperatur målt: {temp}°C");
            _temperature = temp;
            Notify();
        }

        public void Attach(IObserver observer)
        {
            _observers.Add(observer);
        }
        public void Detach(IObserver observer)
        {
            _observers.Add(observer);
        }
        public void Notify()
        {
            foreach (var observer in _observers)
            {
                observer.Update(_temperature);
            }

        }

    }
}
