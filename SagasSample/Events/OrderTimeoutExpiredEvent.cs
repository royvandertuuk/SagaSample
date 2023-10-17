namespace SagasSample.Events;

public sealed class OrderTimeoutExpiredEvent
{
    public Guid OrderId { get; set; }
}