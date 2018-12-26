using System.ComponentModel.Composition;

namespace Vim.VisualStudio.Implementation.Settings
{
    [Export(typeof(ISettingsCollectionPathProvider))]
    internal sealed class VsVimConstantCollectionPathProvider : ISettingsCollectionPathProvider
    {
        internal const string CollectionPath = "VsVim";
        
        public string GetCollectionName()
        {
            return CollectionPath;
        }
    }
}