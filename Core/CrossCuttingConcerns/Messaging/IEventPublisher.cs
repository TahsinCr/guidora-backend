using System.Threading;
using System.Threading.Tasks;

namespace Core.CrossCuttingConcerns.Messaging
{
    public interface IEventPublisher
    {
        Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken cancellationToken = default);
    }
}
