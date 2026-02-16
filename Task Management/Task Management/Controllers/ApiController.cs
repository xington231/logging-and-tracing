using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Npgsql;
using System.Collections.Concurrent;
using System.Diagnostics;
using Task_Management.Models;

namespace Task_Management.Controllers
{
    [ApiController]
    [Route("api/[controller]")]

    public class ApiController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiController> _logger;

        public ApiController(IConfiguration configuration, ILogger<ApiController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("CreateTask")]
        public IActionResult CreateTask([FromBody] CurrentTask request)
        {
            var requestTime = DateTime.UtcNow;
            Tracer.TaskManagerTrace.TraceEvent(TraceEventType.Start, 0, "Начало CreateTask");
            Stopwatch sw = Stopwatch.StartNew();

            string connectionString = _configuration.GetConnectionString("task_management");
            if (string.IsNullOrEmpty(connectionString))
            {

                            Tracer.TaskManagerTrace.TraceEvent(
                   TraceEventType.Warning,
                   2,
                   "Попытка добавить подключение с пустым названием."
               );
                _logger.LogError($"[{DateTime.UtcNow}] Ошибка: строка подключения 'task_management' не найдена или пуста.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Ошибка конфигурации: строка подключения не найдена.");
            }

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string query = @"INSERT INTO current_tasks 
                (task_name, task_description, dateadded, deadlinedate, iscompleted, statusid, priorityid) 
                VALUES 
                (@task_name, @task_description, @dateadded, @deadlinedate, @iscompleted, @statusid, @priorityid)
                RETURNING task_id";

                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@task_name", request.task_name ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@task_description", request.task_description ?? (object)DBNull.Value);
                            command.Parameters.Add(new NpgsqlParameter("@dateadded", NpgsqlTypes.NpgsqlDbType.Date)
                            { Value = request.dateadded });
                            command.Parameters.Add(new NpgsqlParameter("@deadlinedate", NpgsqlTypes.NpgsqlDbType.Date)
                            { Value = request.deadlinedate });
                            command.Parameters.AddWithValue("@iscompleted", request.iscompleted);
                            command.Parameters.AddWithValue("@statusid", request.statusid);
                            command.Parameters.AddWithValue("@priorityid", request.priorityid);
                            var newTaskId = command.ExecuteScalar();

                            if (newTaskId == null)
                            {
                                transaction.Rollback();
                                _logger.LogWarning("[{Time}] Ошибка: задача не была создана.", requestTime);
                                return BadRequest("Создание задачи не удалось.");
                            }

