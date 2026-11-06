using Bancada.Models;
using Bancada.Pages;
using Bancada.Services;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bancada.Tests;

public class PageTests : BunitContext
{
    private readonly OrderStore _store;

    public PageTests()
    {
        _store = new OrderStore(new MemoryStorage(), () => new DateTime(2026, 11, 6, 10, 0, 0));
        Services.AddSingleton(_store);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Dashboard_shows_kpis_and_all_columns()
    {
        var cut = Render<Dashboard>();
        Assert.Equal(_store.Orders.Count(o => o.Status != OrderStatus.Delivered).ToString(), cut.Find("[data-testid=kpi-open]").TextContent);
        Assert.Equal(4, cut.FindAll(".col").Count);
        Assert.Equal(_store.Orders.Count, cut.FindAll(".card-os").Count);
    }

    [Fact]
    public void Orders_search_filters_rows()
    {
        var cut = Render<Orders>();
        var all = cut.FindAll("tbody tr").Count;
        cut.Find("input[type=search]").Input("lenovo");
        Assert.Equal(1, cut.FindAll("tbody tr").Count);
        Assert.True(all > 1);
    }

    [Fact]
    public void Orders_status_chip_filters()
    {
        var cut = Render<Orders>();
        cut.FindAll(".chip").First(c => c.TextContent == "Entregue").Click();
        Assert.All(cut.FindAll("tbody tr"), r => Assert.Contains("Entregue", r.TextContent));
    }

    [Fact]
    public void OrderDetail_adding_a_line_updates_total_and_marks_quote_pending()
    {
        var o = _store.Orders.First(x => x.Status == OrderStatus.Received);
        var cut = Render<OrderDetail>(p => p.Add(x => x.Id, o.Id));
        cut.Find(".add-line input:not([type=number])").Change("Troca de HD");
        cut.FindAll(".add-line input[type=number]")[1].Change("250.50");
        cut.Find(".add-line").Submit();
        Assert.Equal("R$ 250,50", cut.Find("[data-testid=total]").TextContent);
        Assert.Equal(QuoteDecision.Pending, o.Decision);
    }

    [Fact]
    public void OrderDetail_rejects_empty_line()
    {
        var o = _store.Orders.First(x => x.Status == OrderStatus.Received);
        var cut = Render<OrderDetail>(p => p.Add(x => x.Id, o.Id));
        cut.Find(".add-line").Submit();
        Assert.NotNull(cut.Find(".error"));
        Assert.Empty(o.Lines);
    }

    [Fact]
    public void OrderDetail_advance_button_moves_status_and_blocks_pending_quote()
    {
        var o = _store.Orders.First(x => x.Status == OrderStatus.Diagnosing && x.Decision == QuoteDecision.Pending);
        var cut = Render<OrderDetail>(p => p.Add(x => x.Id, o.Id));
        cut.Find(".head-actions .primary").Click();
        Assert.Equal(OrderStatus.Ready, o.Status);
        cut.Find(".head-actions .primary").Click();
        Assert.Equal(OrderStatus.Ready, o.Status);
        Assert.Contains("aguarda", cut.Find(".notice").TextContent);
    }

    [Fact]
    public void OrderDetail_unknown_id_shows_not_found()
    {
        var cut = Render<OrderDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));
        Assert.Contains("não encontrada", cut.Markup);
    }

    [Fact]
    public void Receipt_lists_items_and_total()
    {
        var o = _store.Orders.First(x => x.Lines.Count > 1);
        var cut = Render<Receipt>(p => p.Add(x => x.Id, o.Id));
        Assert.Contains(Quote.Code(o.Number), cut.Markup);
        Assert.Contains(Quote.Money(o.Total), cut.Find("tfoot").TextContent);
    }

    [Fact]
    public void NewOrder_creates_order_with_new_customer()
    {
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        var cut = Render<NewOrder>();
        var before = _store.Orders.Count;
        cut.FindAll(".seg button")[1].Click();
        var inputs = cut.FindAll("fieldset input");
        inputs[0].Change("Cliente Teste");
        cut.FindAll("fieldset input")[2].Change("Notebook");
        cut.Find("textarea").Change("Não liga");
        cut.Find("form").Submit();
        Assert.Equal(before + 1, _store.Orders.Count);
        Assert.Contains(_store.Customers, c => c.Name == "Cliente Teste");
        Assert.Contains("os/", nav.Uri);
    }

    [Fact]
    public void NewOrder_validation_does_not_leave_orphan_customer()
    {
        var cut = Render<NewOrder>();
        var customers = _store.Customers.Count;
        cut.FindAll(".seg button")[1].Click();
        cut.FindAll("fieldset input")[0].Change("Fulano");
        cut.Find("form").Submit();
        Assert.NotNull(cut.Find(".error"));
        Assert.Equal(customers, _store.Customers.Count);
    }

    [Fact]
    public void Customers_page_adds_a_customer()
    {
        var cut = Render<Customers>();
        var before = _store.Customers.Count;
        cut.Find(".page-head button").Click();
        cut.Find("form input").Change("Nova Cliente");
        cut.Find("form").Submit();
        Assert.Equal(before + 1, _store.Customers.Count);
    }
}
