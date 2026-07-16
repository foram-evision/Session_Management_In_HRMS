using System;
using System.IO;
using Npgsql;

var connectionString = "Host=localhost;Port=5432;Database=hrms_poc;Username=postgres;Password=Welcome@123";
var sqlPath = Path.Combine(Directory.GetCurrentDirectory(), "StoredProcedures.sql");

Console.WriteLine("Applying StoredProcedures.sql to the database...");

var sql = File.ReadAllText(sqlPath);

using var conn = new NpgsqlConnection(connectionString);
conn.Open();

using var cmd = new NpgsqlCommand(sql, conn);
cmd.ExecuteNonQuery();

Console.WriteLine("Successfully applied stored procedures!");
