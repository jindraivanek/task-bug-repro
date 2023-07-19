open System.Data
open System.Data.SqlClient
open System.Threading.Tasks
open Dapper

// docker pull mcr.microsoft.com/mssql/server:2019-latest
// docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=Asdf*963" -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2019-latest
use db = new SqlConnection(@"User Id=SA;Password=Asdf*963;data source=localhost;Trusted_Connection=True;encrypt=false;")
//use db = new SqlConnection(@"Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;")
let dbName = "testdb"

type Person = { Id: int64; Name: string }

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
                [Id] [BIGINT] NOT NULL PRIMARY KEY,
                [Name] [TEXT] NOT NULL
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

let repro (conn:IDbConnection) =
    task {
        let hof f = task { return! f() }
        let! _ = $"UPDATE Persons SET Name = 'BOO' WHERE Id = 1" |> conn.ExecuteAsync
        printfn $"Task completed (line {__LINE__})" // we never hit this line
        return ()
}

// this works
let noDapperAsyncCall (conn:IDbConnection) =
    task {
        let hof f = task { return! f() }
        let execute (q: string) = task { return conn.Execute q }
        let! _ = $"UPDATE Persons SET Name = 'BOO' WHERE Id = 1" |> execute
        printfn $"Task completed (line {__LINE__})"
        return ()
}

// this works
let explicitType (conn:IDbConnection) =
    task {
        let hof (f: _ -> Task<_>) = task { return! f() }
        let! _ = $"UPDATE Persons SET Name = 'BOO' WHERE Id = 1" |> conn.ExecuteAsync
        printfn $"Task completed (line {__LINE__})"
        return ()
}

// this works
let hofOutsideTask (conn:IDbConnection) =
    let hof f = task { return! f() }
    task {
        let! _ = $"UPDATE Persons SET Name = 'BOO' WHERE Id = 1" |> conn.ExecuteAsync
        printfn $"Task completed (line {__LINE__})"
        return ()
}

// this works
let hofDefinedAfter (conn:IDbConnection) =
    task {
        let! _ = $"UPDATE Persons SET Name = 'BOO' WHERE Id = 1" |> conn.ExecuteAsync
        let hof f = task { return! f() }
        printfn $"Task completed (line {__LINE__})"
        return ()
}

// this works
let hofAsync (conn:IDbConnection) =
    task {
        let hof f = async { return! f() }
        let! _ = $"UPDATE Persons SET Name = 'BOO' WHERE Id = 1" |> conn.ExecuteAsync
        printfn $"Task completed (line {__LINE__})"
        return ()
}

// this works
let asAsync (conn:IDbConnection) =
    async {
        let hof f = async { return! f() }
        let! _ = $"UPDATE Persons SET Name = 'BOO' WHERE Id = 1" |> conn.ExecuteAsync |> Async.AwaitTask
        printfn $"Task completed (line {__LINE__})"
        return ()
}

try
    db.Open()
    dbInit db
    init db |> Task.WaitAll
    insertValues db |> Task.WaitAll
    printValues db |> Task.WaitAll
    repro db |> Task.WaitAll // this task don't finish
    noDapperAsyncCall db |> Task.WaitAll
    explicitType db |> Task.WaitAll
    hofOutsideTask db |> Task.WaitAll
    hofDefinedAfter db |> Task.WaitAll
    hofAsync db |> Task.WaitAll
    asAsync db |> Async.RunSynchronously
    printValues db |> Task.WaitAll
finally
    db.Close()
