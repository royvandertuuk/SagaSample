using MassTransit;

namespace SagasSample.Sagas;

public class OrderState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public byte[] RowVersion { get; set; }
    public Guid? TimeoutExpiredTokenId { get; set; }
}