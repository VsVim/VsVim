using Microsoft.VisualStudio.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.VisualStudio.UnitTest
{
    public class TestableWritableSettingsStore : WritableSettingsStore
    {
        public Func<string, string, object> GetPropertyFunc { get; set; }
        public Func<string, string, object, object> GetPropertyWithDefaultFunc { get; set; }
        public Action<string, string, object> SetPropertyFunc { get; set; }
        public Func<string, string, bool> DeletePropertyFunc { get; set; }
        public Action<string> CreateCollectionFunc { get; set; }
        public Func<string, bool> DeleteCollectionFunc { get; set; }
        public Func<string, string, SettingsType> GetPropertyTypeFunc { get; set; }
        public Func<string, string, bool> PropertyExistsFunc { get; set; }
        public Func<string, bool> CollectionExistsFunc { get; set; }
        public Func<string, DateTime> GetLastWriteTimeFunc { get; set; }
        public Func<string, int> GetSubCollectionCountFunc { get; set; }
        public Func<string, int> GetPropertyCountFunc { get; set; }
        public Func<string, IEnumerable<string>> GetSubCollectionNamesFunc { get; set; }
        public Func<string, IEnumerable<string>> GetPropertyNamesFunc { get; set; }

        public TestableWritableSettingsStore()
        {
            GetPropertyFunc = delegate { throw new NotImplementedException(); };
            GetPropertyWithDefaultFunc = delegate { throw new NotImplementedException(); };
            SetPropertyFunc = delegate { throw new NotImplementedException(); };
            DeletePropertyFunc = delegate { throw new NotImplementedException(); };
            CreateCollectionFunc = delegate { throw new NotImplementedException(); };
            DeleteCollectionFunc = delegate { throw new NotImplementedException(); };
            GetPropertyTypeFunc = delegate { throw new NotImplementedException(); };
            PropertyExistsFunc = delegate { throw new NotImplementedException(); };
            CollectionExistsFunc = delegate { throw new NotImplementedException(); };
            GetLastWriteTimeFunc = delegate { throw new NotImplementedException(); };
            GetSubCollectionCountFunc = delegate { throw new NotImplementedException(); };
            GetPropertyCountFunc = delegate { throw new NotImplementedException(); };
            GetSubCollectionNamesFunc = delegate { throw new NotImplementedException(); };
            GetPropertyNamesFunc = delegate { throw new NotImplementedException(); };
        }

        public override void SetBoolean(string collectionPath, string propertyName, bool value) =>
            SetPropertyFunc(collectionPath, propertyName, value);

        public override void SetInt32(string collectionPath, string propertyName, int value) =>
            SetPropertyFunc(collectionPath, propertyName, value);

        public override void SetUInt32(string collectionPath, string propertyName, uint value) =>
            SetPropertyFunc(collectionPath, propertyName, value);

        public override void SetInt64(string collectionPath, string propertyName, long value) =>
            SetPropertyFunc(collectionPath, propertyName, value);

        public override void SetUInt64(string collectionPath, string propertyName, ulong value) =>
            SetPropertyFunc(collectionPath, propertyName, value);

        public override void SetString(string collectionPath, string propertyName, string value) =>
            SetPropertyFunc(collectionPath, propertyName, value);

        public override void SetMemoryStream(string collectionPath, string propertyName, MemoryStream value) =>
            SetPropertyFunc(collectionPath, propertyName, value);

        public override void CreateCollection(string collectionPath) =>
            CreateCollectionFunc(collectionPath);

        public override bool DeleteCollection(string collectionPath) =>
            DeleteCollectionFunc(collectionPath);

        public override bool DeleteProperty(string collectionPath, string propertyName) =>
            DeletePropertyFunc(collectionPath, propertyName);

        public override bool GetBoolean(string collectionPath, string propertyName) =>
            (bool)GetPropertyFunc(collectionPath, propertyName);

        public override bool GetBoolean(string collectionPath, string propertyName, bool defaultValue) =>
            (bool)GetPropertyWithDefaultFunc(collectionPath, propertyName, defaultValue);

        public override int GetInt32(string collectionPath, string propertyName) =>
            (int)GetPropertyFunc(collectionPath, propertyName);

        public override int GetInt32(string collectionPath, string propertyName, int defaultValue) =>
            (int)GetPropertyWithDefaultFunc(collectionPath, propertyName, defaultValue);

        public override uint GetUInt32(string collectionPath, string propertyName) =>
            (uint)GetPropertyFunc(collectionPath, propertyName);

        public override uint GetUInt32(string collectionPath, string propertyName, uint defaultValue) =>
            (uint)GetPropertyWithDefaultFunc(collectionPath, propertyName, defaultValue);

        public override long GetInt64(string collectionPath, string propertyName) =>
            (long)GetPropertyFunc(collectionPath, propertyName);

        public override long GetInt64(string collectionPath, string propertyName, long defaultValue) =>
            (long)GetPropertyWithDefaultFunc(collectionPath, propertyName, defaultValue);

        public override ulong GetUInt64(string collectionPath, string propertyName) =>
            (ulong)GetPropertyFunc(collectionPath, propertyName);

        public override ulong GetUInt64(string collectionPath, string propertyName, ulong defaultValue) =>
            (ulong)GetPropertyWithDefaultFunc(collectionPath, propertyName, defaultValue);

        public override string GetString(string collectionPath, string propertyName) =>
            (string)GetPropertyFunc(collectionPath, propertyName);

        public override string GetString(string collectionPath, string propertyName, string defaultValue) =>
            (string)GetPropertyWithDefaultFunc(collectionPath, propertyName, defaultValue);

        public override MemoryStream GetMemoryStream(string collectionPath, string propertyName) =>
            (MemoryStream)GetPropertyFunc(collectionPath, propertyName);

        public override SettingsType GetPropertyType(string collectionPath, string propertyName) =>
            GetPropertyTypeFunc(collectionPath, propertyName);

        public override bool PropertyExists(string collectionPath, string propertyName) =>
            PropertyExistsFunc(collectionPath, propertyName);

        public override bool CollectionExists(string collectionPath) =>
            CollectionExistsFunc(collectionPath);

        public override DateTime GetLastWriteTime(string collectionPath) =>
            GetLastWriteTimeFunc(collectionPath);

        public override int GetSubCollectionCount(string collectionPath) =>
            GetSubCollectionCountFunc(collectionPath);

        public override int GetPropertyCount(string collectionPath) =>
            GetPropertyCountFunc(collectionPath);

        public override IEnumerable<string> GetSubCollectionNames(string collectionPath) =>
            GetSubCollectionNamesFunc(collectionPath);

        public override IEnumerable<string> GetPropertyNames(string collectionPath) =>
            GetPropertyNamesFunc(collectionPath);
    }
}
