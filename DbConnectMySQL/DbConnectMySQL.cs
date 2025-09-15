using MySql.Data.MySqlClient;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text;

namespace DbConnectMySQL
{
    /// <summary>
    /// Класс для работы с базой данных MySQL, реализующий базовые CRUD-операции.
    /// Автоматически маппирует данные между объектами C# и таблицами БД.
    /// </summary>
    public class DbConnectMySQL : IDisposable
    {
        #region Properties and Fields

        private string _host;
        private string _user;
        private string _password;
        private string _database;
        private int _port = 3306;
        private MySqlConnection _connection;
        protected List<string> _listKeys;
        public string Where { get; set; } = "";
        private bool _disposed = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Конструктор по умолчанию. Загружает настройки из стандартного файла конфигурации.
        /// </summary>
        /// <exception cref="ArgumentNullException">Выбрасывается, если какие-либо обязательные настройки отсутствуют</exception>
        public DbConnectMySQL()
        {
            var settings = new AppSettings();
            Initialize(
                settings.GetData("host") ?? throw new ArgumentNullException("host"),
                settings.GetData("user") ?? throw new ArgumentNullException("user"),
                settings.GetData("password") ?? throw new ArgumentNullException("password"),
                settings.GetData("database") ?? throw new ArgumentNullException("database"),
                settings.GetData("port") is string portString && int.TryParse(portString, out var port) ? port : _port
            );
        }

        /// <summary>
        /// Конструктор с указанием пути к файлу настроек.
        /// </summary>
        /// <param name="pathSettings">Путь к файлу с настройками подключения</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если какие-либо обязательные настройки отсутствуют</exception>
        public DbConnectMySQL(string pathSettings)
        {
            var settings = new AppSettings(pathSettings);
            Initialize(
                settings.GetData("host") ?? throw new ArgumentNullException("host"),
                settings.GetData("user") ?? throw new ArgumentNullException("user"),
                settings.GetData("password") ?? throw new ArgumentNullException("password"),
                settings.GetData("database") ?? throw new ArgumentNullException("database"),
                settings.GetData("port") is string portString && int.TryParse(portString, out var port) ? port : _port
            );
        }

        /// <summary>
        /// Конструктор с прямым указанием параметров подключения.
        /// </summary>
        /// <param name="host">Хост или IP-адрес сервера MySQL</param>
        /// <param name="port">Порт сервера MySQL</param>
        /// <param name="user">Имя пользователя БД</param>
        /// <param name="password">Пароль пользователя БД</param>
        /// <param name="database">Имя базы данных</param>
        /// <exception cref="ArgumentNullException">Выбрасывается, если какие-либо параметры равны null</exception>
        public DbConnectMySQL(string host, int port, string user, string password, string database)
        {
            Initialize(
                host ?? throw new ArgumentNullException(nameof(host)),
                user ?? throw new ArgumentNullException(nameof(user)),
                password ?? throw new ArgumentNullException(nameof(password)),
                database ?? throw new ArgumentNullException(nameof(database)),
                port > 0 ? port : _port
            );
        }

