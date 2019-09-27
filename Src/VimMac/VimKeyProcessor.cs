using System;
using System.Collections.Generic;
using System.Text;
//using System.Windows.Input;
using AppKit;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.FSharp.Core;
using Vim.Mac;
using MonoDevelop.Core;
using MonoDevelop.Ide;

using Vim.Extensions;
namespace Vim.UI.Cocoa
{
    /// <summary>
    /// The morale of the history surrounding this type is translating key input is
    /// **hard**.  Anytime it's done manually and expected to be 100% correct it 
    /// likely to have a bug.  If you doubt this then I encourage you to read the 
    /// following 10 part blog series
    /// 
    /// http://blogs.msdn.com/b/michkap/archive/2006/04/13/575500.aspx
    ///
    /// Or simply read the keyboard feed on the same blog page.  It will humble you
    /// </summary>
    public class VimKeyProcessor : KeyProcessor
    {
        private readonly IKeyUtil _keyUtil;

        public IVimBuffer VimBuffer { get; }

        public ITextBuffer TextBuffer
        {
            get { return VimBuffer.TextBuffer; }
        }

        public ITextView TextView
        {
            get { return VimBuffer.TextView; }
        }

        public bool ModeChanged { get; private set; }

        public VimKeyProcessor(IVimBuffer vimBuffer, IKeyUtil keyUtil)
        {
            VimBuffer = vimBuffer;
            _keyUtil = keyUtil;
        }

        //public override bool IsInterestedInHandledEvents
        //{
        //	get { return true; }
        //}

        /// <summary>
        /// Try and process the given KeyInput with the IVimBuffer.  This is overridable by 
        /// derived classes in order for them to prevent any KeyInput from reaching the 
        /// IVimBuffer
        /// </summary>
        protected virtual bool TryProcess(KeyInput keyInput)
        {
            return VimBuffer.CanProcess(keyInput) && VimBuffer.Process(keyInput).IsAnyHandled;
        }


        /// <summary>
        /// Last chance at custom handling of user input.  At this point we have the 
        /// advantage that WPF has properly converted the user input into a char which 
        /// can be effeciently mapped to a KeyInput value.  
        /// </summary>
        /// 
        /// **** KeyProcessor does not contain a TextInput method on Mac ****
        /// 
        //public override void TextInput(NSEvent args)
        //{
        //	VimTrace.TraceInfo("VimKeyProcessor::TextInput Text={0} ControlText={1} SystemText={2}",
        //		StringUtil.GetDisplayString(args..Text),
        //		StringUtil.GetDisplayString(args.ControlText),
        //		StringUtil.GetDisplayString(args.SystemText));

        //	var handled = false;

        //	var text = args.Text;
        //	if (string.IsNullOrEmpty(text))
        //	{
        //		text = args.ControlText;
        //	}

        //	if (!string.IsNullOrEmpty(text))
        //	{
        //		// In the case of a failed dead key mapping (pressing the accent key twice for
        //		// example) we will recieve a multi-length string here.  One character for every
        //		// one of the mappings.  Make sure to handle each of them
        //		for (var i = 0; i < text.Length; i++)
        //		{
        //			var keyInput = KeyInputUtil.CharToKeyInput(text[i]);
        //			handled = TryProcess(keyInput);
        //		}
        //	}
        //	else if (!string.IsNullOrEmpty(args.SystemText))
        //	{
        //		// The system text needs to be processed differently than normal text.  When 'a'
        //		// is pressed with control it will come in as control text as the proper control
        //		// character.  When 'a' is pressed with Alt it will come in as simply 'a' and we
        //		// have to rely on the currently pressed key modifiers to determine the appropriate
        //		// character
        //		var keyboardDevice = args.Device as KeyboardDevice;
        //		var keyModifiers = keyboardDevice != null
        //			? _keyUtil.GetKeyModifiers(keyboardDevice.Modifiers)
        //			: VimKeyModifiers.Alt;

        //		text = args.SystemText;
        //		for (var i = 0; i < text.Length; i++)
        //		{
        //			var keyInput = KeyInputUtil.ApplyKeyModifiers(KeyInputUtil.CharToKeyInput(text[i]), keyModifiers);
        //			handled = TryProcess(keyInput);
        //		}
        //	}

        //	VimTrace.TraceInfo("VimKeyProcessor::TextInput Handled={0}", handled);
        //	args.Handled = handled;
        //	base.TextInput(args);
        //}

        /// <summary>
        /// This handler is necessary to intercept keyboard input which maps to Vim
        /// commands but doesn't map to text input.  Any combination which can be 
        /// translated into actual text input will be done so much more accurately by
        /// WPF and will end up in the TextInput event.
        /// 
        /// An example of why this handler is needed is for key combinations like 
        /// Shift+Escape.  This combination won't translate to an actual character in most
        /// (possibly all) keyboard layouts.  This means it won't ever make it to the
        /// TextInput event.  But it can translate to a Vim command or mapped keyboard 
        /// combination that we do want to handle.  Hence we override here specifically
        /// to capture those circumstances
        /// </summary>
        public override void KeyDown(NSEvent theEvent)
        {
            VimTrace.TraceInfo("VimKeyProcessor::KeyDown {0} {1}", theEvent.Characters, theEvent.CharactersIgnoringModifiers);
            //var key = (NSKey)theEvent.KeyCode;

            bool handled;
            //if (key == Key.DeadCharProcessed)
            //{
            //	// When a dead key combination is pressed we will get the key down events in 
            //	// sequence after the combination is complete.  The dead keys will come first
            //	// and be followed by the final key which produces the char.  That final key 
            //	// is marked as DeadCharProcessed.
            //	//
            //	// All of these should be ignored.  They will produce a TextInput value which
            //	// we can process in the TextInput event
            //	handled = false;
            //}
            //else
            {
                var oldMode = VimBuffer.Mode.ModeKind;
                VimTrace.TraceDebug(oldMode.ToString());
                // Attempt to map the key information into a KeyInput value which can be processed
                // by Vim.  If this works and the key is processed then the input is considered
                // to be handled
                if (_keyUtil.TryConvertSpecialToKeyInput(theEvent, out KeyInput keyInput))
                {
                    handled = TryProcess(keyInput);
                }
                else
                {
                    handled = false;
                }
                var newMode = VimBuffer.Mode.ModeKind;
                VimTrace.TraceDebug(newMode.ToString());
            }

            VimTrace.TraceInfo("VimKeyProcessor::KeyDown Handled = {0}", handled);
            if (VimBuffer.LastMessage.IsSome())
            {
                IdeApp.Workbench.StatusBar.ShowMessage(VimBuffer.LastMessage.Value);
            }
            else
            {
                IdeApp.Workbench.StatusBar.ShowReady();
            }

            var message = Mac.StatusBar.GetStatus(VimBuffer).Text;
            IdeApp.Workbench.StatusBar.ShowMessage(message);
            //if(!handled)
            //{
            //    base.KeyDown(theEvent);
            //}
        }

        public override void KeyUp(NSEvent theEvent)
        {
            var key = (NSKey)theEvent.KeyCode;
            VimTrace.TraceInfo("VimKeyProcessor::KeyUp {0}", key);
            base.KeyUp(theEvent);

        }
    }
}
