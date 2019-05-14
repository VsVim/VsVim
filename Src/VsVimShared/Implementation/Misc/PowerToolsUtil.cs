﻿using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(IExtensionAdapter))]
    internal sealed class PowerToolsUtil : VimExtensionAdapter
    {
        internal static readonly Guid QuickFindGuid = new Guid("4848f190-8e66-4af0-a898-454a568e8f65");

        private readonly bool _isQuickFindInstalled;
        private readonly Lazy<object> _searchModel;
        private readonly Lazy<PropertyInfo> _isActivePropertyInfo;

        [ImportingConstructor]
        internal PowerToolsUtil(SVsServiceProvider serviceProvider)
        {
            var vsShell = serviceProvider.GetService<SVsShell, IVsShell>();
            _isQuickFindInstalled = vsShell.IsPackageInstalled(QuickFindGuid);
            _searchModel = new Lazy<object>(GetSearchModel);
            _isActivePropertyInfo = new Lazy<PropertyInfo>(GetIsActivePropertyInfo);
        }

        // Detect PowerTools quick find as an incremental search.
        protected override bool IsIncrementalSearchActive(ITextView textView) =>
            IsQuickFindActive();

        private bool IsQuickFindActive()
        {
            if (!_isQuickFindInstalled)
            {
                return false;
            }

            var searchModel = _searchModel.Value;
            if (searchModel == null)
            {
                return false;
            }

            var isActiveInfo = _isActivePropertyInfo.Value;
            if (isActiveInfo == null)
            {
                return false;
            }

            try
            {
                return (bool)isActiveInfo.GetValue(searchModel, null);
            }
            catch
            {
                return false;
            }
        }

        private PropertyInfo GetIsActivePropertyInfo()
        {
            var searchModel = _searchModel.Value;
            if (searchModel == null)
            {
                return null;
            }

            var type = searchModel.GetType();
            return type.GetProperty("IsActive", BindingFlags.Public | BindingFlags.Instance);
        }

        private object GetSearchModel()
        {
            try
            {
                var assembly = GetQuickFindAssembly();
                if (assembly == null)
                {
                    return null;
                }

                var type = assembly.GetType("Microsoft.QuickFind.SearchModel");
                if (type == null)
                {
                    return null;
                }

                var property = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                return property.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private Assembly GetQuickFindAssembly()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(x => x.GetName().Name == "QuickFind")
                .FirstOrDefault();
        }
    }
}
