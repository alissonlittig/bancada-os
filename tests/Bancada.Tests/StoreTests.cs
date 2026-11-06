using Bancada.Models;
using Bancada.Services;

namespace Bancada.Tests;

public class StoreTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 10, 0, 0);

    private static OrderStore Fresh() => new(new MemoryStorage(), () => Now);

    [Fact]
    public void Money_uses_brazilian_format()
    {
        Assert.Equal("R$ 1.234,50", Quote.Money(1234.5m));
        Assert.Equal("R$ 0,00", Quote.Money(0));
    }

    [Fact]
    public void Total_clamps_discount_to_subtotal()
    {
        var lines = new[] { new QuoteLine("Tela", 2, 100m), new QuoteLine("Mão de obra", 1, 50m) };
        Assert.Equal(250m, Quote.Subtotal(lines));
        Assert.Equal(230m, Quote.Total(lines, 20m));
        Assert.Equal(0m, Quote.Total(lines, 9999m));
        Assert.Equal(250m, Quote.Total(lines, -5m));
    }

    [Fact]
    public void New_order_gets_next_number_and_history()
    {
        var s = Fresh();
        var last = s.Orders.Max(o => o.Number);
        var o = s.CreateOrder(new ServiceOrder { CustomerId = s.Customers[0].Id, Device = "Rádio", Defect = "Mudo" });
        Assert.Equal(last + 1, o.Number);
        Assert.Equal(OrderStatus.Received, o.Status);
        Assert.Single(o.History);
    }

    [Fact]
    public void Create_rejects_unknown_customer_and_missing_fields()
    {
        var s = Fresh();
        Assert.Throws<ArgumentException>(() => s.CreateOrder(new ServiceOrder { CustomerId = Guid.NewGuid(), Device = "x", Defect = "y" }));
        Assert.Throws<ArgumentException>(() => s.CreateOrder(new ServiceOrder { CustomerId = s.Customers[0].Id, Device = " ", Defect = "y" }));
    }

    [Fact]
    public void Advance_walks_the_whole_flow_and_stops_at_delivered()
    {
        var s = Fresh();
        var o = s.CreateOrder(new ServiceOrder { CustomerId = s.Customers[0].Id, Device = "TV", Defect = "Sem imagem" });
        Assert.True(s.Advance(o)); Assert.True(s.Advance(o)); Assert.True(s.Advance(o));
        Assert.Equal(OrderStatus.Delivered, o.Status);
        Assert.False(s.Advance(o));
        Assert.Equal(4, o.History.Count);
    }

    [Fact]
    public void Cannot_deliver_while_quote_is_pending()
    {
        var s = Fresh();
        var o = s.CreateOrder(new ServiceOrder { CustomerId = s.Customers[0].Id, Device = "TV", Defect = "x", Decision = QuoteDecision.Pending });
        s.Advance(o); s.Advance(o);
        Assert.Equal(OrderStatus.Ready, o.Status);
        Assert.NotNull(s.CanAdvance(o));
        Assert.False(s.Advance(o));
        o.Decision = QuoteDecision.Approved;
        Assert.True(s.Advance(o));
    }

    [Fact]
    public void Search_matches_customer_device_and_code_and_filters_status()
    {
        var s = Fresh();
        Assert.Contains(s.Search("mariana", null), o => s.CustomerName(o).StartsWith("Mariana"));
        Assert.All(s.Search("dell", null), o => Assert.Equal("Dell", o.Brand));
        Assert.Single(s.Search("OS-1001", null));
        Assert.All(s.Search(null, OrderStatus.Ready), o => Assert.Equal(OrderStatus.Ready, o.Status));
        Assert.Empty(s.Search("zzzz", null));
    }

    [Fact]
    public void Data_persists_and_reloads_from_storage()
    {
        var storage = new MemoryStorage();
        var a = new OrderStore(storage, () => Now);
        var o = a.CreateOrder(new ServiceOrder { CustomerId = a.Customers[0].Id, Device = "Câmera", Defect = "Lente travada" });
        o.Lines.Add(new QuoteLine("Lente", 1, 99.9m));
        a.Update(o);
        var b = new OrderStore(storage, () => Now);
        var back = b.Order(o.Id);
        Assert.NotNull(back);
        Assert.Equal(99.9m, back!.Total);
    }

    [Fact]
    public void Corrupted_storage_falls_back_to_demo_data()
    {
        var storage = new MemoryStorage();
        storage.Set("bancada.v1", "{not json");
        var s = new OrderStore(storage, () => Now);
        Assert.NotEmpty(s.Orders);
    }

    [Fact]
    public void Revenue_counts_only_delivered_orders_in_range()
    {
        var s = Fresh();
        var all = s.RevenueSince(DateTime.MinValue);
        var expected = s.Orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.Total);
        Assert.Equal(expected, all);
        Assert.Equal(0m, s.RevenueSince(Now.AddYears(1)));
    }

    [Fact]
    public void Customer_requires_name()
    {
        var s = Fresh();
        Assert.Throws<ArgumentException>(() => s.SaveCustomer(new Customer { Name = "  " }));
    }
}
