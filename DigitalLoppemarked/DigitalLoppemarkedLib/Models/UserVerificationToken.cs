namespace DigitalLoppemarkedLib.Models;

public class UserVerificationToken
{
    public int UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}