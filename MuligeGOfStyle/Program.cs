namespace MuligeGOfStyle
{
    public class Program
    {
        public static void Main()
        {
            // Program opretter WeatherStation (subject/publisher)
            var weatherStation = new WeatherStation();

            // Program opretter Logger og Display (subscribers/observers)
            var logger = new Logger();
            var display = new Display();

            // Observers tilknyttes subject
            weatherStation.Attach(logger);
            weatherStation.Attach(display);

            // Simuler temperaturændringer - alle observers notifies
            weatherStation.SetTemp(21.5f);
            weatherStation.SetTemp(23.0f);

            // Fjern Logger og vis at kun Display nu modtager opdateringer
            weatherStation.Detach(logger);
            weatherStation.SetTemp(19.8f);

            Console.WriteLine("\nTryk på en tast for at afslutte...");
            Console.ReadKey();
        }
    }

}
