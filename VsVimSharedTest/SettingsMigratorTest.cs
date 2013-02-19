using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EditorUtils;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using VsVim.Implementation.Settings;
using VsVim.UnitTest.Mock;
using Xunit;

namespace VsVim.Shared.UnitTest
{
    public abstract class SettingsMigratorTest 
    {
        protected static readonly ReadOnlyCollection<CommandBindingSetting> EmptyBindings = new ReadOnlyCollection<CommandBindingSetting>(new List<CommandBindingSetting>());

        protected readonly MockRepository _factory;
        protected readonly Mock<IVimApplicationSettings> _vimApplicationSettings;
        protected readonly Mock<ILegacySettings> _legacySettings;
        protected readonly Mock<IProtectedOperations> _protectedOperations;
        internal readonly SettingsMigrator _settingsMigrator;

        protected SettingsMigratorTest(MockRepository factory = null, SVsServiceProvider serviceProvider = null)
        {
            _factory = factory ?? new MockRepository(MockBehavior.Strict);
            _vimApplicationSettings = _factory.Create<IVimApplicationSettings>(MockBehavior.Loose);
            _legacySettings = _factory.Create<ILegacySettings>(MockBehavior.Loose);
            _legacySettings.SetupGet(x => x.RemovedBindings).Returns(EmptyBindings);
            _protectedOperations = _factory.Create<IProtectedOperations>();
            if (serviceProvider == null)
            {
                var dte = MockObjectFactory.CreateDteWithCommands();
                serviceProvider = MockObjectFactory.CreateVsServiceProvider(Tuple.Create<Type, object>(typeof(SDTE), dte.Object)).Object;
            }

            _settingsMigrator = new SettingsMigrator(
                serviceProvider,
                _vimApplicationSettings.Object,
                _legacySettings.Object,
                _protectedOperations.Object);
        }

        public sealed class NeedsMigrationTest : SettingsMigratorTest
        {
            public NeedsMigrationTest()
            {
            }

            [Fact]
            public void AlreadyMigrated()
            {
                _vimApplicationSettings.SetupGet(x => x.LegacySettingsMigrated).Returns(true);
                Assert.False(_settingsMigrator.NeedsMigration);
            }

            [Fact]
            public void NoLegacySettingsUsed()
            {
                _legacySettings.SetupGet(x => x.RemovedBindings).Returns(EmptyBindings);
                _vimApplicationSettings.SetupGet(x => x.LegacySettingsMigrated).Returns(false);
                Assert.False(_settingsMigrator.LegacySettingsUsed);
                Assert.False(_settingsMigrator.NeedsMigration);
            }
        }

        public sealed class LegacySettingsUsed : SettingsMigratorTest
        {
            [Fact]
            public void Default()
            {
                Assert.False(_settingsMigrator.LegacySettingsUsed);
            }

            [Fact]
            public void HaveUpdatedKeyBindings()
            {
                _legacySettings.SetupGet(x => x.HaveUpdatedKeyBindings).Returns(true);
                Assert.True(_settingsMigrator.LegacySettingsUsed);
            }

            [Fact]
            public void IgnoredConflictingKeyBindings()
            {
                _legacySettings.SetupGet(x => x.IgnoredConflictingKeyBinding).Returns(true);
                Assert.True(_settingsMigrator.LegacySettingsUsed);
            }

            [Fact]
            public void RemovedBindings()
            {
                var binding = new CommandBindingSetting();
                var list = new List<CommandBindingSetting>(new[] { binding });
                _legacySettings.SetupGet(x => x.RemovedBindings).Returns(list.AsReadOnly());
                Assert.True(_settingsMigrator.LegacySettingsUsed);
            }
        }
    }
}
