using StockSync.Orders.Application.IntegrationEvents;

namespace StockSync.Orders.Infrastructure.Outbox;

// Adds the event to the request DbContext. The repository SaveChangesAsync that stores
// the business change also stores the event, in the same database transaction.
public class EfIntegrationEventOutbox : IIntegrationEventOutbox
{
    private readonly OrdersDbContext _context;

    public EfIntegrationEventOutbox(OrdersDbContext context)
    {
        _context = context;
    }

    public void Enqueue(IIntegrationEvent integrationEvent) =>
        _context.OutboxMessages.Add(OutboxMessage.From(integrationEvent));
}
