using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.Json;
namespace MorningBrew.Admin.Services;

/// <summary>[CRM-07] Xuất dữ liệu ra file Excel (.xlsx) không cần thư viện ngoài: tự ghi các phần XML tối thiểu của
/// định dạng Office Open XML vào file zip. Mỗi sheet có dòng tiêu đề in đậm, cố định dòng đầu, số giữ dạng số
/// (cộng / lọc được trong Excel). Không dùng CSV vì Excel tiếng Việt dùng dấu ";" làm phân cách nên CSV dấu "," bị dồn một cột.</summary>
public sealed class XlsxSheet(string name)
{
    public string Name {get;}=CleanName(name);
    public List<string> Headers {get;}=[];
    public List<object?[]> Rows {get;}=[];

    private static string CleanName(string name)
    {
        var s=new string(name.Select(c=>"[]:*?/\\".Contains(c)?' ':c).ToArray()).Trim();
        return s.Length>31?s[..31]:s.Length==0?"Sheet":s;
    }

    /// <summary>Dựng sheet từ các dòng API (snake_case). Mỗi cột: (khóa, tiêu đề). Số giữ dạng số; còn lại hiển thị
    /// như trên web (RowDisplay: trạng thái tiếng Việt, giờ máy, ngày sinh dd/MM/yyyy). <paramref name="format"/> ghi đè từng ô.</summary>
    public static XlsxSheet From(string name,IEnumerable<Dictionary<string,JsonElement>> rows,(string Key,string Label)[] columns,
        Func<string,Dictionary<string,JsonElement>,object?>? format=null)
    {
        var sheet=new XlsxSheet(name);
        sheet.Headers.AddRange(columns.Select(c=>c.Label));
        foreach(var row in rows)
            sheet.Rows.Add(columns.Select(c=>format?.Invoke(c.Key,row) ?? Cell(c.Key,row)).ToArray());
        return sheet;
    }
    public static object? Cell(string key,Dictionary<string,JsonElement> row)
    {
        if(!row.TryGetValue(key,out var v) || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        return v.ValueKind==JsonValueKind.Number ? v.GetDecimal() : RowDisplay.Value(key,v);
    }
}

public static class XlsxExport
{
    public const string ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build(IReadOnlyList<XlsxSheet> sheets)
    {
        using var ms=new MemoryStream();
        using(var zip=new ZipArchive(ms,ZipArchiveMode.Create,true))
        {
            void Add(string path,string xml){ using var w=new StreamWriter(zip.CreateEntry(path,CompressionLevel.Fastest).Open(),new UTF8Encoding(false)); w.Write(xml); }
            var sheetTypes=string.Concat(sheets.Select((_,i)=>$"<Override PartName=\"/xl/worksheets/sheet{i+1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
            Add("[Content_Types].xml","<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"+
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/>"+
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"+
                "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"+sheetTypes+"</Types>");
            Add("_rels/.rels","<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add("xl/workbook.xml","<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>"+
                string.Concat(sheets.Select((s,i)=>$"<sheet name=\"{Esc(s.Name)}\" sheetId=\"{i+1}\" r:id=\"rId{i+1}\"/>"))+"</sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels","<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
                string.Concat(sheets.Select((_,i)=>$"<Relationship Id=\"rId{i+1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i+1}.xml\"/>"))+
                $"<Relationship Id=\"rId{sheets.Count+1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            // Kiểu: 0 thường, 1 tiêu đề đậm nền nâu nhạt, 2 số nguyên có phân cách nghìn, 3 số thập phân
            Add("xl/styles.xml","<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"+
                "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.###\"/></numFmts>"+
                "<fonts count=\"2\"><font><sz val=\"10\"/><name val=\"Arial\"/></font><font><b/><sz val=\"10\"/><name val=\"Arial\"/></font></fonts>"+
                "<fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFF3E6D3\"/></patternFill></fill></fills>"+
                "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"+
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"+
                "<cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"+
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>"+
                "<xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"+
                "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>"+
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
            for(var i=0;i<sheets.Count;i++) Add($"xl/worksheets/sheet{i+1}.xml",SheetXml(sheets[i]));
        }
        return ms.ToArray();
    }

    private static string SheetXml(XlsxSheet s)
    {
        var sb=new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        // Độ rộng cột theo nội dung dài nhất (giới hạn 10–60 ký tự).
        sb.Append("<cols>");
        for(var c=0;c<s.Headers.Count;c++)
        {
            var width=Math.Clamp(s.Rows.Select(r=>c<r.Length?Text(r[c]).Length:0).Append(s.Headers[c].Length).Max()+2,10,60);
            sb.Append($"<col min=\"{c+1}\" max=\"{c+1}\" width=\"{width}\" customWidth=\"1\"/>");
        }
        sb.Append("</cols><sheetData>");
        AppendRow(sb,1,s.Headers.Cast<object?>().ToArray(),true);
        for(var r=0;r<s.Rows.Count;r++) AppendRow(sb,r+2,s.Rows[r],false);
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb,int index,object?[] cells,bool header)
    {
        sb.Append($"<row r=\"{index}\">");
        for(var c=0;c<cells.Length;c++)
        {
            var refName=Column(c)+index;
            switch(cells[c])
            {
                case null: break;
                case decimal or int or long or double:
                    var n=Convert.ToDecimal(cells[c],CultureInfo.InvariantCulture);
                    sb.Append($"<c r=\"{refName}\" s=\"{(header?1:n==Math.Truncate(n)?2:3)}\"><v>{n.ToString(CultureInfo.InvariantCulture)}</v></c>");
                    break;
                default:
                    sb.Append($"<c r=\"{refName}\" t=\"inlineStr\"{(header?" s=\"1\"":"")}><is><t xml:space=\"preserve\">{Esc(Text(cells[c]))}</t></is></c>");
                    break;
            }
        }
        sb.Append("</row>");
    }

    private static string Text(object? v)=>v switch{null=>"",decimal d=>d.ToString("#,##0.###",CultureInfo.InvariantCulture),_=>v.ToString()??""};
    private static string Column(int i){ var s=""; i++; while(i>0){ var m=(i-1)%26; s=(char)('A'+m)+s; i=(i-1)/26; } return s; }
    // Bỏ ký tự điều khiển không hợp lệ trong XML rồi escape.
    private static string Esc(string s)=>SecurityElement.Escape(new string(s.Where(ch=>ch is '\t' or '\n' or '\r' || ch>=' ').ToArray()))!;

    /// <summary>Tên file kèm ngày giờ: MorningBrew_KhachHang_20261115_1430.xlsx</summary>
    public static string FileName(string what)=>$"MorningBrew_{what}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
}
