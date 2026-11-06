using System.Text.Json;
using Bancada.Models;

namespace Bancada.Services;

public class OrderStore
{
    private const string Key = "bancada.v1";
    private readonly IStorage _storage;
    private readonly Func<DateTime> _now;

    public List<Customer> Customers { get; private set; } = new();
    public List<ServiceOrder> Orders { get; private set; } = new();
    public event Action? Changed;

    public OrderStore(IStorage storage, Func<DateTime>? now = null)
    {
        _storage = storage;
        _now = now ?? (() => DateTime.Now);
        var raw = storage.Get(Key);
        if (!string.IsNullOrEmpty(raw) && TryLoad(raw)) return;
        LoadDemo();
    }

    private record Snapshot(List<Customer> Customers, List<ServiceOrder> Orders);

    private bool TryLoad(string raw)
    {
        try
        {
            var s = JsonSerializer.Deserialize<Snapshot>(raw);
            if (s is null) return false;
            Customers = s.Customers; Orders = s.Orders;
            return true;
        }
        catch (JsonException) { return false; } // corrupted storage: fall back to demo data
    }

    private void Save()
    {
        _storage.Set(Key, JsonSerializer.Serialize(new Snapshot(Customers, Orders)));
        Changed?.Invoke();
    }

    public Customer? Customer(Guid id) => Customers.FirstOrDefault(c => c.Id == id);
    public ServiceOrder? Order(Guid id) => Orders.FirstOrDefault(o => o.Id == id);
    public string CustomerName(ServiceOrder o) => Customer(o.CustomerId)?.Name ?? "—";

    public Customer SaveCustomer(Customer c)
    {
        if (string.IsNullOrWhiteSpace(c.Name)) throw new ArgumentException("Nome obrigatório.");
        if (!Customers.Contains(c)) Customers.Add(c);
        Save();
        return c;
    }

    public ServiceOrder CreateOrder(ServiceOrder o)
    {
        if (Customer(o.CustomerId) is null) throw new ArgumentException("Cliente inválido.");
        if (string.IsNullOrWhiteSpace(o.Device) || string.IsNullOrWhiteSpace(o.Defect))
            throw new ArgumentException("Aparelho e defeito são obrigatórios.");
        o.Number = Orders.Count == 0 ? 1001 : Orders.Max(x => x.Number) + 1;
        o.CreatedAt = _now();
        o.Status = OrderStatus.Received;
        o.History = new() { new StatusChange(OrderStatus.Received, o.CreatedAt) };
        Orders.Add(o);
        Save();
        return o;
    }

    public void Update(ServiceOrder _) => Save();

    public static OrderStatus? Next(OrderStatus s) => s == OrderStatus.Delivered ? null : s + 1;

    // Business rule: a device can't be handed back while the quote is still waiting for the customer.
    public string? CanAdvance(ServiceOrder o)
    {
        if (Next(o.Status) is null) return "A OS já foi entregue.";
        if (o.Status == OrderStatus.Ready && o.Decision == QuoteDecision.Pending)
            return "O orçamento ainda aguarda resposta do cliente.";
        return null;
    }

    public bool Advance(ServiceOrder o)
    {
        if (CanAdvance(o) is not null) return false;
        o.Status = Next(o.Status)!.Value;
        o.History.Add(new StatusChange(o.Status, _now()));
        Save();
        return true;
    }

    public IEnumerable<ServiceOrder> Search(string? text, OrderStatus? status)
    {
        var q = (text ?? "").Trim();
        return Orders
            .Where(o => status is null || o.Status == status)
            .Where(o => q.Length == 0
                || Quote.Code(o.Number).Contains(q, StringComparison.OrdinalIgnoreCase)
                || CustomerName(o).Contains(q, StringComparison.OrdinalIgnoreCase)
                || (o.Device + " " + o.Brand + " " + o.Model + " " + o.Serial).Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(o => o.Number);
    }

    public decimal RevenueSince(DateTime from) =>
        Orders.Where(o => o.Status == OrderStatus.Delivered && o.Decision != QuoteDecision.Declined && o.DeliveredAt >= from)
              .Sum(o => o.Total);

    public void ResetDemo() { LoadDemo(); }

    private void LoadDemo()
    {
        var now = _now();
        Customers = new();
        Orders = new();
        Customer C(string n, string p, string e) { var c = new Customer { Name = n, Phone = p, Email = e }; Customers.Add(c); return c; }
        var mariana = C("Mariana Teixeira", "(41) 99812-3345", "mariana@exemplo.com");
        var carlos = C("Carlos Eduardo Pinto", "(41) 99677-2210", "carlos@exemplo.com");
        var lia = C("Lia Marques", "(41) 98844-0912", "");
        var padaria = C("Padaria Pão Quente", "(41) 3322-5500", "contato@paoquente.example");

        void O(Customer c, string dev, string brand, string model, string defect, string diag, OrderStatus st, QuoteDecision dec, int daysAgo, decimal disc, params QuoteLine[] lines)
        {
            var o = new ServiceOrder
            {
                CustomerId = c.Id, Device = dev, Brand = brand, Model = model, Defect = defect, Diagnosis = diag,
                Serial = "SN" + (4000 + Orders.Count * 137), Accessories = "Carregador", Decision = dec, Discount = disc, Lines = lines.ToList(),
            };
            o.Number = 1001 + Orders.Count;
            o.CreatedAt = now.AddDays(-daysAgo);
            o.History = new() { new StatusChange(OrderStatus.Received, o.CreatedAt) };
            for (var s = OrderStatus.Diagnosing; s <= st; s++) o.History.Add(new StatusChange(s, o.CreatedAt.AddHours(5 * (int)s + 2)));
            o.Status = st;
            Orders.Add(o);
        }

        O(mariana, "Notebook", "Dell", "Inspiron 15 3520", "Não liga, luz de carga pisca.", "Placa-mãe OK; bateria em curto.", OrderStatus.Delivered, QuoteDecision.Approved, 12, 0,
            new("Bateria 41Wh original", 1, 389.90m), new("Mão de obra", 1, 80m));
        O(carlos, "Smartphone", "Samsung", "Galaxy A54", "Tela trincada, touch falhando.", "Troca de módulo de tela.", OrderStatus.Delivered, QuoteDecision.Approved, 7, 20m,
            new("Módulo de tela AMOLED", 1, 520m), new("Película de vidro", 1, 35m), new("Mão de obra", 1, 90m));
        O(lia, "Console", "Sony", "PlayStation 4 Slim", "Superaquecendo e desligando.", "Limpeza e troca de pasta térmica.", OrderStatus.Ready, QuoteDecision.Approved, 4, 0,
            new("Pasta térmica", 1, 45m), new("Limpeza completa", 1, 120m));
        O(padaria, "Impressora", "Epson", "L3250", "Almofada de tinta cheia.", "Reset do contador + troca de almofada.", OrderStatus.Diagnosing, QuoteDecision.Pending, 2, 0,
            new("Kit almofada de tinta", 1, 160m), new("Mão de obra", 1, 110m));
        O(mariana, "Tablet", "Apple", "iPad 9ª geração", "Não carrega.", "", OrderStatus.Diagnosing, QuoteDecision.None, 1, 0);
        O(carlos, "Notebook", "Lenovo", "IdeaPad 3", "Lento e com ruído no HD.", "", OrderStatus.Received, QuoteDecision.None, 0, 0);
        Save();
    }
}
