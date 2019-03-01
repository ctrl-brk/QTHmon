using Microsoft.Extensions.Configuration;

namespace QTHmon
{
    public static class Extensions
    {
        /// <summary>Shorthand for GetSection("AppSettings")[name].</summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="name">The connection string key.</param>
        /// <returns></returns>
        public static string GetAppSetting(this IConfiguration configuration, string name)
        {
            var section = configuration?.GetSection("AppSettings");

            return section?[name];
        }
    }
}
