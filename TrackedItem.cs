namespace PriceMonitor.Worker;

public class TrackedItem
{
    public int Id { get; set; }
    public long ChatId { get; set; } // Кому отправлять
    public string Url { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public DateTime LastChecked { get; set; }
}