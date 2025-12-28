using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Keytietkiem.Services.Background
{
    /// <summary>
    /// Work item for pushing a message via NotificationHub.
    /// Exactly one of GroupName/UserId must be set.
    /// </summary>
    public record NotificationDispatchItem(
        string MethodName,
        object Payload,
        string? GroupName = null,
        Guid? UserId = null
    );

    public interface INotificationDispatchQueue
    {
        ChannelReader<NotificationDispatchItem> Reader { get; }

        ValueTask QueueToUserAsync(Guid userId, object payload, string methodName, CancellationToken cancellationToken = default);
        ValueTask QueueToGroupAsync(string groupName, object payload, string methodName, CancellationToken cancellationToken = default);
    }

    public class NotificationDispatchQueue : INotificationDispatchQueue
    {
        // FE/Hub method name: clients should listen on this method.
        public const string MethodReceiveNotification = "ReceiveNotification";

        private readonly Channel<NotificationDispatchItem> _channel;

        public NotificationDispatchQueue()
        {
            _channel = Channel.CreateBounded<NotificationDispatchItem>(new BoundedChannelOptions(20000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public ChannelReader<NotificationDispatchItem> Reader => _channel.Reader;

        public ValueTask QueueToUserAsync(Guid userId, object payload, string methodName, CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(new NotificationDispatchItem(
                MethodName: methodName,
                Payload: payload,
                GroupName: null,
                UserId: userId
            ), cancellationToken);
        }

        public ValueTask QueueToGroupAsync(string groupName, object payload, string methodName, CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(new NotificationDispatchItem(
                MethodName: methodName,
                Payload: payload,
                GroupName: groupName,
                UserId: null
            ), cancellationToken);
        }
    }
}
