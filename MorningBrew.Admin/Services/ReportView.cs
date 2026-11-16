using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
namespace MorningBrew.Admin.Services;

// Đọc an toàn số liệu báo cáo từ JSON của API và tính tỷ lệ phần trăm để hiển thị.
public static class ReportView
{
    public static List<Dictionary<string,JsonElement>> Rows(Dictionary<string,JsonElement> data,string key)
        => data.TryGetValue(key,out var value) && value.ValueKind==JsonValueKind.Array
            ? value.Deserialize<List<Dictionary<string,JsonElement>>>() ?? [] : [];
    public static Dictionary<string,JsonElement> Object(Dictionary<string,JsonElement> data,string key)
        => data.TryGetValue(key,out var value) && value.ValueKind==JsonValueKind.Object
            ? value.Deserialize<Dictionary<string,JsonElement>>() ?? [] : [];
    public static int Int(Dictionary<string,JsonElement> row,string key)
        => row.TryGetValue(key,out var v) && v.ValueKind==JsonValueKind.Number ? (int)v.GetDecimal() : 0;
    public static decimal? Decimal(Dictionary<string,JsonElement> row,string key)
        => row.TryGetValue(key,out var v) && v.ValueKind==JsonValueKind.Number ? v.GetDecimal() : null;
    public static string Text(Dictionary<string,JsonElement> row,string key)
        => row.TryGetValue(key,out var v) && v.ValueKind!=JsonValueKind.Null ? v.ToString() : "";
    public static double Percent(int part,int whole) => whole<=0 ? 0 : Math.Round(100.0*part/whole,1);
    public static string Pct(double value) => value.ToString("0.#",CultureInfo.GetCultureInfo("vi-VN"))+"%";
    // Dùng trong thuộc tính style: luôn dấu chấm thập phân.
    public static string Width(double value) => Math.Clamp(value,0,100).ToString("0.#",CultureInfo.InvariantCulture)+"%";
    public static string Number(decimal? value) => value is null ? "—" : value.Value.ToString("0.##",CultureInfo.GetCultureInfo("vi-VN"));
    public static string QuestionType(string type) => type switch
    {
        "Rating"=>"Chấm điểm", "SingleChoice"=>"Chọn một", "MultipleChoice"=>"Chọn nhiều", "Text"=>"Tự luận", _=>type
    };
    // Một dòng thanh tỷ lệ: nhãn | thanh | số lượng · %.
    public static IHtmlContent Bar(string label,int count,double percent,string? note=null)
    {
        var e=HtmlEncoder.Default;
        var noteHtml=string.IsNullOrEmpty(note) ? "" : $"<small>{e.Encode(note)}</small>";
        return new HtmlString($"<div class=\"stat-bar\"><span class=\"stat-bar-label\">{e.Encode(label)}{noteHtml}</span><div class=\"stat-bar-track\"><i style=\"width:{Width(percent)}\"></i></div><b>{count} <small>· {e.Encode(Pct(percent))}</small></b></div>");
    }
}
