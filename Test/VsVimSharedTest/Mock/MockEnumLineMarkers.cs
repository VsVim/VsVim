using System.Collections.Generic;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Vim.VisualStudio.UnitTest.Mock
{
    public class MockEnumLineMarkers : IVsEnumLineMarkers
    {
        private readonly List<IVsTextLineMarker> _markers;
        private int _index;

        public MockEnumLineMarkers(List<IVsTextLineMarker> markers)
        {
            _markers = markers;
        }

        public int GetCount(out int count)
        {
            count = _markers.Count;
            return VSConstants.S_OK;
        }

        public int Next(out IVsTextLineMarker marker)
        {
            if (_index >= _markers.Count)
            {
                marker = null;
                return VSConstants.S_FALSE;
            }
            else
            {
                marker = _markers[_index];
                _index++;
                return VSConstants.S_OK;
            }
        }

        public int Reset()
        {
            _index = 0;
            return VSConstants.S_OK;
        }
    }
}
