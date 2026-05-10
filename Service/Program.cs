using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Service
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (ServiceHost host = new ServiceHost(typeof(ConsumptionService)))
            {
                host.Open();
                Console.WriteLine("Servis je otvoren na: net.tcp://localhost:4000/ConsumptionService");
                Console.WriteLine("Pritisni bilo koji taster za zatvaranje...");
                Console.ReadKey();
                host.Close();
            }
            Console.WriteLine("\nServis je zatvoren.");
            Console.ReadKey();
        }
    }
}
