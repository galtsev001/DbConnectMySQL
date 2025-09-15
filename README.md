# DbConnectMySQL

Простая и удобная библиотека для работы с MySQL в .NET, предоставляющая автоматическое маппирование объектов C# на таблицы базы данных и базовые CRUD-операции.

## Особенности

- **Автоматическое маппирование:** Преобразование результатов SQL-запросов в объекты C# и обратно
- **CRUD-операции:** Полный набор методов для работы с данными (Create, Read, Update, Delete)
- **Гибкая конфигурация:** Поддержка разных способов настройки подключения
- **Транзакции:** Автоматическое управление транзакциями для операций изменения данных
- **Логирование:** Встроенная система логирования с ротацией файлов
- **Безопасность:** Защита от SQL-инъекций через параметризованные запросы

## Требования

- .NET 8.0 или выше
- MySQL Server 5.7+ / MariaDB 10.3+
- Доступ к базе данных с соответствующими правами

## Установка

### 1. Добавление пакета NuGet

Добавьте в ваш проект следующие пакеты NuGet:

```bash
dotnet add package MySql.Data
dotnet add package Microsoft.Extensions.Configuration
```

### 2. Клонирование проекта

```bash
git clone https://github.com/your-username/DbConnectMySQL.git
```

### 3.Добавление в проект

Добавьте файлы в ваш проект:

* DbConnectMySQL.cs
* AppSettings.cs
* Log.cs

### 4. Настройка

##### Способ 1: Через файл appsettings.json
Добавьте в корень вашего проекта файл appsettings.json:

```json
{
  "host": "localhost",
  "port": 3306,
  "user": "your_username",
  "password": "your_password",
  "database": "your_database"
}
```

##### Способ 2: Прямое указание параметров
```csharp
using var db = new DbConnectMySQL(
    host: "localhost",
    port: 3306,
    user: "username",
    password: "password",
    database: "database_name"
);
```
### 5. Примеры операций
**Выборка данных**
```csharp
using var db = new DbConnectMySQL();

// Получить все записи
var allUsers = db.Sel_Data<User>();

// Выборка с условием
db.Where = "WHERE Email LIKE '%@gmail.com'";
var gmailUsers = db.Sel_Data<User>();

// Кастомный SQL-запрос
var customQuery = "SELECT Id, Name FROM Users WHERE CreatedAt > '2024-01-01'";
var recentUsers = db.Sel_Data<User>(customQuery);
```

**Добавление данных**
```csharp
var newUsers = new List<User>
{
    new User { Name = "Alice", Email = "alice@example.com", CreatedAt = DateTime.Now },
    new User { Name = "Bob", Email = "bob@example.com", CreatedAt = DateTime.Now }
};

using var db = new DbConnectMySQL();
bool success = db.Ins_Data(newUsers);
```

**Обновление данных**
```csharp
var usersToUpdate = new List<User>
{
    new User { Id = 1, Name = "Alice Smith", Email = "alice.smith@example.com" }
};

using var db = new DbConnectMySQL();
bool success = db.Update_Data(usersToUpdate);
```

**Удаление данных**
```csharp
var usersToDelete = new List<User> 
{ 
    new User { Id = 42 } 
};

using var db = new DbConnectMySQL();
bool success = db.Del_Data(usersToDelete);
```

**Произвольные SQL-запросы**
```csharp
using var db = new DbConnectMySQL();
db.ExecuteNonSQL("UPDATE Users SET Status = 'inactive' WHERE LastLogin < '2024-01-01'");
```

### 6.Настройка логирования
Библиотека автоматически создает папку logs и записывает туда файлы с ошибками. Настройки по умолчанию:

+ Путь: ./logs/
+ Имя файла: _Log.log
+ Хранение: 30 дней

Вы можете изменить настройки:

```csharp
Log.FilePath = "/var/log/myapp";
Log.FileName = "database";
Log.SaveDays = 90;
```

### Важные примечания
Соглашения об именах
+ Имя класса должно совпадать с именем таблицы в БД
+ Свойства класса должны соответствовать столбцам таблицы
+ Первичные ключи определяются автоматически через SHOW KEYS

Обработка ошибок
+ Все исключения логируются в файл
+ Методы возвращают bool для операций изменения данных
+ Исключения пробрасываются для операций выборки данных

Требования к базе данных
+ Пользователь БД должен иметь права на:
`SELECT, INSERT, UPDATE, DELETE для нужных таблиц`
+ Выполнение SHOW KEYS для определения первичных ключей

### Лицензия
MIT License - смотрите файл LICENSE для деталей.

### Поддержка
Если у вас есть вопросы или предложения:
+ Создайте Issue в репозитории
+ Напишите на email: galtsev001@gmail.com
+ Проверьте документацию в коде методов