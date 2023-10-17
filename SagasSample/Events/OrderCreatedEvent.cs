namespace SagasSample.Events;

public sealed class OrderCreatedEvent
{
    public Guid OrderId { get; set; }
}
