using System.Collections.Generic;

var funcList = new List<string>();
string funcInfo;

FileCodeModel fcm = DTE.ActiveDocument.ProjectItem.FileCodeModel as FileCodeModel;
if (fcm == null)
    return;

foreach (CodeElement element in fcm.CodeElements)
{
    if (element is CodeNamespace)
    {
        CodeNamespace nsp = element as CodeNamespace;

        foreach (CodeElement subElement in nsp.Children)
        {
            if (subElement is CodeClass)
            {
                CodeClass c2 = subElement as CodeClass;
                foreach (CodeElement item in c2.Children)
                {
                    if (item is CodeFunction)
                    {
                        CodeFunction cf = item as CodeFunction;
                        funcInfo = $"{cf.StartPoint.Line,5}:{cf.Name}";
                        funcList.Add(funcInfo);
                    }
                }
            }
        }
    }
}
if (funcList.Count == 0)
    return;

DisplayStatusLong(funcList.ToArray());

