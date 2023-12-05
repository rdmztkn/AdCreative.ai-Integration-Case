using Integration.Backend;
using Integration.Service;
using System.Diagnostics;

namespace Integration;

public abstract class Program
{
    public static void Main(string[] args)
    {
        var service = new DisributedItemIntegrationService();
        //var service = new ItemIntegrationService();

        var arr = new string[] { "a", "b", "c" };
        Parallel.For(0, 50, i =>
        {
            var randomText = arr[new Random().Next(0, 3)];
            var result = service.SaveItem(randomText);
            Console.WriteLine(result);
        });


        //ThreadPool.QueueUserWorkItem(_ => service.SaveItem("a"));
        //ThreadPool.QueueUserWorkItem(_ => service.SaveItem("b"));
        //ThreadPool.QueueUserWorkItem(_ => service.SaveItem("c"));

        //Thread.Sleep(500);

        //ThreadPool.QueueUserWorkItem(_ => service.SaveItem("a"));
        //ThreadPool.QueueUserWorkItem(_ => service.SaveItem("b"));
        //ThreadPool.QueueUserWorkItem(_ => service.SaveItem("c"));

        //Thread.Sleep(5000);

        Console.WriteLine("Everything recorded:");

        service.GetAllItems().ForEach(Console.WriteLine);

        Console.ReadLine();
    }
}