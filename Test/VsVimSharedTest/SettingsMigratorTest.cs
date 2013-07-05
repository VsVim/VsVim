using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EditorUtils;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using VsVim.Implementation.Settings;
using VsVim.UnitTest.Mock;
using Xunit;

namespace VsVim.UnitTest
{
    public abstract class SettingsMigratorTest 
    {
        protected static readonly ReadOnlyCollection<CommandBindingSetting> EmptyBindings = new ReadOnlyCollection<CommandBindingSetting>(new List<CommandBindingSetting>());

        protected readonly MockRepository _factory;
        protected readonly Mock<IVimApplicationSettings> _vimApplicationSettings;
        protected readonly Mock<ILegacySettings> _legacySettings;
        internal readonly SettingsMigrator _settingsMigrator;

        protected SettingsMigratorTest(MockRepository factory = null)
        {
            _factory = factory ?? new MockRepository(MockBehavior.Strict);
            _vimApplicationSettings = _factory.Create<IVimApplicationSettings>(MockBehavior.Loose);
            _vimApplicationSettings.SetupAllProperties();
            _legacySettings = _factory.Create<ILegacySettings>(MockBehavior.Loose);
            _legacySettings.SetupGet(x => x.RemovedBindings).Returns(EmptyBindings);
            var dte = MockObjectFactory.CreateDteWithCommands();

            _settingsMigrator = new SettingsMigrator(
                dte.Object,
                _vimApplicationSettings.Object,
                _legacySettings.Object);
        }

        public sealed class DoMigrationTest : SettingsMigratorTest
        {
            [Fact]
            public void CompleteMigration()
            {
                var list = new ReadOnlyCollection<CommandKeyBinding>(new CommandKeyBinding[] { });
                _legacySettings.SetupGet(x => x.HaveUpdatedKeyBindings).Returns(true);
                _legacySettings.SetupGet(x => x.IgnoredConflictingKeyBinding).Returns(true);
                _settingsMigrator.DoMigration(list);
                Assert.True(_vimApplicationSettings.Object.HaveUpdatedKeyBindings);
                Assert.True(_vimApplicationSettings.Object.IgnoredConflictingKeyBinding);
            }
        }
    }
}
