using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Service
{
    class Program
    {
        static void Main()
        {
            ConsumptionService serviceInstance = new ConsumptionService();

            // Pretplate na evente
            serviceInstance.OnTransferStarted += (s, e) =>
                Console.WriteLine($">>> [EVENT] Prenos poceo: {e.CountryCode} {e.Date:yyyy-MM-dd}, ukupno {e.TotalSamples} uzoraka");

            serviceInstance.OnSampleReceived += (s, e) =>
                Console.WriteLine($">>> [EVENT] Uzorak primljen: {e.ReceivedCount}/{e.TotalSamples} ({e.PercentDone:F1}%)");

            serviceInstance.OnTransferCompleted += (s, e) =>
                Console.WriteLine($">>> [EVENT] Prenos zavrsen! Primljeno {e.TotalReceived}/{e.TotalSamples}");

            serviceInstance.OnWarningRaised += (s, e) =>
                Console.WriteLine($">>> [EVENT][{e.WarningType}] {e.Message}");

            ServiceHost host = new ServiceHost(serviceInstance);
            host.Open();
            Console.WriteLine("Servis pokrenut. Pritisni Enter za izlaz...");
            Console.ReadLine();
            host.Close();
        }
    }
}