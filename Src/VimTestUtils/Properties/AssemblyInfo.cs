using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("6819ad26-901e-4261-95aa-9913d435296a")]

// https://github.com/VsVim/VsVim/issues/2905
// Remove these when fixing that issue. Everything left in VimTestUtils should be public
[assembly: InternalsVisibleTo("Vim.Core.2017.UnitTest")]
[assembly: InternalsVisibleTo("Vim.Core.2019.UnitTest")]
[assembly: InternalsVisibleTo("Vim.VisualStudio.Shared.UnitTest")]
