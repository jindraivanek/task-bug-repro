open System.Data
open System.Data.SqlClient
open System.Threading.Tasks
open Dapper

// docker pull mcr.microsoft.com/mssql/server:2017-latest-ubuntu
// docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=<YourStrong!Passw0rd>" -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2017-latest-ubuntu
let connection = @"Data Source=localhost,1433;Database=master;User=sa;Password=<YourStrong!Passw0rd>;";
let dbName = "testdb"

use db = new SqlConnection(connection)

let dbInit (conn:IDbConnection) =
    dbName |> sprintf "DROP DATABASE IF EXISTS %s;" |> conn.ExecuteAsync |> Task.WaitAll
    dbName |> sprintf "CREATE DATABASE %s;" |> conn.ExecuteAsync |> Task.WaitAll
    conn.Open()
    conn.ChangeDatabase dbName

dbInit db

let init (conn:IDbConnection) =
    task {
        let! _ = "DROP TABLE IF EXISTS Persons" |> conn.ExecuteAsync
        let! _ =
            """
            CREATE TABLE [Persons](
                [Id] [INTEGER] NOT NULL PRIMARY KEY,
            )
            """
            |> conn.ExecuteAsync
        return ()
    }

init db |> Task.WaitAll

let insertValues (conn:IDbConnection) =
    task {
        let! _ = "INSERT INTO Persons VALUES (1)" |> conn.ExecuteAsync
        let! _ = "INSERT INTO Persons VALUES (2)" |> conn.ExecuteAsync
        return ()
    }

let complexTask() =
    task {
        let rs = Persons.View.generate 10
        let! _ =
            insert {
                into personsView
                values rs
            } |> conn.InsertAsync
        let! fromDb =
            task {
                let updateItem (rs: Persons.View list) f i = task {
                    let! x = f (rs |> Seq.find (fun p -> p.Position = i))
                    return!
                        update {
                            for p in personsView do
                            set x
                            where (p.Position = i)
                        } |> conn.UpdateOutputAsync<Persons.View, {| Id:Guid |}>
                }
                let! _ = updateItem rs (fun p -> task { return { p with LastName = "UPDATED3" }}) 3
                let! x = updateItem rs (fun p -> task { return { p with LastName = "UPDATED" }}) 2
                return x
            }
        let pos2Id = rs |> List.pick (fun p -> if p.Position = 2 then Some p.Id else None)

        Assert.AreEqual(pos2Id, fromDb |> Seq.head |> fun (p:{| Id:Guid |}) -> p.Id)
    }