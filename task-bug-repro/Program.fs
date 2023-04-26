open System.Data
open System.Data.SqlClient
open System.Threading.Tasks
open Dapper

// docker pull mcr.microsoft.com/mssql/server:2019-latest
// docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Asdf*963" -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2019-latest
let connection = @"User Id=SA;Password=Asdf*963;data source=localhost;Trusted_Connection=True;encrypt=false;";
//let connection = @"Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;";
let dbName = "testdb"

type Person = { Id: int; Name: string }

let dbInit (conn:IDbConnection) =
    dbName |> sprintf "DROP DATABASE IF EXISTS %s;" |> conn.ExecuteAsync |> Task.WaitAll
    dbName |> sprintf "CREATE DATABASE %s;" |> conn.ExecuteAsync |> Task.WaitAll
    conn.ChangeDatabase dbName

let init (conn:IDbConnection) =
    task {
        let! _ = "DROP TABLE IF EXISTS Persons" |> conn.ExecuteAsync
        let! _ =
            """
            CREATE TABLE [Persons](
                [Id] [INTEGER] NOT NULL PRIMARY KEY,
                [Name] [TEXT] NOT NULL,
            )
            """
            |> conn.ExecuteAsync
        return ()
    }


let insertValues (conn:IDbConnection) =
    task {
        let! _ = "INSERT INTO Persons VALUES (1, 'Bob')" |> conn.ExecuteAsync
        let! _ = "INSERT INTO Persons VALUES (2, 'Builder')" |> conn.ExecuteAsync
        return ()
    }

let printValues (conn:IDbConnection) =
    task {
        let! rs = "SELECT * FROM Persons" |> conn.QueryAsync<Person>
        rs |> Seq.iter (printfn "%A")
        return rs
    }

let complexTask (conn:IDbConnection) =
    task {
        let! _ =
            task {
                let updateItem f i = task {
                    let! name = f()
                    let! _ = $"UPDATE Persons SET Name = '{name}' WHERE Id = {i}" |> conn.ExecuteAsync
                    return () // this line is hit only once
                }
                let! _ = updateItem (fun _ -> task { return "UPDATED1" }) 1
                let! x = updateItem (fun _ -> task { return "UPDATED" }) 2 // we never hit this line
                return x
            }
        return ()
    }

// this works
let complexTaskNoDapperAsyncCall (conn:IDbConnection) =
    task {
        let! _ =
            task {
                let updateItem f i = task {
                    let execute (q: string) = task { return conn.Execute q }
                    let! name = f()
                    let! _ = $"UPDATE Persons SET Name = '{name}' WHERE Id = {i}" |> execute
                    return ()
                }
                let! _ = updateItem (fun _ -> task { return "UPDATED1" }) 1
                let! x = updateItem (fun _ -> task { return"UPDATED" }) 2
                return x
            }
        return ()
    }

// this works
let complexTaskOneTaskSmaller (conn:IDbConnection) =
    task {
        let! _ =
            task {
                let updateItem f i = task {
                    let name = f()
                    let! _ = $"UPDATE Persons SET Name = '{name}' WHERE Id = {i}" |> conn.ExecuteAsync
                    return ()
                }
                let! _ = updateItem (fun _ -> "UPDATED1") 1
                let! x = updateItem (fun _ -> "UPDATED") 2
                return x
            }
        return ()
    }

// this works
let complexTaskAsync (conn:IDbConnection) =
    task {
        let! _ =
            async {
                let updateItem f i = task {
                    let! name = f()
                    let! _ = $"UPDATE Persons SET Name = '{name}' WHERE Id = {i}" |> conn.ExecuteAsync
                    return ()
                }
                let! _ = updateItem (fun _ -> task { return "UPDATED1" }) 1 |> Async.AwaitTask
                let! x = updateItem (fun _ -> task { return"UPDATED" }) 2 |> Async.AwaitTask
                return x
            }
        return ()
    }

use db = new SqlConnection(connection)
try
    db.Open()
    dbInit db
    init db |> Task.WaitAll
    insertValues db |> Task.WaitAll
    printValues db |> Task.WaitAll
    complexTask db |> Task.WaitAll
    //complexTaskNoDapperAsyncCall db |> Task.WaitAll
    //complexTaskAsync db |> Task.WaitAll
    //complexTaskOneTaskSmaller db |> Task.WaitAll
    printValues db |> Task.WaitAll
finally
    db.Close()
