using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using task6.Models;

namespace task6.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IConfiguration configuration, ILogger<UsersController> logger)
        {
            _configuration = configuration;
            _logger = logger;

            try
            {
                InitializeDatabase();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка инициализации базы данных");
            }
        }

        private void InitializeDatabase()
        {
            string connStr = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connStr))
            {
                throw new ArgumentException("Строка подключения к БД не настроена в appsettings.json");
            }

            using (SqliteConnection conn = new SqliteConnection(connStr))
            {
                conn.Open();

                // 1. Таблица Users
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL,
                        Email TEXT NOT NULL,
                        Age INTEGER
                    )";

                using (SqliteCommand cmd = new SqliteCommand(createTableQuery, conn))
                {
                    cmd.ExecuteNonQuery();
                    _logger.LogInformation("Таблица Users создана или уже существует");
                }

                // 2. Таблица логов (ЗАДАНИЕ 5)
                string createLogsTableQuery = @"
                    CREATE TABLE IF NOT EXISTS UpdateLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NOT NULL,
                        OperationType TEXT NOT NULL,
                        OperationTime TEXT NOT NULL,
                        Status TEXT NOT NULL,
                        Details TEXT,
                        IpAddress TEXT,
                        UserAgent TEXT
                    )";

                using (SqliteCommand logsCmd = new SqliteCommand(createLogsTableQuery, conn))
                {
                    logsCmd.ExecuteNonQuery();
                    _logger.LogInformation("Таблица UpdateLogs создана или уже существует");
                }

                // 3. Тестовые данные
                string checkDataQuery = "SELECT COUNT(*) FROM Users";
                using (SqliteCommand cmd = new SqliteCommand(checkDataQuery, conn))
                {
                    long count = (long)cmd.ExecuteScalar();
                    if (count == 0)
                    {
                        string insertDataQuery = @"
                            INSERT INTO Users (Name, Email, Age) VALUES 
                            ('Иван', 'ivan@mail.com', 25),
                            ('Мария', 'maria@mail.com', 30)";
                        using (SqliteCommand insertCmd = new SqliteCommand(insertDataQuery, conn))
                        {
                            insertCmd.ExecuteNonQuery();
                            _logger.LogInformation("Добавлены тестовые пользователи");
                        }
                    }
                }
            }
        }

        // ==================== МЕТОДЫ ЛОГИРОВАНИЯ (ЗАДАНИЕ 5) ====================

        private void LogUpdateOperation(int userId, string operationType, string status, string details = null)
        {
            try
            {
                string connStr = _configuration.GetConnectionString("DefaultConnection");

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    string query = @"
                        INSERT INTO UpdateLogs 
                        (UserId, OperationType, OperationTime, Status, Details, IpAddress, UserAgent) 
                        VALUES (@UserId, @OperationType, @OperationTime, @Status, @Details, @IpAddress, @UserAgent)";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@OperationType", operationType);
                        cmd.Parameters.AddWithValue("@OperationTime", DateTime.UtcNow.ToString("o"));
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@Details", details ?? string.Empty);
                        cmd.Parameters.AddWithValue("@IpAddress", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
                        cmd.Parameters.AddWithValue("@UserAgent", HttpContext.Request.Headers.UserAgent.ToString() ?? "Unknown");

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                _logger.LogInformation("Запись в лог: {OperationType} для пользователя {UserId} - {Status}",
                    operationType, userId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при записи в лог для пользователя {UserId}", userId);
            }
        }

        // ==================== ПОМОЩНИКИ ДЛЯ ОБРАБОТКИ ИСКЛЮЧЕНИЙ ====================

        private IActionResult HandleDatabaseException(Exception ex, string operationName)
        {
            _logger.LogError(ex, "Ошибка базы данных при выполнении {Operation}", operationName);

            var errorResponse = ApiErrorResponse.Create(
                $"Ошибка при выполнении операции '{operationName}'",
                "DatabaseError"
            );

            if (ex is Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                errorResponse.Details.Add("SqliteErrorCode", sqlEx.SqliteErrorCode.ToString());
                errorResponse.Details.Add("ErrorMessage", sqlEx.Message);
            }

            return StatusCode(500, errorResponse);
        }

        private IActionResult HandleValidationException(Exception ex)
        {
            var errorResponse = ApiErrorResponse.Create(
                "Ошибка валидации данных",
                "ValidationError"
            );
            errorResponse.Details.Add("ValidationError", ex.Message);

            return BadRequest(errorResponse);
        }

        private IActionResult HandleNotFoundException(string message)
        {
            var errorResponse = ApiErrorResponse.Create(
                message,
                "NotFound"
            );

            return NotFound(errorResponse);
        }

        // ==================== GET МЕТОДЫ ====================

        [HttpGet]
        public IActionResult GetUsers()
        {
            try
            {
                List<User> users = new List<User>();
                string connStr = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connStr))
                {
                    throw new ArgumentException("Строка подключения к БД не настроена");
                }

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    string query = "SELECT Id, Name, Email, Age FROM Users";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    User user = new User
                                    {
                                        Id = reader.GetInt32(0),
                                        Name = reader.GetString(1),
                                        Email = reader.GetString(2),
                                        Age = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)
                                    };
                                    users.Add(user);
                                }
                                catch (InvalidCastException castEx)
                                {
                                    _logger.LogWarning(castEx, "Ошибка при чтении данных пользователя");
                                    continue;
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Получено {Count} пользователей", users.Count);
                return Ok(users);
            }
            catch (ArgumentException argEx)
            {
                return HandleValidationException(argEx);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                return HandleDatabaseException(sqlEx, "GetUsers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при получении пользователей");
                return StatusCode(500, ApiErrorResponse.Create(
                    "Внутренняя ошибка сервера",
                    "InternalError"
                ));
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetUser(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID должен быть положительным числом");
                }

                string connStr = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connStr))
                {
                    throw new ArgumentException("Строка подключения к БД не настроена");
                }

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    string query = "SELECT Id, Name, Email, Age FROM Users WHERE Id = @Id";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                User user = new User
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    Email = reader.GetString(2),
                                    Age = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)
                                };

                                _logger.LogInformation("Получен пользователь с ID {Id}", id);
                                return Ok(user);
                            }
                        }
                    }
                }

                return HandleNotFoundException($"Пользователь с ID {id} не найден");
            }
            catch (ArgumentException argEx)
            {
                return HandleValidationException(argEx);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                return HandleDatabaseException(sqlEx, "GetUser");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при получении пользователя с ID {Id}", id);
                return StatusCode(500, ApiErrorResponse.Create(
                    "Внутренняя ошибка сервера",
                    "InternalError"
                ));
            }
        }

        // ==================== МЕТОДЫ ДЛЯ РАБОТЫ С ЛОГАМИ (ЗАДАНИЕ 5) ====================

        [HttpGet("logs")]
        public IActionResult GetLogs([FromQuery] int? userId = null, [FromQuery] int limit = 50)
        {
            try
            {
                string connStr = _configuration.GetConnectionString("DefaultConnection");
                List<object> logs = new List<object>();

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    string query = @"
                        SELECT Id, UserId, OperationType, OperationTime, Status, Details, IpAddress, UserAgent 
                        FROM UpdateLogs 
                        WHERE (@UserId IS NULL OR UserId = @UserId)
                        ORDER BY OperationTime DESC 
                        LIMIT @Limit";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId.HasValue ? (object)userId.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@Limit", limit);

                        conn.Open();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                logs.Add(new
                                {
                                    Id = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    OperationType = reader.GetString(2),
                                    OperationTime = reader.GetString(3),
                                    Status = reader.GetString(4),
                                    Details = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    IpAddress = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    UserAgent = reader.IsDBNull(7) ? null : reader.GetString(7)
                                });
                            }
                        }
                    }
                }

                _logger.LogInformation("Получено {Count} записей логов", logs.Count);
                return Ok(new
                {
                    TotalLogs = logs.Count,
                    Logs = logs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении логов");
                return StatusCode(500, ApiErrorResponse.Create(
                    "Ошибка при получении логов",
                    "LogRetrievalError"
                ));
            }
        }

        [HttpGet("logs/stats")]
        public IActionResult GetLogsStatistics()
        {
            try
            {
                string connStr = _configuration.GetConnectionString("DefaultConnection");

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    conn.Open();

                    // Общая статистика
                    string statsQuery = @"
                        SELECT 
                            COUNT(*) as Total,
                            SUM(CASE WHEN Status = 'SUCCESS' THEN 1 ELSE 0 END) as Success,
                            SUM(CASE WHEN Status = 'FAILED' THEN 1 ELSE 0 END) as Failed,
                            COUNT(DISTINCT UserId) as UniqueUsers,
                            MIN(OperationTime) as FirstLog,
                            MAX(OperationTime) as LastLog
                        FROM UpdateLogs";

                    using (SqliteCommand cmd = new SqliteCommand(statsQuery, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var stats = new
                            {
                                TotalOperations = reader.GetInt64(0),
                                SuccessOperations = reader.GetInt64(1),
                                FailedOperations = reader.GetInt64(2),
                                UniqueUsers = reader.GetInt64(3),
                                FirstLog = reader.IsDBNull(4) ? null : reader.GetString(4),
                                LastLog = reader.IsDBNull(5) ? null : reader.GetString(5)
                            };

                            _logger.LogInformation("Получена статистика логов: {Total} операций", stats.TotalOperations);
                            return Ok(stats);
                        }
                    }
                }

                return Ok(new { Message = "Нет данных в логах" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики логов");
                return StatusCode(500, ApiErrorResponse.Create(
                    "Ошибка при получении статистики логов",
                    "LogStatsError"
                ));
            }
        }

        // ==================== PUT МЕТОДЫ (С ЛОГИРОВАНИЕМ) ====================

        [HttpPut("{id}")]
        public IActionResult UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    throw new ArgumentException($"Ошибки валидации: {string.Join(", ", errors)}");
                }

                string connStr = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connStr))
                {
                    throw new ArgumentException("Строка подключения к БД не настроена");
                }

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    conn.Open();

                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            _logger.LogInformation("Начало транзакции для обновления пользователя ID {Id}", id);

                            // Проверяем существование пользователя
                            string checkQuery = "SELECT COUNT(*) FROM Users WHERE Id = @Id";
                            using (SqliteCommand checkCmd = new SqliteCommand(checkQuery, conn, transaction))
                            {
                                checkCmd.Parameters.AddWithValue("@Id", id);
                                long userExists = (long)checkCmd.ExecuteScalar();

                                if (userExists == 0)
                                {
                                    throw new KeyNotFoundException($"Пользователь с ID {id} не найден");
                                }
                            }

                            // Обновляем данные пользователя
                            string updateQuery = "UPDATE Users SET Name = @Name, Email = @Email, Age = @Age WHERE Id = @Id";
                            using (SqliteCommand updateCmd = new SqliteCommand(updateQuery, conn, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@Id", id);
                                updateCmd.Parameters.AddWithValue("@Name", request.Name);
                                updateCmd.Parameters.AddWithValue("@Email", request.Email);

                                if (request.Age.HasValue)
                                {
                                    updateCmd.Parameters.AddWithValue("@Age", request.Age.Value);
                                }
                                else
                                {
                                    updateCmd.Parameters.AddWithValue("@Age", DBNull.Value);
                                }

                                int updateResult = updateCmd.ExecuteNonQuery();

                                if (updateResult < 1)
                                {
                                    throw new Exception("Обновление данных не выполнено");
                                }
                            }

                            // ЗАПИСЫВАЕМ В ЛОГ (ЗАДАНИЕ 5)
                            string logQuery = @"
                                INSERT INTO UpdateLogs 
                                (UserId, OperationType, OperationTime, Status, Details, IpAddress, UserAgent) 
                                VALUES (@UserId, @OperationType, @OperationTime, @Status, @Details, @IpAddress, @UserAgent)";

                            using (SqliteCommand logCmd = new SqliteCommand(logQuery, conn, transaction))
                            {
                                logCmd.Parameters.AddWithValue("@UserId", id);
                                logCmd.Parameters.AddWithValue("@OperationType", "UPDATE_USER");
                                logCmd.Parameters.AddWithValue("@OperationTime", DateTime.UtcNow.ToString("o"));
                                logCmd.Parameters.AddWithValue("@Status", "SUCCESS");
                                logCmd.Parameters.AddWithValue("@Details", $"Обновлены поля: Name={request.Name}, Email={request.Email}, Age={request.Age}");
                                logCmd.Parameters.AddWithValue("@IpAddress", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
                                logCmd.Parameters.AddWithValue("@UserAgent", HttpContext.Request.Headers.UserAgent.ToString() ?? "Unknown");

                                logCmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            _logger.LogInformation("Транзакция успешно завершена для пользователя ID {Id}", id);

                            return Ok("Данные пользователя обновлены");
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                transaction.Rollback();
                                _logger.LogWarning("Транзакция откачена для пользователя ID {Id}", id);

                                // ЗАПИСЫВАЕМ ОШИБКУ В ЛОГ (ЗАДАНИЕ 5)
                                LogUpdateOperation(id, "UPDATE_USER", "FAILED", $"Ошибка: {ex.Message}");
                            }
                            catch { }

                            throw;
                        }
                    }
                }
            }
            catch (KeyNotFoundException notFoundEx)
            {
                LogUpdateOperation(id, "UPDATE_USER", "FAILED", $"Не найден: {notFoundEx.Message}");
                return HandleNotFoundException(notFoundEx.Message);
            }
            catch (ArgumentException argEx)
            {
                LogUpdateOperation(id, "UPDATE_USER", "FAILED", $"Валидация: {argEx.Message}");
                return HandleValidationException(argEx);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                LogUpdateOperation(id, "UPDATE_USER", "FAILED", $"БД: {sqlEx.Message}");
                return HandleDatabaseException(sqlEx, "UpdateUser");
            }
            catch (Exception ex)
            {
                LogUpdateOperation(id, "UPDATE_USER", "FAILED", $"Неизвестно: {ex.Message}");
                _logger.LogError(ex, "Неожиданная ошибка при обновлении пользователя с ID {Id}", id);
                return StatusCode(500, ApiErrorResponse.Create(
                    "Внутренняя ошибка сервера при обновлении данных",
                    "InternalError"
                ));
            }
        }

        [HttpPut("{id}/email")]
        public IActionResult UpdateUserEmail(int id, [FromBody] UpdateEmailRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    throw new ArgumentException($"Ошибки валидации: {string.Join(", ", errors)}");
                }

                string connStr = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connStr))
                {
                    throw new ArgumentException("Строка подключения к БД не настроена");
                }

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    string query = @"
                        UPDATE Users 
                        SET Email = @NewEmail 
                        WHERE Id = @Id AND Name = @CurrentName";

                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@CurrentName", request.CurrentName);
                        cmd.Parameters.AddWithValue("@NewEmail", request.NewEmail);

                        conn.Open();
                        int result = cmd.ExecuteNonQuery();

                        if (result < 1)
                        {
                            LogUpdateOperation(id, "UPDATE_EMAIL", "FAILED",
                                "Пользователь не найден или имя не совпадает");

                            return BadRequest(new
                            {
                                message = "Обновление не удалось",
                                reason = "Пользователь не найден или имя не совпадает"
                            });
                        }
                    }
                }

                LogUpdateOperation(id, "UPDATE_EMAIL", "SUCCESS",
                    $"Новый email: {request.NewEmail}, Проверка имени: {request.CurrentName}");

                _logger.LogInformation("Email пользователя ID {Id} успешно обновлен", id);
                return Ok(new
                {
                    message = "Email успешно обновлен",
                    userId = id,
                    newEmail = request.NewEmail
                });
            }
            catch (ArgumentException argEx)
            {
                LogUpdateOperation(id, "UPDATE_EMAIL", "FAILED", $"Валидация: {argEx.Message}");
                return HandleValidationException(argEx);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                LogUpdateOperation(id, "UPDATE_EMAIL", "FAILED", $"БД: {sqlEx.Message}");
                return HandleDatabaseException(sqlEx, "UpdateUserEmail");
            }
            catch (Exception ex)
            {
                LogUpdateOperation(id, "UPDATE_EMAIL", "FAILED", $"Неизвестно: {ex.Message}");
                _logger.LogError(ex, "Неожиданная ошибка при обновлении email пользователя с ID {Id}", id);
                return StatusCode(500, ApiErrorResponse.Create(
                    "Внутренняя ошибка сервера при обновлении email",
                    "InternalError"
                ));
            }
        }

        // ==================== POST МЕТОДЫ (С ЛОГИРОВАНИЕМ) ====================

        [HttpPost]
        public IActionResult CreateUser([FromBody] UpdateUserRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    throw new ArgumentException($"Ошибки валидации: {string.Join(", ", errors)}");
                }

                string connStr = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connStr))
                {
                    throw new ArgumentException("Строка подключения к БД не настроена");
                }

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    string query = "INSERT INTO Users (Name, Email, Age) VALUES (@Name, @Email, @Age)";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", request.Name);
                        cmd.Parameters.AddWithValue("@Email", request.Email);

                        if (request.Age.HasValue)
                        {
                            cmd.Parameters.AddWithValue("@Age", request.Age.Value);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@Age", DBNull.Value);
                        }

                        conn.Open();
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = "SELECT last_insert_rowid()";
                        int newId = Convert.ToInt32(cmd.ExecuteScalar());

                        // ЗАПИСЫВАЕМ В ЛОГ (ЗАДАНИЕ 5)
                        LogUpdateOperation(newId, "CREATE_USER", "SUCCESS",
                            $"Создан пользователь: {request.Name}, {request.Email}");

                        _logger.LogInformation("Создан новый пользователь с ID {Id}", newId);

                        return CreatedAtAction(nameof(GetUser), new { id = newId }, new
                        {
                            id = newId,
                            name = request.Name,
                            email = request.Email,
                            age = request.Age
                        });
                    }
                }
            }
            catch (ArgumentException argEx)
            {
                LogUpdateOperation(0, "CREATE_USER", "FAILED", $"Валидация: {argEx.Message}");
                return HandleValidationException(argEx);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                LogUpdateOperation(0, "CREATE_USER", "FAILED", $"БД: {sqlEx.Message}");
                return HandleDatabaseException(sqlEx, "CreateUser");
            }
            catch (Exception ex)
            {
                LogUpdateOperation(0, "CREATE_USER", "FAILED", $"Неизвестно: {ex.Message}");
                _logger.LogError(ex, "Неожиданная ошибка при создании пользователя");
                return StatusCode(500, ApiErrorResponse.Create(
                    "Внутренняя ошибка сервера при создании пользователя",
                    "InternalError"
                ));
            }
        }

        // ==================== DELETE МЕТОДЫ (С ЛОГИРОВАНИЕМ) ====================

        [HttpDelete("{id}")]
        public IActionResult DeleteUser(int id)
        {
            try
            {
                if (id <= 0)
                {
                    throw new ArgumentException("ID должен быть положительным числом");
                }

                string connStr = _configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connStr))
                {
                    throw new ArgumentException("Строка подключения к БД не настроена");
                }

                using (SqliteConnection conn = new SqliteConnection(connStr))
                {
                    string query = "DELETE FROM Users WHERE Id = @Id";
                    using (SqliteCommand cmd = new SqliteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);

                        conn.Open();
                        int result = cmd.ExecuteNonQuery();

                        if (result < 1)
                        {
                            LogUpdateOperation(id, "DELETE_USER", "FAILED", "Пользователь не найден");
                            return HandleNotFoundException($"Пользователь с ID {id} не найден");
                        }

                        // ЗАПИСЫВАЕМ В ЛОГ (ЗАДАНИЕ 5)
                        LogUpdateOperation(id, "DELETE_USER", "SUCCESS", "Пользователь удален");

                        _logger.LogInformation("Пользователь с ID {Id} удален", id);
                        return Ok($"Пользователь с ID {id} удален");
                    }
                }
            }
            catch (ArgumentException argEx)
            {
                LogUpdateOperation(id, "DELETE_USER", "FAILED", $"Валидация: {argEx.Message}");
                return HandleValidationException(argEx);
            }
            catch (Microsoft.Data.Sqlite.SqliteException sqlEx)
            {
                LogUpdateOperation(id, "DELETE_USER", "FAILED", $"БД: {sqlEx.Message}");
                return HandleDatabaseException(sqlEx, "DeleteUser");
            }
            catch (Exception ex)
            {
                LogUpdateOperation(id, "DELETE_USER", "FAILED", $"Неизвестно: {ex.Message}");
                _logger.LogError(ex, "Неожиданная ошибка при удалении пользователя с ID {Id}", id);
                return StatusCode(500, ApiErrorResponse.Create(
                    "Внутренняя ошибка сервера при удалении пользователя",
                    "InternalError"
                ));
            }
        }
    }
}