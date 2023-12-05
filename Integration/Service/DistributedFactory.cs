using StackExchange.Redis;

namespace Integration.Service;
public static class DistributedFactory
{
    public static IConnectionMultiplexer CreateConnection()
    {
        return ConnectionMultiplexer.Connect("localhost");
    }
}
