using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UnitTest;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class CommandIdTest
    {
        public sealed class EqualityTest : CommandIdTest
        {
            [Fact]
            public void Ids()
            {
                var guid = Guid.NewGuid();
                var unit = EqualityUnit
                    .Create(new CommandId(guid, 42))
                    .WithEqualValues(new CommandId(guid, 42))
                    .WithNotEqualValues(new CommandId(guid, 13));
                EqualityUtil.RunAll(
                    (x, y) => x == y,
                    (x, y) => x != y,
                    unit);
            }

            [Fact]
            public void Guids()
            {
                var guid = Guid.NewGuid();
                var unit = EqualityUnit
                    .Create(new CommandId(guid, 42))
                    .WithEqualValues(new CommandId(guid, 42))
                    .WithNotEqualValues(new CommandId(Guid.NewGuid(), 42));
                EqualityUtil.RunAll(
                    (x, y) => x == y,
                    (x, y) => x != y,
                    unit);
            }
        }
    }
}
