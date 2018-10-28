﻿using jIAnSoft.Brook;
using jIAnSoft.Nami.Clockwork;
using NLog;
using System;
using System.Data;
using System.IO;
using IdGen;

namespace Example
{
    internal static class Program
    {
        private static readonly IdGenerator Generator = new IdGenerator(0);
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private const string AllAccount = "SELECT TOP 10 {id},{name},{email} FROM {account} ORDER BY {id} DESC LIMIT 10;";

        private const string AllAccountForObject =
            "SELECT TOP 10 {id} AS {Id},{name} AS {Name},{email} AS {Email} FROM {account} ORDER BY {id} DESC LIMIT 10;";

        private const string AccountFindByName =
            "SELECT {id} AS {Id},{name} AS {Name},{email} AS {Email} FROM {account} where {name} = @name;";

        private const string AccountFindById =
            "SELECT {id} AS {Id},{name} AS {Name},{email} AS {Email} FROM {account} where {id} = @id;";

        private const string InsertAccount = "insert into {account} ({name},{email})values(@name, @email);";
        private const string DeleteAccount = "DELETE FROM {account} WHERE {id} > @id;";

        private static void RunCmd(string dbName, int count)
        {
            DatabaseType dt;
            switch (dbName)
            {
                case "mssql":
                    dt = DatabaseType.SQLServer;
                    break;
                case "posql":
                    dt = DatabaseType.PostgreSQL;
                    break;
                case "mysql":
                    dt = DatabaseType.MySQL;
                    break;
                default:
                    dt = DatabaseType.SQLite;
                    const string dbPath = @".\brook.sqlite";
                    var dbExists = File.Exists(dbPath);
                    if (!dbExists)
                    {
                        var init = File.ReadAllText("./App_Data/sqlite.sql");
                        using (var db = jIAnSoft.Brook.Mapper.Brook.Load(dbName))
                        {
                            db.Execute(init);
                        }
                    }

                    break;
            }

            var providerNme = Enum.GetName(typeof(DatabaseType), dt);
            Log.Info($"{DateTime.Now:HH:mm:ss.fff} From {providerNme} {count}");

            try
            {
                using (var db = jIAnSoft.Brook.Mapper.Brook.Load(dbName))
                {
                    var query = db.Query<Account>(ConvertSeparate(AllAccountForObject, dt));
                    var table = db.Table(ConvertSeparate(AllAccount, dt));
                    var dataSet = db.DataSet(ConvertSeparate(AccountFindByName, dt),
                        new[] {db.Parameter("@name", "許功蓋", DbType.String)});
                    var account = db.First<Account>(ConvertSeparate(AccountFindById, dt),
                        new[] {db.Parameter("@id", 1, DbType.Int32)});
                    db.Execute(ConvertSeparate(InsertAccount, dt),
                        new[]
                        {
                            db.Parameter("@name", $"{Generator.CreateId()}", DbType.String),
                            db.Parameter("@email", $"{Generator.CreateId()}@{providerNme}.com", DbType.String)
                        });

                    foreach (var row in query)
                    {
                        Log.Info($"{providerNme} Query    {row.Id} {row.Name} {row.Email}");
                    }

                    foreach (DataRow row in table.Rows)
                    {
                        Log.Info($"{providerNme} table    {row[0]} {row[1]} {row[2]}");
                    }

                    foreach (DataRow row in dataSet.Tables[0].Rows)
                    {
                        if (row != null)
                        {
                            Log.Info($"{providerNme} dataSet  {row[0]} {row[1]} {row[2]}");
                        }
                    }

                    if (null != account)
                    {
                        Log.Info($"{providerNme} First    Id:{account.Id} Email:{account.Email} Name:{account.Name}");
                    }

                    if (dt == DatabaseType.MySQL)
                    {
                        var one = db.One<int>(CommandType.StoredProcedure, "test.ReturnValue",
                            new[] {db.Parameter("@param1", DateTime.Now.Ticks / 1000 % 10000000, DbType.Int32)});
                        Log.Info($"{providerNme} One is {one}");
                    }

                    if (count % 1000 == 0)
                    {
                        db.Execute(ConvertSeparate(DeleteAccount, dt), new[] {db.Parameter("@id", 3, DbType.Int32)});
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, e.Message);
            }

            Nami.Delay(0).Milliseconds().Do(() =>
            {
                try
                {
                    RunCmd(dbName, ++count);
                }
                catch (Exception e)
                {
                    Log.Error(e, e.Message);
                }
            });

        }

        private static string ConvertSeparate(string sql, DatabaseType dt = DatabaseType.SQLite)
        {
            switch (dt)
            {
                case DatabaseType.SQLServer:
                    return sql.Replace("{", "[").Replace("}", "]").Replace("LIMIT 10", "");
                case DatabaseType.PostgreSQL:
                    return sql.Replace("{", "\"").Replace("}", "\"").Replace("TOP 10", "");
                case DatabaseType.MySQL:
                    return sql.Replace("{", "`").Replace("}", "`").Replace("TOP 10", "");
                default:
                    return sql.Replace("{", "").Replace("}", "").Replace("TOP 10", "");

            }
        }

        private static void Run(int count)
        {

            var sqlType = new []
            {
                "sqlite",
                "mssql",
                "mysql",
                "posql"
            };
            foreach (var s in sqlType)
            {
                Nami.Delay(100).Milliseconds().Do(() =>
                {
                    try
                    {
                        RunCmd(s, count);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, e.Message);
                    }
                });
            }
        }

        private static void Main(string[] args)
        {
#if NETCOREAPP2_1
            if (File.Exists("Example.dll.config"))
            {
                //There need to delete .net framework config file if we run the program as .net core app
                File.Delete("Example.dll.config");
            }
#endif
            
            Nami.Delay(100).Milliseconds().Do(() =>
            {
                Run(0);
            });
            ConsoleKeyInfo cki;
            Console.TreatControlCAsInput = true;
            Console.WriteLine("Press the CTRL + Q key to quit: \n");
            do
            {
                cki = Console.ReadKey();
            } while (cki.Key != ConsoleKey.Q && (cki.Modifiers & ConsoleModifiers.Control) == 0);

        }
    }

    public class Account
    {
        public long Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }
}

