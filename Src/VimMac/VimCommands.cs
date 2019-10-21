using System.Linq;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;

namespace Vim.Mac
{
    internal class ShowPadByIdHandler : CommandHandler
    {
        protected override void Run(object dataItem)
        {
            IdeApp.Workbench.Pads.FirstOrDefault(pad => pad.Id == (string)dataItem)?.BringToFront(true);
        }
    }

    internal class ShowPadByTitleHandler : CommandHandler
    {
        protected override void Run(object dataItem)
        {
            IdeApp.Workbench.Pads.FirstOrDefault(pad => pad.Title == (string)dataItem)?.BringToFront(true);
        }
    }
}
