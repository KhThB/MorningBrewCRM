using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MorningBrew.Admin.Services;
namespace MorningBrew.Admin.Pages;
public class AnswerModel(CrmApi api):PageModel
{
    [BindProperty(SupportsGet=true)] public int Id{get;set;}
    public Dictionary<string,List<Dictionary<string,JsonElement>>> Survey{get;set;}=new(){["questions"]=[],["options"]=[]};
    public async Task OnGetAsync()
    {
        try{Survey=await api.Get<Dictionary<string,List<Dictionary<string,JsonElement>>>>($"v1/me/surveys/{Id}");}
        catch(CrmApiException e){TempData["Error"]=e.Message;}
    }
    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();
        try
        {
            var answers=new List<object>();
            foreach(var q in Survey["questions"])
            {
                var id=q["question_id"].GetInt32();
                var values=Request.Form["answer_"+id];
                if(values.Count==0||values.All(string.IsNullOrWhiteSpace))continue;
                var type=q["type"].ToString();
                answers.Add(new {questionId=id,text=type=="Text"?values.ToString():null,rating=type=="Rating"?(int?)int.Parse(values.ToString()):null,
                    optionIds=type is "SingleChoice" or "MultipleChoice"?values.Select(x=>int.Parse(x!)).ToArray():null});
            }
            await api.Send(HttpMethod.Post,$"v1/me/surveys/{Id}:submit",new{answers});
            TempData["Success"]="Cảm ơn bạn đã hoàn thành khảo sát.";
            return RedirectToPage("/Portal",new{section="surveys"});
        }
        catch(CrmApiException e){TempData["Error"]=e.Message;}
        catch(FormatException){TempData["Error"]="Câu trả lời không hợp lệ.";}
        return Page();
    }
}
