import os
import re

pages_dir = r"C:\MoMaherB\LegalERP.Web\LegalERP.Web\Components\Pages"
api_call_regex = re.compile(r'(await\s+\w+Api\.(?:Delete|Update|Create|Add|Upload|Remove)\w*Async\([^)]*\);)')

for root, _, files in os.walk(pages_dir):
    for filename in files:
        if filename.endswith(".razor"):
            filepath = os.path.join(root, filename)
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()

            if not api_call_regex.search(content):
                continue

            modified = False
            
            # Inject ToastService if not present
            if "@inject LegalERP.Web.Services.Toast.IToastService ToastService" not in content:
                content = content.replace("@inject NavigationManager NavManager", "@inject NavigationManager NavManager\n@inject LegalERP.Web.Services.Toast.IToastService ToastService")
                if "ToastService" not in content:
                    content = re.sub(r'(@inject\s+[^\n]+)\n', r'\1\n@inject LegalERP.Web.Services.Toast.IToastService ToastService\n', content, count=1)
                modified = True

            lines = content.split('\n')
            for i in range(len(lines)):
                if api_call_regex.search(lines[i]):
                    if "ToastService.ShowSuccess" not in lines[i] and (i + 1 >= len(lines) or "ToastService.ShowSuccess" not in lines[i+1]):
                        indent = re.match(r'^\s*', lines[i]).group(0)
                        lines[i] = lines[i] + f'\n{indent}ToastService.ShowSuccess(Loc["OperationSuccess"].Value);'
                        modified = True

            if modified:
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write('\n'.join(lines))
                print(f"Modified: {filepath}")
