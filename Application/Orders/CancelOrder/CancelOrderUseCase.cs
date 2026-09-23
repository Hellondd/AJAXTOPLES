using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WebApplication3.Application.Orders.CancelOrder;

public sealed class CancelOrderUseCase
{
    private readonly IOrderRepository _orders;
    private readonly AppDbContext _db;

    public CancelOrderUseCase(IOrderRepository orders, AppDbContext db)
    {
        _orders = orders; _db = db;
    }

    public async Task<Result<bool>> ExecuteAsync(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await _orders.GetAsync(cmd.OrderId, ct);
        if (order is null) return Error.NotFound("order.not_found", "Заказ не найден");
        if (order.IsShipped) return Error.Conflict("order.shipped", "Отгруженный заказ нельзя отменить");

        order.Cancel();
        await _db.SaveChangesAsync(ct);

        return true;
    }
}