using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Common;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ChannelFactory<IConsumptionService> factory = null;
            IConsumptionService proxy = null;

            try
            {
                factory = new ChannelFactory<IConsumptionService>("ConsumptionServiceEndpoint");
                proxy = factory.CreateChannel();

                Console.WriteLine("Klijent je spreman. Pritisni taster da bi poslao test poruku...");
                Console.ReadKey();

                // Test poziv - posaljemo prazan StartSession da vidimo da konekcija radi
                SessionMeta meta = new SessionMeta
                {
                    CountryCode = "DE",
                    Date = new DateTime(2017, 6, 15),
                    SourceFileName = "test.csv",
                    TotalSamples = 0
                };

                proxy.StartSession(meta);
                proxy.EndSession();

                Console.WriteLine("Test sesija uspesna.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GRESKA] {ex.Message}");
            }
            finally
            {
                // Zatvaranje proxy-ja i factory-ja kako god da se zavrsilo (uspeh ili greska)
                if (proxy != null)
                {
                    try
                    {
                        ((IClientChannel)proxy).Close();
                        Console.WriteLine("Proxy zatvoren.");
                    }
                    catch
                    {
                        ((IClientChannel)proxy).Abort();
                        Console.WriteLine("Proxy abort-ovan (nije se zatvorio normalno).");
                    }
                }

                if (factory != null)
                {
                    try
                    {
                        factory.Close();
                        Console.WriteLine("Factory zatvoren.");
                    }
                    catch
                    {
                        factory.Abort();
                        Console.WriteLine("Factory abort-ovan.");
                    }
                }
            }

            Console.WriteLine("Klijent zavrsava. Pritisni bilo koji taster...");
            Console.ReadKey();
        }
    }
}
