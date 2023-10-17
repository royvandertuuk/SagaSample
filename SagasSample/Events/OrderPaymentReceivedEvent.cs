namespace SagasSample.Events;

public sealed class OrderPaymentReceivedEvent
{
    public Guid OrderId { get; set; }
}
