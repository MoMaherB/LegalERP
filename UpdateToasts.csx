using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

var pagesDir = @"C:\MoMaherB\LegalERP.Web\LegalERP.Web\Components\Pages";
var files = Directory.GetFiles(pagesDir, "*.razor", SearchOption.AllDirectories);

var apiCallRegex = new Regex(@"(await\s+\w+Api\.(?:Delete|Update|Create|Add|Upload)\w*Async\([^)]*\);)");

foreach (var file in files)
{
    var content = File.ReadAllText(file);
    bool modified = false;

    // Check if it has any mutating API calls
    if (!apiCallRegex.IsMatch(content)) continue;

    // Inject ToastService if not present
    if (!content.Contains("@inject LegalERP.Web.Services.Toast.IToastService ToastService"))
    {
        content = content.Replace("@inject NavigationManager NavManager", "@inject NavigationManager NavManager\n@inject LegalERP.Web.Services.Toast.IToastService ToastService");
        // Fallback if NavManager isn't there
        if (!content.Contains("ToastService"))
        {
             content = Regex.Replace(content, @"(@inject\s+[^\n]+)\n", "\n@inject LegalERP.Web.Services.Toast.IToastService ToastService\n", RegexOptions.Compiled);
        }
        modified = true;
    }

    var lines = content.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        var match = apiCallRegex.Match(lines[i]);
        if (match.Success)
        {
            // check if the line already contains ToastService
            if (!lines[i].Contains("ToastService.ShowSuccess") && 
                (i + 1 >= lines.Length || !lines[i+1].Contains("ToastService.ShowSuccess")))
            {
                var indentMatch = Regex.Match(lines[i], @"^\s*");
                var indent = indentMatch.Success ? indentMatch.Value : "";
                
                // insert after the call but handle cases where it's part of an inline statement or has NavManager
                // simplest: just insert it on the next line
                lines[i] = lines[i] + "\n" + indent + "ToastService.ShowSuccess(Loc[\"OperationSuccess\"].Value);";
                modified = true;
            }
        }
    }

    if (modified)
    {
        File.WriteAllText(file, string.Join('\n', lines));
        Console.WriteLine("Modified: " + file);
    }
}
