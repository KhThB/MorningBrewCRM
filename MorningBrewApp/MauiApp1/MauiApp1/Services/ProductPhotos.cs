namespace MauiApp1.Services;

/// <summary>Bundled photos for the demo catalog; match names rather than database-specific IDs.</summary>
public static class ProductPhotos
{
    public static string? ForName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "cà phê sữa" => "images/products/ca-phe-sua.jpg",
        "cà phê đen đá" => "images/products/ca-phe-den.jpg",
        "bạc xỉu" => "images/products/bac-xiu.jpg",
        "cold brew cam sả" => "images/products/cold-brew-white.jpg",
        "trà đào" => "images/products/tra-dao.png",
        "trà vải lài" => "images/products/tra-vai.jpg",
        "trà sen vàng" => "images/products/tra-sen-white.png",
        "bánh tiramisu" => "images/products/tiramisu.jpg",
        "bánh croissant bơ" => "images/products/croissant.jpg",
        "bánh mousse chanh dây" => "images/products/mousse.jpg",
        _ => null
    };
}