        /// <summary>
        /// Инициализирует параметры подключения и устанавливает соединение с БД.
        /// </summary>
        /// <param name="host">Хост сервера БД</param>
        /// <param name="user">Имя пользователя</param>
        /// <param name="password">Пароль</param>
        /// <param name="database">Имя базы данных</param>
        /// <param name="port">Порт подключения</param>
        private void Initialize(string host, string user, string password, string database, int port)
        {
            _host = host;
            _user = user;
            _password = password;
            _database = database;
            _port = port;

            GetConnection();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Выполняет SELECT-запрос и возвращает результат в виде списка объектов.
        /// </summary>
        /// <typeparam name="T">Тип объекта для маппинга данных</typeparam>
        /// <param name="sql">Необязательный SQL-запрос. Если null, формируется автоматически</param>
        /// <returns>Список объектов типа T, заполненных данными из БД</returns>
        public List<T> Sel_Data<T>(string sql = null) where T : new()
        {
            var list = new List<T>();

            try
            {
                var listFields = GetFieldsObject<T>();
                var finalSql = BuildSelectSql<T>(sql);

                using var cmd = new MySqlCommand(finalSql, _connection);
                _connection.Open();

                using var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        var obj = new T();
                        MapReaderToObject(reader, obj, listFields);
                        list.Add(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteException(ex);
                throw; // Лучше пробросить исключение, чтобы вызывающий код знал о проблеме
            }
            finally
            {
                Where = "";
                _connection.Close();
            }

            return list;
        }

        /// <summary>
        /// Обновляет записи в БД на основе переданных объектов.
        /// </summary>
        /// <typeparam name="T">Тип объекта, соответствующий таблице БД</typeparam>
        /// <param name="data">Список объектов для обновления</param>
        /// <returns>True если обновление прошло успешно, иначе False</returns>
        public bool Update_Data<T>(List<T> data) where T : new()
        {
            if (data == null || data.Count == 0)
                return true;

            try
            {
                _listKeys = GetPrimaryKeys<T>();
                _connection.Open();

                using var transaction = _connection.BeginTransaction();

                try
                {
                    foreach (var dto in data)
                    {
                        var sql = GetUpdateByObj(dto);
                        ExecuteNonSQLInternal(sql);
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.WriteException(ex);
                return false;
            }
            finally
            {
                _connection.Close();
            }
        }

        /// <summary>
        /// Добавляет новые записи в БД на основе переданных объектов.
        /// </summary>
        /// <typeparam name="T">Тип объекта, соответствующий таблице БД</typeparam>
        /// <param name="data">Список объектов для добавления</param>
        /// <returns>True если добавление прошло успешно, иначе False</returns>
        public bool Ins_Data<T>(List<T> data) where T : new()
        {
            if (data == null || data.Count == 0)
                return true;

            try
            {
                _listKeys = GetPrimaryKeys<T>();
                _connection.Open();

                using var transaction = _connection.BeginTransaction();

                try
                {
                    foreach (var dto in data)
                    {
                        var (sql, parameters) = GetInsertSQLByObj(dto); // Теперь метод возвращает и SQL, и параметры

                        using var cmd = new MySqlCommand(sql, _connection, transaction);
                        cmd.Parameters.AddRange(parameters);
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.WriteException(ex);
                return false;
            }
            finally
            {
                _connection.Close();
            }
        }

        /// <summary>
        /// Формирует SQL-запрос INSERT и параметры для добавления объекта в БД.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <param name="dto">Объект для добавления</param>
        /// <returns>Кортеж с SQL-запросом и массивом параметров</returns>
        private (string sql, MySqlParameter[] parameters) GetInsertSQLByObj<T>(T dto)
        {
            var sb = new StringBuilder();
            var listFields = GetFieldsObject<T>();
            var parameters = new List<MySqlParameter>();

            sb.Append($"INSERT INTO {GetNameTable<T>()} (");
            sb.Append(string.Join(", ", listFields.Select(f => f.Name)));
            sb.Append(") VALUES (");
            sb.Append(string.Join(", ", listFields.Select(f => $"@{f.Name}")));
            sb.Append(")");

            // Добавляем параметры
            foreach (var field in listFields)
            {
                var value = field.GetValue(dto);
                parameters.Add(new MySqlParameter($"@{field.Name}", value ?? DBNull.Value));
            }

            return (sb.ToString(), parameters.ToArray());
        }

        /// <summary>
        /// Удаляет записи из БД на основе переданных объектов.
        /// </summary>
        /// <typeparam name="T">Тип объекта, соответствующий таблице БД</typeparam>
        /// <param name="data">Список объектов для удаления</param>
        /// <returns>True если удаление прошло успешно, иначе False</returns>
        public bool Del_Data<T>(List<T> data) where T : new()
        {
            if (data == null || data.Count == 0)
                return true;

            try
            {
                _listKeys = GetPrimaryKeys<T>();
                _connection.Open();

                using var transaction = _connection.BeginTransaction();

                try
                {
                    foreach (var dto in data)
                    {
                        var sql = GetDeleteSQLByObj(dto);
                        ExecuteNonSQLInternal(sql);
                    }

                    transaction.Commit();
                    return true;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.WriteException(ex);
                return false;
            }
            finally
            {
                _connection.Close();
            }
        }

        /// <summary>
        /// Выполняет произвольный SQL-запрос без возвращения результата (INSERT, UPDATE, DELETE, etc).
        /// </summary>
        /// <param name="sql">SQL-запрос для выполнения</param>
        /// <exception cref="ArgumentException">Выбрасывается если SQL-запрос пустой</exception>
        public void ExecuteNonSQL(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("SQL statement cannot be empty", nameof(sql));

            try
            {
                _connection.Open();
                ExecuteNonSQLInternal(sql);
            }
            catch (Exception ex)
            {
                Log.WriteException(ex);
                throw;
            }
            finally
            {
                _connection.Close();
            }
        }

        /// <summary>
        /// Удаляет все записи из таблицы. Может быть использовано с условием Where.
        /// </summary>
        /// <typeparam name="T">Тип объекта, соответствующий таблице БД</typeparam>
        /// <returns>True если операция прошла успешно, иначе False</returns>
        public bool Del_All<T>()
        {
            try
            {
                var sql = BuildDeleteAllSql<T>();

                _connection.Open();
                using var cmd = new MySqlCommand(sql, _connection);
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteException(ex);
                return false;
            }
            finally
            {
                _connection.Close();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Создает и настраивает объект соединения с БД на основе параметров.
        /// </summary>
        private void GetConnection()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = _host,
                UserID = _user,
                Password = _password,
                Database = _database,
                Port = (uint)_port,
                PersistSecurityInfo = true,
                CharacterSet = "utf8",
                SslMode = MySqlSslMode.Preferred // Добавлена безопасность соединения
            };

            _connection = new MySqlConnection(builder.ConnectionString);
        }

        /// <summary>
        /// Возвращает список всех публичных свойств объекта.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <returns>Список свойств объекта</returns>
        private List<PropertyInfo> GetFieldsObject<T>()
        {
            return typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public).ToList();
        }

        /// <summary>
        /// Формирует SQL-запрос SELECT. Если запрос предоставлен, использует его, иначе формирует автоматически.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <param name="sql">Пользовательский SQL-запрос</param>
        /// <returns>Готовый SQL-запрос</returns>
        private string BuildSelectSql<T>(string sql)
        {
            if (!string.IsNullOrWhiteSpace(sql))
                return sql;

            var tableName = GetNameTable<T>();
            var whereClause = string.IsNullOrWhiteSpace(Where) ? "" : " " + Where;

            return $"SELECT * FROM {tableName}{whereClause}";
        }

        /// <summary>
        /// Формирует SQL-запрос DELETE для удаления всех записей из таблицы.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <returns>SQL-запрос DELETE</returns>
        private string BuildDeleteAllSql<T>()
        {
            var tableName = GetNameTable<T>();
            var whereClause = string.IsNullOrWhiteSpace(Where) ? "" : " " + Where;

            return $"DELETE FROM {tableName}{whereClause}";
        }

        /// <summary>
        /// Маппит данные из DataReader в объект.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <param name="reader">DataReader с данными из БД</param>
        /// <param name="obj">Объект для заполнения</param>
        /// <param name="properties">Список свойств объекта</param>
        private void MapReaderToObject<T>(DbDataReader reader, T obj, List<PropertyInfo> properties)
        {
            foreach (var prop in properties)
            {
                if (!prop.CanWrite)
                    continue;

                try
                {
                    var ordinal = reader.GetOrdinal(prop.Name);
                    if (reader.IsDBNull(ordinal))
                    {
                        prop.SetValue(obj, GetDefaultValue(prop.PropertyType));
                    }
                    else
                    {
                        var value = reader.GetValue(ordinal);
                        prop.SetValue(obj, Convert.ChangeType(value, prop.PropertyType));
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // Пропускаем свойства, которых нет в результате запроса
                    continue;
                }
            }
        }

        /// <summary>
        /// Возвращает значение по умолчанию для указанного типа.
        /// </summary>
        /// <param name="type">Тип данных</param>
        /// <returns>Значение по умолчанию для типа</returns>
        private object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Формирует SQL-запрос UPDATE для обновления объекта в БД.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <param name="dto">Объект с данными для обновления</param>
        /// <returns>SQL-запрос UPDATE</returns>
        private string GetUpdateByObj<T>(T dto)
        {
            var sb = new StringBuilder();
            sb.Append($"UPDATE {GetNameTable<T>()} SET ");

            var listFields = GetFieldsObject<T>();
            var parameters = new List<MySqlParameter>();

            foreach (var field in listFields)
            {
                if (!_listKeys.Exists(x => x.Equals(field.Name)))
                {
                    var value = field.GetValue(dto);
                    sb.Append($"{field.Name}=@{field.Name}, ");
                    parameters.Add(new MySqlParameter($"@{field.Name}", value ?? DBNull.Value));
                }
            }

            sb.Remove(sb.Length - 2, 2); // Удаляем последнюю запятую и пробел
            sb.Append(" WHERE ");

            foreach (var key in _listKeys)
            {
                var prop = listFields.FirstOrDefault(f => f.Name == key);
                if (prop != null)
                {
                    var value = prop.GetValue(dto);
                    sb.Append($"{key}=@{key}_cond AND ");
                    parameters.Add(new MySqlParameter($"@{key}_cond", value ?? DBNull.Value));
                }
            }

            sb.Remove(sb.Length - 5, 5); // Удаляем последний " AND "

            return sb.ToString();
        }

        /// <summary>
        /// Формирует SQL-запрос DELETE для удаления объекта из БД.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <param name="dto">Объект для удаления</param>
        /// <returns>SQL-запрос DELETE</returns>
        private string GetDeleteSQLByObj<T>(T dto)
        {
            var sb = new StringBuilder();
            sb.Append($"DELETE FROM {GetNameTable<T>()} WHERE ");

            var listFields = GetFieldsObject<T>();

            foreach (var key in _listKeys)
            {
                var prop = listFields.FirstOrDefault(f => f.Name == key);
                if (prop != null)
                {
                    var value = prop.GetValue(dto);
                    sb.Append($"{key}=@{key}_cond AND ");
                }
            }

            sb.Remove(sb.Length - 5, 5); // Удаляем последний " AND "

            return sb.ToString();
        }

        /// <summary>
        /// Получает список первичных ключей для таблицы, соответствующей типу T.
        /// </summary>
        /// <typeparam name="T">Тип объекта, соответствующий таблице БД</typeparam>
        /// <returns>Список имен первичных ключей</returns>
        private List<string> GetPrimaryKeys<T>()
        {
            var list = new List<string>();

            try
            {
                var sql = $"SHOW KEYS FROM {GetNameTable<T>()} WHERE Key_name = 'PRIMARY'";

                _connection.Open();
                using var cmd = new MySqlCommand(sql, _connection);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(reader.GetString(reader.GetOrdinal("Column_name")));
                }
            }
            catch (Exception ex)
            {
                Log.WriteException(ex);
                throw;
            }
            finally
            {
                _connection.Close();
            }

            return list;
        }

        /// <summary>
        /// Возвращает имя таблицы БД на основе имени типа.
        /// </summary>
        /// <typeparam name="T">Тип объекта</typeparam>
        /// <returns>Имя таблицы в БД</returns>
        private string GetNameTable<T>()
        {
            return typeof(T).Name;
        }

        /// <summary>
        /// Внутренний метод для выполнения SQL-запросов без возврата результата.
        /// </summary>
        /// <param name="sql">SQL-запрос для выполнения</param>
        private void ExecuteNonSQLInternal(string sql)
        {
            using var cmd = new MySqlCommand(sql, _connection);
            cmd.ExecuteNonQuery();
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Освобождает ресурсы, связанные с соединением с БД.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Защищенный метод для освобождения управляемых и неуправляемых ресурсов.
        /// </summary>
        /// <param name="disposing">True - освобождать управляемые ресурсы, False - только неуправляемые</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _connection?.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Финализатор. Вызывается сборщиком мусора, если объект не был корректно удален.
        /// </summary>
        ~DbConnectMySQL()
        {
            Dispose(false);
        }

        #endregion
    }
}