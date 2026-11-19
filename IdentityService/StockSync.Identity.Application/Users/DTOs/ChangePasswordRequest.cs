namespace StockSync.Identity.Application.Users.DTOs;

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

