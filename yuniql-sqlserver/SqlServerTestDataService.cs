﻿using Yuniql.Extensibility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;

namespace Yuniql.SqlServer
{
    public class SqlServerTestDataService : ITestDataService
    {
        private readonly IDataService _dataService;

        public bool IsAtomicDDLSupported => true;

        public SqlServerTestDataService(IDataService dataService)
        {
            this._dataService = dataService;
        }
        public string GetConnectionString(string databaseName)
        {
            var connectionString = EnvironmentHelper.GetEnvironmentVariable("YUNIQL_TEST_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ApplicationException("Missing environment variable YUNIQL_TEST_CONNECTION_STRING. See WIKI for developer guides.");
            }

            var result = new SqlConnectionStringBuilder(connectionString);
            result.InitialCatalog = databaseName;

            return result.ConnectionString;
        }

        public bool QuerySingleBool(string connectionString, string sqlStatement)
        {
            return _dataService.QuerySingleBool(connectionString, sqlStatement);
        }

        public string QuerySingleString(string connectionString, string sqlStatement)
        {
            return _dataService.QuerySingleString(connectionString, sqlStatement);
        }

        public string GetCurrentDbVersion(string connectionString)
        {
            _dataService.Initialize(connectionString);
            return _dataService.GetCurrentVersion();
        }

        public List<DbVersion> GetAllDbVersions(string connectionString)
        {
            _dataService.Initialize(connectionString);
            return _dataService.GetAllVersions();
        }

        public bool CheckIfDbExist(string connectionString)
        {
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            var sqlStatement = $"SELECT ISNULL(database_id, 0) FROM [sys].[databases] WHERE name = '{connectionStringBuilder.InitialCatalog}'";

            //check if database exists and auto-create when its not
            var masterConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
            masterConnectionStringBuilder.InitialCatalog = "master";

            return QuerySingleBool(masterConnectionStringBuilder.ConnectionString, sqlStatement);
        }

        public bool CheckIfDbObjectExist(string connectionString, string objectName)
        {
            var sqlStatement = $"SELECT ISNULL(OBJECT_ID('[dbo].[{objectName}]'), 0) AS ObjectID";
            return QuerySingleBool(connectionString, sqlStatement);
        }

        public string CreateDbObjectScript(string scriptName)
        {
            return $@"
CREATE PROC [dbo].[{scriptName}]
AS
    SELECT 1;
GO
                ";
        }

        public string CreateDbObjectScriptWithError(string scriptName)
        {
            return $@"
CREATE PROC [NONEXISTINGDB].[dbo].[{scriptName}]
AS
    SELECT 1/0;
GO
                ";
        }
        public string CreateTokenizedDbObjectScript(string objectName)
        {
            return $@"
CREATE PROC [dbo].[{objectName}_${{Token1}}_${{Token2}}_${{Token3}}]
AS
    SELECT '${{Token1}}.${{Token2}}.${{Token3}}' AS ReplacedStatement;
";
        }

        public string CreateBulkTableScript(string tableName)
        {
            return $@"
IF (NOT EXISTS(SELECT 1 FROM [sys].[objects] WHERE type = 'U' AND name = '{tableName}'))
BEGIN
    CREATE TABLE [dbo].[{tableName}](
	    [FirstName] [nvarchar](50) NOT NULL,
	    [LastName] [nvarchar](50) NOT NULL,
	    [BirthDate] [datetime] NULL
    );
END
            ";
        }

        public string CreateSingleLineScript(string objectName)
        {
            return $@"
CREATE PROC [dbo].[{objectName}]
AS
    SELECT 1;
GO
";
        }

        public string CreateSingleLineScriptWithoutTerminator(string objectName)
        {
            return $@"
CREATE PROC [dbo].[{objectName}]
AS
    SELECT 1;
";
        }

        public string CreateMultilineScriptWithoutTerminatorInLastLine(string objectName1, string objectName2, string objectName3)
        {
            return $@"
CREATE PROC [dbo].[{objectName1}]
AS
    SELECT 1;
GO

CREATE PROC [dbo].[{objectName2}]
AS
    SELECT 1;
GO

CREATE PROC [dbo].[{objectName3}]
AS
    SELECT 1;
";
        }

        public string CreateMultilineScriptWithTerminatorInsideStatements(string objectName1, string objectName2, string objectName3)
        {
            return $@"
CREATE PROC [dbo].[{objectName1}]
AS
    --this is a comment with GO as part of the sentence (ALL CAPS)
    SELECT 1;
GO

CREATE PROC [dbo].[{objectName2}]
AS
    --this is a comment with go as part of the sentence (small caps)
    SELECT 1;
GO

CREATE PROC [dbo].[{objectName3}]
AS
    --this is a comment with Go as part of the sentence (Pascal)
    SELECT 1;
";
        }

        public string CreateMultilineScriptWithError(string objectName1, string objectName2)
        {
            return $@"
CREATE TABLE [dbo].[{objectName1}](        
    [TestId][INT] IDENTITY(1, 1) NOT NULL,        
    [TestColumn] [DECIMAL] NOT NULL
)
GO

CREATE PROC [dbo].[{objectName2}]
AS
    SELECT 1;
GO

--throws divide by zero error
INSERT INTO [dbo].[{objectName1}] (TestColumn) VALUES (3/0);
GO

INSERT INTO [dbo].[{objectName1}] (TestColumn) VALUES (1);
INSERT INTO [dbo].[{objectName1}] (TestColumn) VALUES (2);
GO
";
        }

        public void CreateScriptFile(string sqlFilePath, string sqlStatement)
        {
            using var sw = File.CreateText(sqlFilePath);
            sw.WriteLine(sqlStatement);
        }

        public string CreateCleanupScript()
        {
            return @"
DROP PROCEDURE [dbo].[script1];
DROP PROCEDURE [dbo].[script2];
DROP PROCEDURE [dbo].[script3];
";
        }
        
        public List<BulkTestDataRow> GetBulkTestData(string connectionString, string tableName) {
            List<BulkTestDataRow> results = new List<BulkTestDataRow>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var sqlStatement = $"SELECT * FROM {tableName};";
                var command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.CommandText = sqlStatement;
                command.CommandTimeout = 0;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new BulkTestDataRow { 
                            FirstName = !reader.IsDBNull(0)? reader.GetString(0): null,
                            LastName = !reader.IsDBNull(1) ? reader.GetString(1) : null,
                            BirthDate = !reader.IsDBNull(2) ? reader.GetDateTime(2) : new DateTime?()
                        });
                    }
                }
            }
            return results;
        }
    }
}
