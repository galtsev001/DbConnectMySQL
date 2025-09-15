using Microsoft.Extensions.Configuration;

namespace DbConnectMySQL
{
    public class AppSettings
    {
        private string pathSettings;
        const string DEFAULT_PATH = "./appsettings.json";

        public AppSettings()
        {
            this.pathSettings = DEFAULT_PATH;
        }

        public AppSettings(string pathSettings)
        {
            this.pathSettings = pathSettings;
        }

        /// <summary>
        /// Метод для получения данных из файла конфигурации
        /// </summary>
        /// <param name="key">ключ переменной</param>
        /// <returns></returns>
        public string? GetData(string key)
        {
            IConfiguration config = new ConfigurationBuilder()
           .AddJsonFile(this.pathSettings)
           .Build();
            return config.GetSection(key).Value;
        }
    }

}
