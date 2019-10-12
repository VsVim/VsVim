using AppKit;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Cocoa
{
	/// <summary>
	/// Key utility for intrepretting Cocoa keyboard information
	/// </summary>
	public interface IKeyUtil
	{
		/// <summary>
		/// Is this the AltGr key combination.  This is not directly representable in WPF
		/// logic but the best that can be done is to check for Alt + Control
		/// </summary>
		bool IsAltGr(NSEventModifierMask modifierKeys);

		/// <summary>
		/// Convert the given ModifierKeys into the corresponding KeyModifiers (Cocoa -> Vim)
		/// </summary>
		VimKeyModifiers GetKeyModifiers(NSEventModifierMask modifierKeys);

		/// <summary>
		/// This method handles the cases where a Vim key is mapped directly by virtual key 
		/// and not by a literal character.  Keys like Up, Down, Subtract, etc ... aren't done
		/// by character but instead directly by virtual key.  They are handled by this method
		/// </summary>
		bool TryConvertSpecialToKeyInput(NSEvent theEvent, out KeyInput keyInput);
	}
}
