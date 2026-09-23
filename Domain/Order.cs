// Domain/Order.cs — добавить:
public bool IsShipped { get; private set; }
public bool IsCancelled { get; private set; }

public void Cancel()
{
    if (IsShipped) throw new InvalidOperationException("Cannot cancel shipped order");
    IsCancelled = true;
}