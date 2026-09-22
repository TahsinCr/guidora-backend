namespace Core.CrossCuttingConcerns.Messaging
{
    public class RabbitMqOptions
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guidora";
        public string Password { get; set; } = string.Empty;
        public string VirtualHost { get; set; } = "/";
        public string ExchangeName { get; set; } = "guidora.events";
        public string ClientProvidedName { get; set; } = "guidora-api";
    }
}