                            transaction.Commit();
                            Tracer.TaskManagerTrace.TraceEvent(
                                TraceEventType.Stop,
                                1,
                                $"Завершение CreateTask. Время: {sw.ElapsedMilliseconds} мс"
                            );
                            _logger.LogInformation("[{Time}] Задача успешно создана с TaskID={TaskID}.",requestTime, newTaskId);
                            return Ok(new
                            {
                                task_id = newTaskId,
                                message = "Задача успешно создана."
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Tracer.TaskManagerTrace.TraceEvent(
                            TraceEventType.Error,
                            3,
                            $"Ошибка в CreateTask: {ex.Message}"
                        );
                        transaction.Rollback();
                        _logger.LogError(ex, "[{Time}] Ошибка при создании задачи.", requestTime);
                        return StatusCode(StatusCodes.Status500InternalServerError, $"Ошибка сервера: {ex.Message}");
                    }
                }
            }
        }

        [HttpGet("GetTasks")]
        public IActionResult GetTasks([FromQuery] int? statusid = null)
        {
            var requestTime = DateTime.UtcNow;
            string connectionString = _configuration.GetConnectionString("task_management");

            Tracer.TaskManagerTrace.TraceEvent(TraceEventType.Start, 0, "Начало GetTasks");
            Stopwatch sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(connectionString))
            {
                Tracer.TaskManagerTrace.TraceEvent(
                   TraceEventType.Warning,
                   2,
                   "Попытка добавить подключение с пустым названием.");
                _logger.LogError("[{Time}] Ошибка: строка подключения 'task_management' не найдена или пуста.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ошибка конфигурации: строка подключения не найдена.");
            }

            try
            {
                List<CurrentTask> tasks = new List<CurrentTask>();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT * FROM current_tasks";
                    if (statusid.HasValue && statusid.Value > 0)
                    {
                        query += " AND statusid = @statusid";
                    }
                    using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                    {
                        if (statusid.HasValue && statusid.Value > 0)
                        {
                            command.Parameters.AddWithValue("@statusid", statusid.Value);
                        }
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var task = new CurrentTask
                                {
                                    task_id = reader.GetInt32(reader.GetOrdinal("task_id")),
                                    task_name = reader.IsDBNull(reader.GetOrdinal("task_name")) ? null : reader.GetString(reader.GetOrdinal("task_name")),
                                    task_description = reader.IsDBNull(reader.GetOrdinal("task_description")) ? null : reader.GetString(reader.GetOrdinal("task_description")),
                                    dateadded = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("dateadded"))),
                                    deadlinedate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("deadlinedate"))),
                                    iscompleted = reader.GetBoolean(reader.GetOrdinal("iscompleted")),
                                    statusid = reader.GetInt32(reader.GetOrdinal("statusid")),
                                    priorityid = reader.GetInt32(reader.GetOrdinal("priorityid"))
                                };
                                tasks.Add(task);
                            }
                        }
                    }
                }

                if (tasks.Count == 0)
                {
                    Tracer.TaskManagerTrace.TraceEvent(
                                TraceEventType.Warning,
                                1,
                                $"Попытка получить пустой список задач"
                            );
                    _logger.LogInformation("[{Time}] Задачи не найдены.", requestTime);
                    return Ok(new List<CurrentTask>());
                }
                Tracer.TaskManagerTrace.TraceEvent(
                                TraceEventType.Stop,
                                1,
                                $"Завершение GetTasks. Время: {sw.ElapsedMilliseconds} мс"
                            );
                _logger.LogInformation("[{Time}] Успешно получено {Count} задач.", requestTime, tasks.Count);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                Tracer.TaskManagerTrace.TraceEvent(
                    TraceEventType.Error,
                    3,
                    $"Ошибка в GetTasks: {ex.Message}"
                );
                _logger.LogError(ex, "[{Time}] Ошибка при получении задач.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, $"Ошибка сервера: {ex.Message}");
            }
        }



        [HttpPut("UpdateTask")]
        public IActionResult UpdateTask([FromBody] CurrentTask request)
        {
            var requestTime = DateTime.UtcNow;
            string connectionString = _configuration.GetConnectionString("task_management");

            Tracer.TaskManagerTrace.TraceEvent(TraceEventType.Start, 0, "Начало UpdateTask");
            Stopwatch sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(connectionString))
            {
                Tracer.TaskManagerTrace.TraceEvent(
                   TraceEventType.Warning,
                   2,
                   "Попытка добавить подключение с пустым названием.");
                _logger.LogError("[{Time}] Ошибка: строка подключения 'task_management' не найдена или пуста.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ошибка конфигурации: строка подключения не найдена.");
            }

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string query = @"UPDATE current_tasks 
                                SET task_name = @task_name,task_description = @task_description,dateadded = @dateadded,deadlinedate = @deadlinedate,iscompleted = @iscompleted,statusid = @statusid,priorityid = @priorityid
                        WHERE task_id = @task_id";

                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@task_id", request.task_id);
                            command.Parameters.AddWithValue("@task_name", request.task_name ?? (object)DBNull.Value);
                            command.Parameters.AddWithValue("@task_description", request.task_description ?? (object)DBNull.Value);
                            command.Parameters.Add(new NpgsqlParameter("@dateadded", NpgsqlTypes.NpgsqlDbType.Date)
                            { Value = request.dateadded });
                            command.Parameters.Add(new NpgsqlParameter("@deadlinedate", NpgsqlTypes.NpgsqlDbType.Date)
                            { Value = request.deadlinedate });
                            command.Parameters.AddWithValue("@iscompleted", request.iscompleted);
                            command.Parameters.AddWithValue("@statusid", request.statusid);
                            command.Parameters.AddWithValue("@priorityid", request.priorityid);

                            int result = command.ExecuteNonQuery();

                            if (result < 1)
                            {
                                Tracer.TaskManagerTrace.TraceEvent(
                                TraceEventType.Warning,
                                1,
                                $"Попытка обновить несуществующую задачу"
                            );
                                transaction.Rollback();
                                _logger.LogWarning("[{Time}] Ошибка: задача с ID={TaskID} не была обновлена (не найдена).",requestTime, request.task_id);
                                return NotFound($"Задача с ID {request.task_id} не найдена.");
                            }
                        }
                        Tracer.TaskManagerTrace.TraceEvent(
                               TraceEventType.Stop,
                               1,
                               $"Завершение UpdateTask. Время: {sw.ElapsedMilliseconds} мс"
                           );
                        transaction.Commit();
                        _logger.LogInformation("[{Time}] Задача с TaskID={TaskID} успешно обновлена.",requestTime, request.task_id);
                        return Ok($"Задача с ID {request.task_id} успешно обновлена.");
                    }
                    catch (Exception ex)
                    {
                                Tracer.TaskManagerTrace.TraceEvent(
                            TraceEventType.Error,
                            3,
                            $"Ошибка в UpdateTask: {ex.Message}"
                        );
                        transaction.Rollback();
                        _logger.LogError(ex, "[{Time}] Ошибка: не удалось обновить задачу с ID={TaskID}.",requestTime, request.task_id);
                        return StatusCode(StatusCodes.Status500InternalServerError, $"Ошибка сервера: {ex.Message}");
                    }
                }
            }
        }
        [HttpDelete("DeleteTask/{task_id}")]
        public IActionResult DeleteTask(int task_id)
        {
            var requestTime = DateTime.UtcNow;
            string connectionString = _configuration.GetConnectionString("task_management");

            Tracer.TaskManagerTrace.TraceEvent(TraceEventType.Start, 0, "Начало DeleteTask");
            Stopwatch sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(connectionString))
            {
                Tracer.TaskManagerTrace.TraceEvent(
                   TraceEventType.Warning,
                   2,
                   "Попытка добавить подключение с пустым названием.");
                _logger.LogError("[{Time}] Ошибка: строка подключения 'task_management' не найдена или пуста.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ошибка конфигурации: строка подключения не найдена.");
            }

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string query = @"Delete from current_tasks WHERE task_id = @task_id";

                        using (NpgsqlCommand command = new NpgsqlCommand(query, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@task_id", task_id);
                            int result = command.ExecuteNonQuery();

                            if (result < 1)
                            {
                                Tracer.TaskManagerTrace.TraceEvent(
                              TraceEventType.Warning,
                              2,
                              "Попытка удалить несуществующую задачу");
                                transaction.Rollback();
                                _logger.LogWarning("[{Time}] Ошибка: задача с ID={TaskID} не была удалена",
                                    requestTime, task_id);
                                return NotFound($"Задача с ID {task_id} не найдена.");
                            }
                        }
                        Tracer.TaskManagerTrace.TraceEvent(
                              TraceEventType.Stop,
                              2,
                              $"Завершение DeleteTask. Время: {sw.ElapsedMilliseconds} мс");
                        transaction.Commit();
                        _logger.LogInformation("[{Time}] Задача с TaskID={TaskID} успешно удалена.",
                            requestTime, task_id);
                        return Ok($"Задача с ID {task_id} успешно удалена.");
                    }
                    catch (Exception ex)
                    {
                        Tracer.TaskManagerTrace.TraceEvent(
                          TraceEventType.Error,
                          3,
                          $"Ошибка в DeleteTask: {ex.Message}"
                      );
                        transaction.Rollback();
                        _logger.LogError(ex, "[{Time}] Ошибка: не удалось удалить задачу с ID={TaskID}.",
                            requestTime, task_id);
                        return StatusCode(StatusCodes.Status500InternalServerError, $"Ошибка сервера: {ex.Message}");
                    }
                }
            }
        }
        [HttpPost("ArchiveTasks")]
        public IActionResult ArchiveCompletedTasks()
        {
            var requestTime = DateTime.UtcNow;
            string connectionString = _configuration.GetConnectionString("task_management");

            Tracer.TaskManagerTrace.TraceEvent(TraceEventType.Start, 0, "Начало ArchiveTasks");
            Stopwatch sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(connectionString))
            {
                Tracer.TaskManagerTrace.TraceEvent(
                   TraceEventType.Warning,
                   2,
                   "Попытка добавить подключение с пустым названием.");
                _logger.LogError("[{Time}] Ошибка: строка подключения 'task_management' не найдена или пуста.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ошибка конфигурации: строка подключения не найдена.");
            }
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    using (var command = new NpgsqlCommand("SELECT archiveTask()", connection))
                    {
                        var result = command.ExecuteScalar();
                        int archivedCount = result != DBNull.Value && result != null ?
                            Convert.ToInt32(result) : 0;
                        Tracer.TaskManagerTrace.TraceEvent(
                             TraceEventType.Stop,
                             2,
                             $"Завершение ArchiveTasks. Время: {sw.ElapsedMilliseconds} мс");
                        _logger.LogInformation("[{Time}] Архивировано задач: {Count}",
                            requestTime, archivedCount);
                        return Ok(new
                        {
                            archived_tasks_count = archivedCount,
                            message = archivedCount > 0 ?
                                $"Архивировано {archivedCount} задач" :
                                "У вас нет выполненных задач"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Tracer.TaskManagerTrace.TraceEvent(
                          TraceEventType.Error,
                          3,
                          $"Ошибка в ArchiveTasks: {ex.Message}"
                      );
                _logger.LogError(ex, "[{Time}] Ошибка при архивации выполненных задач.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, $"Ошибка архивации: {ex.Message}");
            }
        }

        [HttpGet("GetArchivedTasks")]
        public IActionResult GetArchivedTasks()
        {
            var requestTime = DateTime.UtcNow;
            string connectionString = _configuration.GetConnectionString("task_management");

            Tracer.TaskManagerTrace.TraceEvent(TraceEventType.Start, 0, "Начало GetArchivedTasks");
            Stopwatch sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(connectionString))
            {
                Tracer.TaskManagerTrace.TraceEvent(
                   TraceEventType.Warning,
                   2,
                   "Попытка добавить подключение с пустым названием.");
                _logger.LogError("[{Time}] Ошибка: строка подключения 'task_management' не найдена или пуста.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ошибка конфигурации: строка подключения не найдена.");
            }

            try
            {
                List<ArchivedTask> archivedTasks = new List<ArchivedTask>();

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"SELECT 
                        idarchivedtask,
                        task_id,
                        completiondate,
                        task_name as task_name
                    FROM taskarchive
                    ORDER BY completiondate DESC, idarchivedtask DESC";

                    using (var command = new NpgsqlCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var archivedTask = new ArchivedTask
                                {
                                    IdArchivedTask = reader.GetInt32(reader.GetOrdinal("idarchivedtask")),
                                    TaskID = reader.GetInt32(reader.GetOrdinal("task_id")),
                                    CompletionDate = DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("completiondate"))),
                                    TaskName = reader.GetString(reader.GetOrdinal("task_name")),
                                };
                                archivedTasks.Add(archivedTask);
                            }
                        }
                    }
                }
                Tracer.TaskManagerTrace.TraceEvent(
                  TraceEventType.Stop,
                  2,
                  $"Завершение GetArchivedTasks. Время: {sw.ElapsedMilliseconds} мс");
                _logger.LogInformation("[{Time}] Успешно получено {Count} архивированных задач.",
                    requestTime, archivedTasks.Count);

                return Ok(archivedTasks);
            }
            catch (Exception ex)
            {
                Tracer.TaskManagerTrace.TraceEvent(
                          TraceEventType.Error,
                          3,
                          $"Ошибка в GetArchivedTasks: {ex.Message}"
                      );
                _logger.LogError(ex, "[{Time}] Ошибка при получении архивированных задач.", requestTime);
                return StatusCode(StatusCodes.Status500InternalServerError, $"Ошибка сервера: {ex.Message}");
            }
        }
    }
}
