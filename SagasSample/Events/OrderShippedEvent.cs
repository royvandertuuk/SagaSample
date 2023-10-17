namespace SagasSample.Events;

public sealed class OrderShippedEvent
{
    public Guid OrderId { get; set; }
}
