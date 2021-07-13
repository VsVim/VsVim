#if VS_UNIT_TEST_HOST

namespace Vim.UnitTest
{
    public class MockRegisterValueBacking : IRegisterValueBacking
    {
        public RegisterValue RegisterValue { get; set; }
    }
}
#endif
