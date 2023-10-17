using MassTransit;
using SagasSample.Events;

namespace SagasSample.Sagas;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State WaitingForPayment { get; private set; }
    public State WaitingForShipping { get; private set; }

    public Event<OrderCreatedEvent> OrderCreated { get; private set; }
    public Event<OrderPaymentReceivedEvent> PaymentReceived { get; private set; }
    public Event<OrderShippedEvent> OrderShipped { get; private set; }

    public Schedule<OrderState, OrderTimeoutExpiredEvent> TimeoutExpired { get; private set; }

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentReceived, x => x.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderShipped, x => x.CorrelateById(context => context.Message.OrderId));

        Schedule(() => TimeoutExpired, instance => instance.TimeoutExpiredTokenId, x =>
        {
            x.Delay = TimeSpan.FromSeconds(10);
            x.Received = r => r.CorrelateById(context => context.Message.OrderId);
        });

        SetCompletedWhenFinalized();

        Initially(
          When(OrderCreated)
              .Schedule(TimeoutExpired, context => context.Init<OrderTimeoutExpiredEvent>(new OrderTimeoutExpiredEvent() { OrderId = context.Saga.CorrelationId }))
              .Then(ctx => Console.WriteLine($"Order created. CorrelationId: '{ctx.Message.OrderId}'"))
              .TransitionTo(WaitingForPayment)
        );

        During(WaitingForPayment,
            When(TimeoutExpired.Received)
            .Then(ctx => Console.WriteLine($"Order cancelled after timeout, not payment received. CorrelationId: '{ctx.Message.OrderId}'"))
            .Finalize()
        );

        During(WaitingForPayment,
            When(PaymentReceived)
            .Then(ctx => Console.WriteLine($"Payment received. CorrelationId: '{ctx.Message.OrderId}'"))
            .TransitionTo(WaitingForShipping)
        );

        During(WaitingForShipping,
            When(OrderShipped)
            .Then(ctx => Console.WriteLine($"Order shipped. CorrelationId: '{ctx.Message.OrderId}'"))
            .Finalize()
        );
    }
}