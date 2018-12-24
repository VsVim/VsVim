using System.Collections.Generic;

using Moq;

using Vim.VisualStudio.Implementation.Settings;

using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public class VimCachingSettingsDecoratorTest
    {
        private readonly Mock<ISpecializedCacheProvider> _cacheProviderMock;
        private readonly Mock<IPhysicalSettingsStore> _underlyingStoreMock;

        private readonly string _key;

        private string _defaultValue;
        private string _sampleValue;

        private string _discard;

        private readonly ISettingsStore _cachedSettingsStore;

        public VimCachingSettingsDecoratorTest()
        {
            _key = "2231EF8E-858C-49DE-99EE-90BF43053DB1";

            _defaultValue = "6B212D36-3F8D-4908-B692-2E78DB22D96B";
            _sampleValue = "4F36AC2D-821D-44FE-AB8E-1A8A3B25CC11";

            var mockRepository = new MockRepository(MockBehavior.Strict);

            _cacheProviderMock = mockRepository.Create<ISpecializedCacheProvider>();
            _underlyingStoreMock = mockRepository.Create<IPhysicalSettingsStore>();

            _cachedSettingsStore = new CachedSettingsStore(
                _cacheProviderMock.Object,
                _underlyingStoreMock.Object);
        }

        [Fact]
        public void CacheFetchesDataFromUnderlyingStoreOnFirstAccess()
        {
            var dictionary = new Dictionary<string, string>();

            _cacheProviderMock.Setup(x => x.Get<string>()).Returns(dictionary);
            _underlyingStoreMock.Setup(x => x.Check(_key, out _sampleValue, _defaultValue)).Returns(true);

            var foundInStore = _cachedSettingsStore.Check(_key, out string actualValue, _defaultValue);

            _cacheProviderMock.Verify(x => x.Get<string>(), Times.AtLeastOnce);
            _underlyingStoreMock.Verify(x => x.Check(_key, out _discard, _defaultValue), Times.Once);

            Assert.True(foundInStore);
            Assert.Equal(_sampleValue, actualValue);
        }

        [Fact]
        public void CacheDoesNotFetchDataFromTheUnderlyingStoreAfterInitialUpdate()
        {
            var dictionary = new Dictionary<string, string>();

            _cacheProviderMock.Setup(x => x.Get<string>()).Returns(dictionary);
            _underlyingStoreMock.Setup(x => x.Check(_key, out _sampleValue, _defaultValue)).Returns(true);

            var firstFoundInStore = _cachedSettingsStore.Check(_key, out string firstActualValue, _defaultValue);
            var secondFoundInStore = _cachedSettingsStore.Check(_key, out string secondActualValue, _defaultValue);

            _cacheProviderMock.Verify(x => x.Get<string>(), Times.AtLeastOnce);
            _underlyingStoreMock.Verify(x => x.Check(_key, out _discard, _defaultValue), Times.Once);

            Assert.True(firstFoundInStore);
            Assert.True(secondFoundInStore);
            Assert.Equal(_sampleValue, firstActualValue);
            Assert.Equal(_sampleValue, secondActualValue);
        }


        [Fact]
        public void CacheDoesNotSaveDataToTheUnderlyingStoreAfterEqualAssignment()
        {
            var dictionary = new Dictionary<string, string>();

            _cacheProviderMock.Setup(x => x.Get<string>()).Returns(dictionary);
            _underlyingStoreMock.Setup(x => x.Check(_key, out _sampleValue, _defaultValue)).Returns(true);

            var firstFoundInStore = _cachedSettingsStore.Check(_key, out string firstActualValue, _defaultValue);

            _cachedSettingsStore.Set(_key, _sampleValue);

            var secondFoundInStore = _cachedSettingsStore.Check(_key, out string secondActualValue, _defaultValue);

            _cacheProviderMock.Verify(x => x.Get<string>(), Times.AtLeastOnce);
            _underlyingStoreMock.Verify(x => x.Check(_key, out _discard, _defaultValue), Times.Once);
            _underlyingStoreMock.Verify(x => x.Set(_key, _sampleValue), Times.Never);

            Assert.True(firstFoundInStore);
            Assert.True(secondFoundInStore);
            Assert.Equal(_sampleValue, firstActualValue);
            Assert.Equal(_sampleValue, secondActualValue);
        }

        [Fact]
        public void CacheSavesDataToTheUnderlyingStoreAfterNonEqualAssignment()
        {
            var anotherSampleValue = "D598C2D7-7561-4F68-8277-B2C4D2C8E8A0";

            var dictionary = new Dictionary<string, string>();

            _cacheProviderMock.Setup(x => x.Get<string>()).Returns(dictionary);
            _underlyingStoreMock.Setup(x => x.Check(_key, out _sampleValue, _defaultValue)).Returns(true);
            _underlyingStoreMock.Setup(x => x.Set(_key, anotherSampleValue));

            var firstFoundInStore = _cachedSettingsStore.Check(_key, out string firstActualValue, _defaultValue);

            _cachedSettingsStore.Set(_key, anotherSampleValue);

            var secondFoundInStore = _cachedSettingsStore.Check(_key, out string secondActualValue, _defaultValue);

            _cacheProviderMock.Verify(x => x.Get<string>(), Times.AtLeastOnce);
            _underlyingStoreMock.Verify(x => x.Check(_key, out _discard, _defaultValue), Times.Once);
            _underlyingStoreMock.Verify(x => x.Set(_key, anotherSampleValue), Times.Once);

            Assert.True(firstFoundInStore);
            Assert.True(secondFoundInStore);
            Assert.Equal(_sampleValue, firstActualValue);
            Assert.Equal(anotherSampleValue, secondActualValue);
        }

        [Fact]
        public void CacheReturnsDefaultValueWhenTheKeyIsNotFoundInUnderlyingStore()
        {
            var dictionary = new Dictionary<string, string>();

            _cacheProviderMock.Setup(x => x.Get<string>()).Returns(dictionary);
            _underlyingStoreMock.Setup(x => x.Check(_key, out _defaultValue, _defaultValue)).Returns(false);

            var foundInStore = _cachedSettingsStore.Check(_key, out string actualValue, _defaultValue);

            _cacheProviderMock.Verify(x => x.Get<string>(), Times.AtLeastOnce);
            _underlyingStoreMock.Verify(x => x.Check(_key, out _discard, _defaultValue), Times.Once);

            Assert.False(foundInStore);
            Assert.Equal(_defaultValue, actualValue);
        }

        [Fact]
        public void CacheDoesNotQueryUnderlyingStoreIfTheObjectWasNotFoundBeforeAndWasNotChanged()
        {
            var dictionary = new Dictionary<string, string>();

            _cacheProviderMock.Setup(x => x.Get<string>()).Returns(dictionary);
            _underlyingStoreMock.Setup(x => x.Check(_key, out _defaultValue, _defaultValue)).Returns(false);

            var firstFoundInStore = _cachedSettingsStore.Check(_key, out string firstActualValue, _defaultValue);
            var secondFoundInStore = _cachedSettingsStore.Check(_key, out string secondActualValue, _defaultValue);

            _cacheProviderMock.Verify(x => x.Get<string>(), Times.AtLeastOnce);
            _underlyingStoreMock.Verify(x => x.Check(_key, out _discard, _defaultValue), Times.Once);

            Assert.False(firstFoundInStore);
            Assert.False(secondFoundInStore);
            Assert.Equal(_defaultValue, firstActualValue);
            Assert.Equal(_defaultValue, secondActualValue);
        }

        [Fact]
        public void CacheUsesRightDefaultWithoutQueryingUnderlyingStoreIfTheObjectWasNotFoundBeforeAndWasNotChanged()
        {
            var dictionary = new Dictionary<string, string>();

            _cacheProviderMock.Setup(x => x.Get<string>()).Returns(dictionary);
            _underlyingStoreMock.Setup(x => x.Check(_key, out _defaultValue, _defaultValue)).Returns(false);

            var firstFoundInStore = _cachedSettingsStore.Check(_key, out string firstActualValue, _defaultValue);
            var secondFoundInStore = _cachedSettingsStore.Check(_key, out string secondActualValue, _sampleValue);

            _cacheProviderMock.Verify(x => x.Get<string>(), Times.AtLeastOnce);
            _underlyingStoreMock.Verify(x => x.Check(_key, out _discard, _defaultValue), Times.Once);

            Assert.False(firstFoundInStore);
            Assert.False(secondFoundInStore);
            Assert.Equal(_defaultValue, firstActualValue);
            Assert.Equal(_sampleValue, secondActualValue);
        }
    }
}