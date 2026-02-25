using System.Text.Json;

namespace SIS.Extensions
{
    public static class SessionExtensions
    {
        /// <summary>
        /// Set a complex object in session
        /// </summary>
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        /// <summary>
        /// Get a complex object from session
        /// </summary>
        public static T GetObject<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonSerializer.Deserialize<T>(value);
        }

        /// <summary>
        /// Check if a key exists in session
        /// </summary>
        public static bool HasKey(this ISession session, string key)
        {
            return session.GetString(key) != null;
        }
    }
}