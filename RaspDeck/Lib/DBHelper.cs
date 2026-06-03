using Hanssens.Net;
using System;

namespace DroidDeck.Lib
{
    public class DBHelper
    {
        private static LocalStorageConfiguration _config = new LocalStorageConfiguration();
        private static LocalStorage _storage = new LocalStorage(_config);
        public DBHelper()
        { }

        public static bool store(string key, object val)
        {
            _storage.Store(key, val);
            _storage.Persist();
            return val.Equals(_storage.Get<string>(key));
        }

        public static object? Retrieve(string key)
        {
            try
            {
                return _storage.Get<object>(key);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
