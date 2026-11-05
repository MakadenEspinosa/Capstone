namespace ESEvents.Infrastructure.Mail;

public sealed class EmailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 25;
    public string FromAddress { get; set; } = "no-reply@esevents.local";
    public string FromName { get; set; } = "E-S Events";
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
