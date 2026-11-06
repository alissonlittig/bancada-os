namespace Bancada.Models;

public enum OrderStatus { Received, Diagnosing, Ready, Delivered }

public enum QuoteDecision { None, Pending, Approved, Declined }

public record QuoteLine(string Description, int Qty, decimal UnitPrice)
{
    public decimal Total => Qty * UnitPrice;
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Notes { get; set; } = "";
}

public record StatusChange(OrderStatus Status, DateTime At);

public class ServiceOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public Guid CustomerId { get; set; }
    public string Device { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public string Serial { get; set; } = "";
    public string Accessories { get; set; } = "";
    public string Defect { get; set; } = "";
    public string Diagnosis { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.Received;
    public QuoteDecision Decision { get; set; } = QuoteDecision.None;
    public List<QuoteLine> Lines { get; set; } = new();
    public decimal Discount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<StatusChange> History { get; set; } = new();

    public decimal Subtotal => Quote.Subtotal(Lines);
    public decimal Total => Quote.Total(Lines, Discount);
    public DateTime? DeliveredAt => History.LastOrDefault(h => h.Status == OrderStatus.Delivered)?.At;
}
