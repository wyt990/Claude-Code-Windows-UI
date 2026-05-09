using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace ClaudeCodeGUI;

/// <summary>
/// SQLite 持久化层 — 保存会话元数据和对话记录
/// 数据库文件位于 %APPDATA%/ClaudeCodeGUI/conversations.db
/// </summary>
public class ConversationStore
{
    private readonly string _connectionString;

    public ConversationStore()
    {
        string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeCodeGUI");
        Directory.CreateDirectory(dataDir);
        string dbPath = Path.Combine(dataDir, "conversations.db");
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS conversations (
                session_item_id TEXT PRIMARY KEY,
                claude_session_id TEXT NOT NULL,
                has_started INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_item_id TEXT NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                kind TEXT NOT NULL DEFAULT 'message',
                timestamp TEXT NOT NULL,
                FOREIGN KEY (session_item_id) REFERENCES conversations(session_item_id) ON DELETE CASCADE
            );
            """;
        cmd.ExecuteNonQuery();

        // 迁移：为旧数据库添加 kind 列（如不存在）
        try
        {
            using var migrateCmd = conn.CreateCommand();
            migrateCmd.CommandText = "ALTER TABLE messages ADD COLUMN kind TEXT NOT NULL DEFAULT 'message'";
            migrateCmd.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // 列已存在，忽略
        }
    }

    /// <summary>创建新会话记录</summary>
    public void CreateConversation(string sessionItemId, string claudeSessionId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO conversations (session_item_id, claude_session_id, has_started, created_at, updated_at)
            VALUES (@sid, @cid, 0, @now, @now)
            """;
        cmd.Parameters.AddWithValue("@sid", sessionItemId);
        cmd.Parameters.AddWithValue("@cid", claudeSessionId);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>标记会话已发送过消息（后续使用 --resume）</summary>
    public void MarkAsStarted(string sessionItemId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE conversations SET has_started = 1, updated_at = @now
            WHERE session_item_id = @sid
            """;
        cmd.Parameters.AddWithValue("@sid", sessionItemId);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>查询会话是否已发送过消息</summary>
    public bool HasStarted(string sessionItemId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT has_started FROM conversations WHERE session_item_id = @sid";
        cmd.Parameters.AddWithValue("@sid", sessionItemId);
        var result = cmd.ExecuteScalar();
        return result != null && Convert.ToInt32(result) == 1;
    }

    /// <summary>保存一条消息（兼容旧格式）</summary>
    public void SaveMessage(string sessionItemId, string role, string content)
    {
        SaveEntry(sessionItemId, role, content, "message");
    }

    /// <summary>保存一条结构化条目（含 kind）</summary>
    public void SaveEntry(string sessionItemId, string role, string content, string kind)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO messages (session_item_id, role, content, kind, timestamp)
            VALUES (@sid, @role, @content, @kind, @ts)
            """;
        cmd.Parameters.AddWithValue("@sid", sessionItemId);
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@kind", kind);
        cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>从 TimelineModel 保存所有消息条目</summary>
    public void SaveTimelineMessages(string sessionItemId, TimelineModel timeline)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        foreach (var entry in timeline.Entries)
        {
            string kind;
            string content;

            if (entry.Kind == TimelineEntryKind.Message)
            {
                if (string.IsNullOrEmpty(entry.Text)) continue;
                kind = "message";
                content = entry.Text;
            }
            else if (entry.Kind == TimelineEntryKind.ToolCall)
            {
                kind = "tool_call";
                content = $"{{\"tool\":\"{entry.ToolName}\",\"id\":\"{entry.ToolId}\",\"input\":{entry.Text},\"status\":\"{entry.ToolStatus}\"}}";
            }
            else
            {
                continue;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO messages (session_item_id, role, content, kind, timestamp)
                VALUES (@sid, @role, @content, @kind, @ts)
                """;
            cmd.Parameters.AddWithValue("@sid", sessionItemId);
            cmd.Parameters.AddWithValue("@role", entry.Role);
            cmd.Parameters.AddWithValue("@content", content);
            cmd.Parameters.AddWithValue("@kind", kind);
            cmd.Parameters.AddWithValue("@ts", entry.CreatedAt.ToUniversalTime().ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>获取会话的所有消息（按时间正序，含 kind）</summary>
    public List<(string Role, string Content, DateTime Timestamp, string Kind)> GetEntries(string sessionItemId)
    {
        var results = new List<(string, string, DateTime, string)>();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT role, content, timestamp, kind FROM messages WHERE session_item_id = @sid ORDER BY id ASC";
        cmd.Parameters.AddWithValue("@sid", sessionItemId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2)),
                reader.IsDBNull(3) ? "message" : reader.GetString(3)
            ));
        }

        return results;
    }

    /// <summary>获取会话的所有消息（按时间正序，旧版签名）</summary>
    public List<(string Role, string Content, DateTime Timestamp)> GetMessages(string sessionItemId)
    {
        var results = new List<(string, string, DateTime)>();

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT role, content, timestamp FROM messages WHERE session_item_id = @sid ORDER BY id ASC";
        cmd.Parameters.AddWithValue("@sid", sessionItemId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                DateTime.Parse(reader.GetString(2))
            ));
        }

        return results;
    }

    /// <summary>删除会话及其所有消息</summary>
    public void DeleteConversation(string sessionItemId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        // 外键 CASCADE 会自动删除 messages，但 SQLite 默认不启用外键
        cmd.CommandText = """
            DELETE FROM messages WHERE session_item_id = @sid;
            DELETE FROM conversations WHERE session_item_id = @sid
            """;
        cmd.Parameters.AddWithValue("@sid", sessionItemId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>获取 ClaudeSessionId（用于恢复）</summary>
    public string? GetClaudeSessionId(string sessionItemId)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT claude_session_id FROM conversations WHERE session_item_id = @sid";
        cmd.Parameters.AddWithValue("@sid", sessionItemId);
        return cmd.ExecuteScalar() as string;
    }
}
