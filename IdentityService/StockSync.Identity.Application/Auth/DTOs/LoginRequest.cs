namespace StockSync.Identity.Application.Auth.DTOs;

public record LoginRequest(
    string Email,
    string Password);

