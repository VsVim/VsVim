using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Collections;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class RegisterValueTest
    {
        public sealed class KeyInputTest : RegisterValueTest
        {
            /// <summary>
            /// KeyInput based RegisterValue instances are all CharacterWise.  No known way to create a 
            /// LineWise one
            /// </summary>
            [Fact]
            public void CharacterWiseOperationKind()
            {
                var value = new RegisterValue(FSharpList<KeyInput>.Empty);
                Assert.Equal(OperationKind.CharacterWise, value.OperationKind);
            }
        }

        public sealed class StringBlockTest : RegisterValueTest
        {
            /// <summary>
            /// When creating a block version there should not be a new line after the last element.  This isn't
            /// specified anywhere in the Vim documentation but is visible if you look at register values
            /// </summary>
            [Fact]
            public void NoNewLineAtEnd()
            {
                var stringData = VimUtil.CreateStringDataBlock("cat", "dog");
                var value = new RegisterValue(stringData, OperationKind.CharacterWise);
                Assert.Equal("cat" + Environment.NewLine + "dog", value.StringValue);
            }
        }
    }
}
