using Kibali;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;

namespace GraphPermissionsTables.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public required string HttpMethod { get; set; }

        [BindProperty]
        public required string RequestUrl { get; set; }

        public string? MarkdownTable { get; set; }

        public string? ErrorMessage { get; set; }

        public PermissionsDocument? PermissionsDocument { get; set; }

        private static readonly Regex FunctionParameterRegex = new(@"(?<=\=)[^)]+(?=\))", RegexOptions.Compiled, TimeSpan.FromSeconds(5));
        private static readonly Regex QueryOptionSegementRegex = new(@"(\$.*)", RegexOptions.Compiled, TimeSpan.FromSeconds(5));

        public async Task OnPostAsync()
        {
            // remove $ref, $count, $value segments from paths
            RequestUrl = QueryOptionSegementRegex.Replace(RequestUrl, string.Empty).TrimEnd('/').ToLowerInvariant();

            // normalize function parameters
            RequestUrl = FunctionParameterRegex.Replace(RequestUrl, "{value}");

            if (!RequestUrl.StartsWith("/"))
            {
                ErrorMessage = "The request path must start with '/'.";
                return;
            }
            try
            {
                var permissionsFileUrl = "https://raw.githubusercontent.com/microsoftgraph/microsoft-graph-devx-content/master/permissions/new/permissions.json";
                using var client = new HttpClient();
                using var stream = await client.GetStreamAsync(permissionsFileUrl);
                PermissionsDocument = PermissionsDocument.Load(stream);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load permissions document: {ex.Message}";
            }

            if (PermissionsDocument != null)
            {
                try
                {
                    var generator = new PermissionsStubGenerator(PermissionsDocument, RequestUrl, HttpMethod, false, true);
                    var mdTable = generator.GenerateTable();
                    if (string.IsNullOrWhiteSpace(mdTable))
                    {
                        ErrorMessage = "Could not find permissions for path";
                    }
                    else
                    {
                        MarkdownTable = ConvertMdToHtml(mdTable.Trim());
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Could not load fetch permissions: {ex.Message}";
                    return;
                }
            }
            else
            {
                ErrorMessage = $"Permissions document was not found";
            }
        }

        private string ConvertMdToHtml(string markdownTable)
        {
            // Split the input into lines
            string[] lines = markdownTable.Trim().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Extract headers and rows
            string[] headers = lines[0].Trim('|').Split('|');
            string[][] rows = new string[lines.Length - 2][];
            for (int i = 2; i < lines.Length; i++)
            {
                rows[i - 2] = lines[i].Trim('|').Split('|');
            }

            // Start building HTML table
            string htmlTable = "<table style=\"width: 100%; border-collapse: collapse; border: 1px solid black;\">\n";

            // Add table headers
            htmlTable += "  <thead>\n    <tr>\n";
            foreach (string header in headers)
            {
                htmlTable += $"      <th style=\"border: 1px solid black; padding: 8px; text-align: left;\">{header.Trim()}</th>\n";
            }
            htmlTable += "    </tr>\n  </thead>\n";

            // Add table rows
            htmlTable += "  <tbody>\n";
            foreach (string[] row in rows)
            {
                htmlTable += "    <tr>\n";
                foreach (string cell in row)
                {
                    htmlTable += $"      <td style=\"border: 1px solid black; padding: 8px; word-wrap: break-word; white-space: normal;\">{cell.Trim()}</td>\n";
                }
                htmlTable += "    </tr>\n";
            }
            htmlTable += "  </tbody>\n";

            // End table
            htmlTable += "</table>";

            // Print or use the resulting HTML
            return htmlTable;
        }
    }
}
