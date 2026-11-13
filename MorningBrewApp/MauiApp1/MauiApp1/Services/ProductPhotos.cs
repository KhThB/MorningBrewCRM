namespace MauiApp1.Services;

/// <summary>Bundled photos for the demo catalog; match names rather than database-specific IDs.</summary>
public static class ProductPhotos
{
    public static string? ForName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "cà phê sữa" => "images/products/ca-phe-sua.jpeg",
        "cà phê đen đá" => "images/products/ca-phe-den.jpeg",
        "bạc xỉu" => "images/products/bac-xiu.jpeg",
        "cold brew cam sả" => "images/products/cold-brew-white.jpeg",
        "trà đào" => "images/products/tra-dao.jpeg",
        "trà vải lài" => "images/products/tra-vai.jpeg",
        "trà sen vàng" => "images/products/tra-sen-white.jpeg",
        "bánh tiramisu" => "images/products/tiramisu.jpeg",
        "bánh croissant bơ" => "images/products/croissant.jpeg",
        "bánh mousse chanh dây" => "images/products/mousse.jpeg",
        _ => null
    };
}