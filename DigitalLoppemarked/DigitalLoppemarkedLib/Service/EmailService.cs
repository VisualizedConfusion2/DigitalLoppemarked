namespace DigitalLoppemarkedLib.Service;

/// <summary>
/// Service for sending system emails.
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default);
}

/// <summary>
/// Mock implementation of IEmailService that logs emails to the console for development.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"[EMAIL SENT TO]: {to}");
        Console.WriteLine($"[SUBJECT]: {subject}");
        Console.WriteLine($"[BODY]:\n{body}");
        Console.WriteLine("--------------------------------------------------");
        return Task.CompletedTask;
    }
}