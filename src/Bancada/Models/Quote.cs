using System.Globalization;

namespace Bancada.Models;

public static class Quote
{
    public static decimal Subtotal(IEnumerable<QuoteLine> lines) => lines.Sum(l => l.Total);

    // The discount can never exceed the subtotal or go negative.
    public static decimal Total(IEnumerable<QuoteLine> lines, decimal discount)
    {
        var sub = Subtotal(lines);
        return sub - Math.Clamp(discount, 0, sub);
    }

    // Manual pt-BR formatting so it works with invariant globalization (smaller WASM payload).
    public static string Money(decimal v) =>
        "R$ " + v.ToString("N2", CultureInfo.InvariantCulture).Replace(',', '#').Replace('.', ',').Replace('#', '.');

    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string Stamp(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public static string Code(int n) => "OS-" + n.ToString("D4", CultureInfo.InvariantCulture);

    public static string StatusLabel(OrderStatus s) => s switch
    {
        OrderStatus.Received => "Recebido",
        OrderStatus.Diagnosing => "Em diagnóstico",
        OrderStatus.Ready => "Pronto p/ retirada",
        _ => "Entregue",
    };

    public static string DecisionLabel(QuoteDecision d) => d switch
    {
        QuoteDecision.Pending => "Aguardando cliente",
        QuoteDecision.Approved => "Aprovado",
        QuoteDecision.Declined => "Recusado",
        _ => "Sem orçamento",
    };
}
