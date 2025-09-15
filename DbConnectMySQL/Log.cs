using System;
using System.IO;

namespace DbConnectMySQL
{
    public static class Log
    {

        #region Properties

        private static string _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static string _fileName = "_Log.log";
        private static int _saveDays = 30;

        #endregion

        #region Settings

        public static string FilePath
        {
            get => _filePath;
            set => _filePath = string.IsNullOrEmpty(value)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs")
                : value;
        }

        public static string FileName
        {
            get => _fileName;
            set => _fileName = string.IsNullOrEmpty(value)
                ? "_Log.log"
                : $"_{value}.log";
        }

        public static int SaveDays
        {
            get => _saveDays;
            set => _saveDays = value < 1 ? 30 : value;
        }

        public enum MessageType
        {
            ERROR = 1,
            DEBUG,
            WARNING,
            INFO
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Метод длч записи сообщение в лог файл с выбором категории сообщения
        /// </summary>
        /// <param name="type">Тип сообщения</param>
        /// <param name="message">Текст сообщения</param>
        public static void WriteMessage(MessageType type, string message)
        {
            EnsureLogDirectoryExists();
            string logFile = Path.Combine(_filePath, $"{DateTime.Now:yyyy-MM-dd}{_fileName}");

            string typeMessage = GetTypeMessage(type);
            string logEntry = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} >>> {typeMessage}: {message}";

            try
            {
                File.AppendAllText(logFile, logEntry + Environment.NewLine);
                CleanOldLogs();
            }
            catch (Exception ex)
            {
                // Fallback logging if primary logging fails
                Console.WriteLine($"Failed to write to log: {ex.Message}");
            }
        }

        /// <summary>
        /// Запись сообщения об исключении (ошибки) в приложении 
        /// </summary>
        /// <param name="ex">Объект ошибки</param>
        public static void WriteException(Exception ex)
        {
            string message = $"{ex.Message}\n{ex.StackTrace}";
            WriteMessage(MessageType.ERROR, message);
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Метод очистки старых записей
        /// </summary>
        private static void CleanOldLogs()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-_saveDays);
                var logFiles = Directory.GetFiles(_filePath, $"*{_fileName}");

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        fileInfo.Delete();
                    }
                }
            }
            catch
            {
                // Suppress cleanup errors
            }
        }

        /// <summary>
        /// Метод преобразования типа категории в текст
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static string GetTypeMessage(MessageType type)
        {
            return type switch
            {
                MessageType.ERROR => "[ERROR] ",
                MessageType.DEBUG => "[DEBUG] ",
                MessageType.WARNING => "[WARNING] ",
                _ => "[INFO] "
            };
        }
        
        /// <summary>
        /// Метод создания папки для хранения лог файлов
        /// </summary>
        private static void EnsureLogDirectoryExists()
        {
            if (!Directory.Exists(_filePath))
            {
                Directory.CreateDirectory(_filePath);
            }
        }

        #endregion

    }
}
